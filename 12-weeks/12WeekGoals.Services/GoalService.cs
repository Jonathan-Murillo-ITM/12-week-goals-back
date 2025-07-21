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
    }
}