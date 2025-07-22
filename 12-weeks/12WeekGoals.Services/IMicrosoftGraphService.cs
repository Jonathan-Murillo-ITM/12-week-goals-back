using _12WeekGoals.Domain.Models;

namespace _12WeekGoals.Services.Interfaces
{
    public interface IMicrosoftGraphService
    {
        Task<string> GetAuthorizationUrlAsync();
        Task<string> ExchangeCodeForTokenAsync(string code);
        Task<string> CreateTaskListAsync(string accessToken, string listName);
        Task<bool> CreateTaskAsync(string accessToken, string listId, string taskTitle, DateTime dueDate);
        Task<List<TaskList>> GetTaskListsAsync(string accessToken);
        Task<List<TodoTask>> GetTasksFromListAsync(string accessToken, string listId);
    }
}