using _12WeekGoals.Domain.Models;

namespace _12WeekGoals.Services.Interfaces
{
    public interface IGoalService
    {
        Task<string> CreateGoalsAsync(GoalGroup goalGroup);
        Task<bool> ProcessGoalCreationAsync(string code, GoalGroup goalGroup);
        Task<string> GetAuthorizationUrlForWeekAsync();
        Task<int> GetCurrentWeekAsync(string code);
        Task<object> DebugTasksAsync(string code);
        Task<object> CreateSampleTasksAsync(string code);
        Task<object> DebugAllMicrosoftToDoAsync(string code);
        Task<object> GetAllTaskListsWithTasksAsync(string code);
        Task<dynamic> GetMyListsSimpleAsync();
        Task<dynamic> GetListsNamesOnlyAsync(string code);
        Task<dynamic> GetListsWithVisibleBrowserAndCacheAsync(string username, string password);
        Task<dynamic> GetListsWithCachedTokenAsync();
    }
}
