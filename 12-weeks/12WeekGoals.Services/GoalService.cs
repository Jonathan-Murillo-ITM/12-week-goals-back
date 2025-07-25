using _12WeekGoals.Domain.Models;
using _12WeekGoals.Services.Interfaces;

namespace _12WeekGoals.Services
{
    public class GoalService : IGoalService
    {
        private readonly IMicrosoftGraphService _graphService;
        private readonly ITokenCacheService _tokenCacheService;

        public GoalService(IMicrosoftGraphService graphService, ITokenCacheService tokenCacheService)
        {
            _graphService = graphService;
            _tokenCacheService = tokenCacheService;
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
                // Paso 1: Intentar obtener el token de acceso
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    return new 
                    { 
                        success = false,
                        error = "No se pudo obtener el token de acceso",
                        step = "exchange_code_for_token",
                        codeProvided = !string.IsNullOrEmpty(code),
                        codeLength = code?.Length ?? 0
                    };
                }
                
                // Paso 2: Intentar obtener las listas
                var taskLists = await _graphService.GetTaskListsAsync(accessToken);

                var debugInfo = new
                {
                    success = true,
                    tokenObtained = true,
                    tokenLength = accessToken.Length,
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
                // Debug detallado del error - mostrar la excepción completa
                return new 
                { 
                    success = false,
                    error = ex.Message,
                    fullError = ex.ToString(),
                    step = ex.Message.Contains("exchange") ? "exchange_code_for_token" : 
                           ex.Message.Contains("task lists") ? "get_task_lists" : "unknown",
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    codeProvided = !string.IsNullOrEmpty(code),
                    codeLength = code?.Length ?? 0
                };
            }
        }
        public async Task<object> CreateSampleTasksAsync(string code)
        {
            try
            {
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No se pudo obtener el token de acceso");
                }

                // Crear una lista de ejemplo para el sistema de 12 semanas
                var startDate = DateTime.Now.Date; // Comenzar hoy
                var listName = $"12 Semanas - Iniciado {startDate:dd/MM/yyyy}";
                
                var listId = await _graphService.CreateTaskListAsync(accessToken, listName);

                // Crear 12 tareas, una para cada semana
                var tasksCreated = new List<object>();
                
                for (int week = 1; week <= 12; week++)
                {
                    var dueDate = startDate.AddDays((week - 1) * 7); // Cada 7 días
                    
                    // Ajustar al domingo si no es domingo
                    while (dueDate.DayOfWeek != DayOfWeek.Sunday)
                    {
                        dueDate = dueDate.AddDays(1);
                    }
                    
                    var taskTitle = $"Semana {week} - Meta semanal";
                    var success = await _graphService.CreateTaskAsync(accessToken, listId, taskTitle, dueDate);
                    
                    tasksCreated.Add(new 
                    {
                        week,
                        title = taskTitle,
                        dueDate = dueDate.ToString("yyyy-MM-dd"),
                        dayOfWeek = dueDate.DayOfWeek.ToString(),
                        created = success
                    });
                }

                return new 
                {
                    success = true,
                    message = "¡Lista de ejemplo creada exitosamente!",
                    listName,
                    listId,
                    startDate = startDate.ToString("yyyy-MM-dd"),
                    totalTasksCreated = tasksCreated.Count(t => ((dynamic)t).created),
                    tasks = tasksCreated,
                    nextSteps = new[]
                    {
                        "1. Ve a Microsoft To Do en tu navegador o app",
                        "2. Verifica que se creó la lista con 12 tareas",
                        "3. Ahora puedes usar /api/goals/lists para ver tus listas",
                        "4. O usar /api/goals/week-calculator para calcular tu semana actual"
                    }
                };
            }
            catch (Exception ex)
            {
                return new 
                { 
                    success = false,
                    error = ex.Message,
                    step = "create_sample_tasks"
                };
            }
        }
        public async Task<object> GetMyListsSimpleAsync()
        {
            try
            {
                // Este endpoint devuelve información para que el frontend maneje la autorización
                var authUrl = await _graphService.GetAuthorizationUrlAsync();
                
                return new
                {
                    requiresAuth = true,
                    message = "Para ver tus listas, necesitas autorizar la aplicación",
                    authUrl,
                    instructions = new[]
                    {
                        "Haz clic en 'Autorizar' para conectarte a Microsoft To Do",
                        "Después de autorizar, tus listas aparecerán automáticamente"
                    },
                    frontendFlow = new
                    {
                        step1 = "Redirigir al usuario a authUrl",
                        step2 = "Cuando regrese con el código, llamar a /api/goals/get-lists-with-code?code=CODIGO",
                        step3 = "Mostrar las listas al usuario"
                    }
                };
            }
            catch (Exception ex)
            {
                return new 
                { 
                    success = false,
                    error = ex.Message,
                    step = "get_my_lists_simple"
                };
            }
        }

        public async Task<object> GetAllTaskListsWithTasksAsync(string code)
        {
            try
            {
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No se pudo obtener el token de acceso");
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                // Primero obtener todas las listas
                var listsResponse = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/todo/lists");
                var listsContent = await listsResponse.Content.ReadAsStringAsync();

                if (!listsResponse.IsSuccessStatusCode)
                {
                    throw new Exception($"Error obteniendo listas: {listsContent}");
                }

                var listsData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(listsContent);
                var lists = new List<object>();

                // Para cada lista, obtener sus tareas
                foreach (var list in ((System.Text.Json.JsonElement)listsData).GetProperty("value").EnumerateArray())
                {
                    var listId = list.GetProperty("id").GetString();
                    var displayName = list.GetProperty("displayName").GetString();

                    try
                    {
                        // Obtener tareas de esta lista
                        var tasksResponse = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/me/todo/lists/{listId}/tasks");
                        var tasksContent = await tasksResponse.Content.ReadAsStringAsync();

                        var tasks = new List<object>();
                        var tasksWithDates = 0;

                        if (tasksResponse.IsSuccessStatusCode)
                        {
                            var tasksData = System.Text.Json.JsonSerializer.Deserialize<dynamic>(tasksContent);
                            
                            foreach (var task in ((System.Text.Json.JsonElement)tasksData).GetProperty("value").EnumerateArray())
                            {
                                var title = task.TryGetProperty("title", out var titleProp) ? titleProp.GetString() : "Sin título";
                                var status = task.TryGetProperty("status", out var statusProp) ? statusProp.GetString() : "notStarted";
                                
                                DateTime? dueDate = null;
                                var hasDueDate = false;
                                
                                if (task.TryGetProperty("dueDateTime", out var dueProp) && dueProp.ValueKind != System.Text.Json.JsonValueKind.Null)
                                {
                                    if (dueProp.TryGetProperty("dateTime", out var dateProp))
                                    {
                                        if (DateTime.TryParse(dateProp.GetString(), out var parsedDate))
                                        {
                                            dueDate = parsedDate;
                                            hasDueDate = true;
                                            tasksWithDates++;
                                        }
                                    }
                                }

                                tasks.Add(new
                                {
                                    title,
                                    status,
                                    dueDate = dueDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                                    hasDueDate
                                });
                            }
                        }

                        lists.Add(new
                        {
                            listName = displayName,
                            listId,
                            totalTasks = tasks.Count,
                            tasksWithDates,
                            tasks
                        });
                    }
                    catch (Exception ex)
                    {
                        lists.Add(new
                        {
                            listName = displayName,
                            listId,
                            error = ex.Message
                        });
                    }
                }

                return new
                {
                    success = true,
                    message = $"Encontré {lists.Count} lista(s) en tu Microsoft To Do",
                    totalLists = lists.Count,
                    lists,
                    summary = new
                    {
                        totalLists = lists.Count,
                        totalTasks = lists.Sum(l => ((dynamic)l).totalTasks ?? 0),
                        totalTasksWithDates = lists.Sum(l => ((dynamic)l).tasksWithDates ?? 0)
                    },
                    instructions = new[]
                    {
                        "Estas son todas tus listas de Microsoft To Do con sus tareas",
                        "Para calcular tu semana actual basada en las fechas de las tareas:",
                        "Usa /api/goals/week-calculator?startDate=YYYY-MM-DD",
                        "O usa /api/goals/week-calculator sin parámetros para ver instrucciones"
                    }
                };
            }
            catch (Exception ex)
            {
                return new 
                { 
                    success = false,
                    error = ex.Message,
                    step = "get_all_task_lists_with_tasks"
                };
            }
        }

        public async Task<object> DebugAllMicrosoftToDoAsync(string code)
        {
            try
            {
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No se pudo obtener el token de acceso");
                }

                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var results = new
                {
                    success = true,
                    tokenLength = accessToken.Length,
                    timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    endpoints = new List<object>()
                };

                // Probar diferentes endpoints de Microsoft Graph
                var endpoints = new[]
                {
                    new { name = "Task Lists", url = "https://graph.microsoft.com/v1.0/me/todo/lists" },
                    new { name = "Task Lists (beta)", url = "https://graph.microsoft.com/beta/me/todo/lists" },
                    new { name = "All Tasks", url = "https://graph.microsoft.com/v1.0/me/todo/lists?$expand=tasks" },
                    new { name = "User Info", url = "https://graph.microsoft.com/v1.0/me" },
                    new { name = "Mail Folders", url = "https://graph.microsoft.com/v1.0/me/mailFolders" }
                };

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        var response = await httpClient.GetAsync(endpoint.url);
                        var content = await response.Content.ReadAsStringAsync();
                        
                        ((List<object>)results.endpoints).Add(new
                        {
                            endpoint = endpoint.name,
                            url = endpoint.url,
                            status = response.StatusCode.ToString(),
                            isSuccess = response.IsSuccessStatusCode,
                            contentLength = content.Length,
                            response = response.IsSuccessStatusCode 
                                ? (content.Length > 2000 ? content.Substring(0, 2000) + "... (truncated)" : content)
                                : content
                        });
                    }
                    catch (Exception ex)
                    {
                        ((List<object>)results.endpoints).Add(new
                        {
                            endpoint = endpoint.name,
                            url = endpoint.url,
                            error = ex.Message
                        });
                    }
                }

                return results;
            }
            catch (Exception ex)
            {
                return new 
                { 
                    success = false,
                    error = ex.Message,
                    step = "debug_all_microsoft_todo"
                };
            }
        }

        public async Task<dynamic> GetListsNamesOnlyAsync(string code)
        {
            try
            {
                // Obtener token con el código
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No se pudo obtener token de acceso");
                }

                // Obtener solo los nombres de las listas
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetStringAsync("https://graph.microsoft.com/v1.0/me/todo/lists");
                
                // Extraer solo los nombres de las listas
                var jsonDocument = System.Text.Json.JsonDocument.Parse(response);
                var lists = jsonDocument.RootElement.GetProperty("value");
                
                var listNames = new List<string>();
                foreach (var list in lists.EnumerateArray())
                {
                    if (list.TryGetProperty("displayName", out var nameProperty))
                    {
                        var name = nameProperty.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            listNames.Add(name);
                        }
                    }
                }

                return new { 
                    success = true,
                    totalLists = listNames.Count,
                    listNames = listNames 
                };
            }
            catch (Exception ex)
            {
                return new { 
                    success = false,
                    error = ex.Message 
                };
            }
        }

        public async Task<dynamic> GetListsWithCodeAndCacheAsync(string code)
        {
            try
            {
                // Obtener token usando el código
                var accessToken = await _graphService.ExchangeCodeForTokenAsync(code);
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new Exception("No se pudo obtener token de acceso con el código proporcionado");
                }

                // Guardar el token en cache para uso futuro
                await _tokenCacheService.SaveTokenAsync(accessToken);

                // Obtener las listas
                using var httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetStringAsync("https://graph.microsoft.com/v1.0/me/todo/lists");
                
                var jsonDocument = System.Text.Json.JsonDocument.Parse(response);
                var lists = jsonDocument.RootElement.GetProperty("value");
                
                var listNames = new List<string>();
                foreach (var list in lists.EnumerateArray())
                {
                    if (list.TryGetProperty("displayName", out var nameProperty))
                    {
                        var name = nameProperty.GetString();
                        if (!string.IsNullOrEmpty(name))
                        {
                            listNames.Add(name);
                        }
                    }
                }

                return new { 
                    success = true,
                    totalLists = listNames.Count,
                    listNames = listNames,
                    source = "auth_code_with_cache",
                    tokenCached = true,
                    message = "Listas obtenidas usando código de autorización y token guardado para próximas veces"
                };
            }
            catch (Exception ex)
            {
                return new { 
                    success = false,
                    error = ex.Message,
                    source = "auth_code_error"
                };
            }
        }

        public async Task<dynamic> GetListsWithCachedTokenAsync()
        {
            try
            {
                // Primero intentar con token en cache
                var cachedToken = await _tokenCacheService.GetValidTokenAsync();
                
                if (!string.IsNullOrEmpty(cachedToken))
                {
                    // Usar token del cache
                    using var httpClient = new HttpClient();
                    httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", cachedToken);

                    var response = await httpClient.GetStringAsync("https://graph.microsoft.com/v1.0/me/todo/lists");
                    
                    var jsonDocument = System.Text.Json.JsonDocument.Parse(response);
                    var lists = jsonDocument.RootElement.GetProperty("value");
                    
                    var listNames = new List<string>();
                    foreach (var list in lists.EnumerateArray())
                    {
                        if (list.TryGetProperty("displayName", out var nameProperty))
                        {
                            var name = nameProperty.GetString();
                            if (!string.IsNullOrEmpty(name))
                            {
                                listNames.Add(name);
                            }
                        }
                    }

                    return new { 
                        success = true,
                        totalLists = listNames.Count,
                        listNames = listNames,
                        source = "cached_token",
                        message = "Listas obtenidas usando token guardado"
                    };
                }
                else
                {
                    return new { 
                        success = false,
                        error = "No hay token válido en cache. Necesitas autenticarte primero.",
                        requiresAuth = true,
                        source = "no_cache"
                    };
                }
            }
            catch (Exception ex)
            {
                // Si el token en cache no funciona, limpiarlo
                await _tokenCacheService.ClearTokenAsync();
                
                return new { 
                    success = false,
                    error = ex.Message,
                    requiresAuth = true,
                    source = "cache_error"
                };
            }
        }
    }
}