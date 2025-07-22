using _12WeekGoals.Domain.Models;
using _12WeekGoals.Services.Interfaces;

namespace _12WeekGoals.Services
{
    public class GoalService : IGoalService
    {
        private readonly IMicrosoftGraphService _graphService;

        public GoalService(IMicrosoftGraphService graphService)
        {
            _graphService = graphService;
        }

        public async Task<string> CreateGoalsAsync(GoalGroup goalGroup)
        {
            return await _graphService.GetAuthorizationUrlAsync();
        }

        public async Task<bool> ProcessGoalCreationAsync(string code, GoalGroup goalGroup)
        {
            try
            {
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);

                foreach (var goal in goalGroup.Goals)
                {
                    var listId = await _graphService.CreateTaskListAsync(accessToken, goal.Name);

                    for (int i = 0; i < goal.Tasks.Count; i++)
                    {
                        var dueDate = goalGroup.StartDate.AddDays(i * 7); // Cada semana

                        // Ajustar al domingo
                        while (dueDate.DayOfWeek != DayOfWeek.Sunday)
                        {
                            dueDate = dueDate.AddDays(1);
                        }

                        await _graphService.CreateTaskAsync(accessToken, listId, goal.Tasks[i], dueDate);
                    }
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task<string> GetAuthorizationUrlForWeekAsync()
        {
            return await _graphService.GetAuthorizationUrlAsync();
        }

        public async Task<int> GetCurrentWeekAsync(string code)
        {
            try
            {
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                var taskLists = await _graphService.GetTaskListsAsync(accessToken);

                if (!taskLists.Any())
                {
                    throw new Exception("No tienes listas de tareas en Microsoft To Do. Necesitas crear metas primero usando el endpoint /api/goals/create");
                }

                // Buscar en todas las listas para encontrar tareas con fechas
                var allTasks = new List<TodoTask>();
                var listInfo = new List<string>();

                foreach (var list in taskLists)
                {
                    try
                    {
                        var tasks = await _graphService.GetTasksFromListAsync(accessToken, list.Id);
                        allTasks.AddRange(tasks);
                        
                        var tasksWithDatesCount = tasks.Where(t => t.DueDateTime.HasValue).Count();
                        listInfo.Add($"Lista '{list.DisplayName}': {tasks.Count} tareas ({tasksWithDatesCount} con fechas)");
                    }
                    catch (Exception ex)
                    {
                        listInfo.Add($"Lista '{list.DisplayName}': Error - {ex.Message}");
                        continue;
                    }
                }

                var tasksWithDates = allTasks.Where(t => t.DueDateTime.HasValue).ToList();

                if (!tasksWithDates.Any())
                {
                    var detailedMessage = $"Encontré {taskLists.Count} lista(s) de tareas con {allTasks.Count} tarea(s) total, pero ninguna tiene fechas de vencimiento.\n\n" +
                                        $"Detalles por lista:\n{string.Join("\n", listInfo)}\n\n" +
                                        $"Para usar el sistema de 12 semanas, las tareas necesitan tener fechas de vencimiento configuradas.";
                    throw new Exception(detailedMessage);
                }

                // Encontrar la fecha más temprana que sería la primera semana
                var earliestDate = tasksWithDates.Min(t => t.DueDateTime!.Value);
                var latestDate = tasksWithDates.Max(t => t.DueDateTime!.Value);

                // Calcular la semana actual basada en la fecha más temprana
                var currentDate = DateTime.Now;
                var daysSinceStart = (currentDate - earliestDate).Days;
                
                // Si estamos antes de la fecha de inicio, estamos en la semana 1
                if (daysSinceStart < 0)
                {
                    return 1;
                }

                var currentWeek = Math.Max(1, (daysSinceStart / 7) + 1);

                // Asegurar que no exceda las 12 semanas
                return Math.Min(currentWeek, 12);
            }
            catch (Exception ex)
            {
                // Re-lanzar con el mensaje original si ya es descriptivo
                if (ex.Message.Contains("lista") || ex.Message.Contains("fechas") || ex.Message.Contains("metas") || ex.Message.Contains("Encontré"))
                {
                    throw;
                }
                
                // Mensaje genérico para otros errores
                throw new Exception($"Error al acceder a Microsoft To Do: {ex.Message}. Verifica tu conexión y permisos.");
            }
        }

        public async Task<object> DebugTasksAsync(string code)
        {
            try
            {
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                var taskLists = await _graphService.GetTaskListsAsync(accessToken);

                var debugInfo = new
                {
                    totalLists = taskLists.Count,
                    lists = new List<object>()
                };

                foreach (var list in taskLists)
                {
                    try
                    {
                        var tasks = await _graphService.GetTasksFromListAsync(accessToken, list.Id);
                        
                        var listDebug = new
                        {
                            listName = list.DisplayName,
                            listId = list.Id,
                            totalTasks = tasks.Count,
                            tasksWithDates = tasks.Where(t => t.DueDateTime.HasValue).Count(),
                            tasks = tasks.Select(t => new
                            {
                                title = t.Title,
                                dueDate = t.DueDateTime?.ToString("yyyy-MM-dd HH:mm:ss"),
                                status = t.Status,
                                hasDueDate = t.DueDateTime.HasValue
                            }).ToList()
                        };

                        ((List<object>)debugInfo.lists).Add(listDebug);
                    }
                    catch (Exception ex)
                    {
                        ((List<object>)debugInfo.lists).Add(new
                        {
                            listName = list.DisplayName,
                            error = ex.Message
                        });
                    }
                }

                return debugInfo;
            }
            catch (Exception ex)
            {
                return new { error = ex.Message };
            }
        }
    }
}