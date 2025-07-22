using Microsoft.Graph;
using _12WeekGoals.Services.Interfaces;
using _12WeekGoals.Domain.Models;
using _12WeekGoals.Services.Configuration;
using Microsoft.Extensions.Options;

namespace _12WeekGoals.Services
{
    public class MicrosoftGraphService : IMicrosoftGraphService
    {
        private readonly MicrosoftGraphSettings _settings;

        public MicrosoftGraphService(IOptions<MicrosoftGraphSettings> settings)
        {
            _settings = settings.Value;
        }

        public Task<string> GetAuthorizationUrlAsync()
        {
            var scopes = "Tasks.ReadWrite User.Read";
            var authUrl = $"https://login.live.com/oauth20_authorize.srf?" +
                         $"client_id={_settings.ClientId}&" +
                         $"response_type=code&" +
                         $"redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}&" +
                         $"scope={Uri.EscapeDataString(scopes)}";

            return Task.FromResult(authUrl);
        }

        public async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            using var httpClient = new HttpClient();
            var tokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

            var parameters = new Dictionary<string, string>
            {
                {"grant_type", "authorization_code"},
                {"code", code},
                {"redirect_uri", _settings.RedirectUri},
                {"client_id", _settings.ClientId},
                {"scope", "Tasks.ReadWrite User.Read"}
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync(tokenUrl, content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                var tokenData = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseContent);
                return tokenData?.AccessToken ?? throw new Exception("No access token received");
            }

            // Log detallado del error para debugging
            var debugInfo = $@"
Error Details:
- Status: {response.StatusCode}
- URL: {tokenUrl}
- Client ID: {_settings.ClientId}
- Redirect URI: {_settings.RedirectUri}
- Response: {responseContent}
- Request Parameters: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value.Substring(0, Math.Min(p.Value.Length, 50))}..."))}";

            throw new Exception($"Failed to get access token: {response.StatusCode}. Debug info: {debugInfo}");
        }

        public async Task<string> CreateTaskListAsync(string accessToken, string listName)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var listData = new { displayName = listName };
            var json = System.Text.Json.JsonSerializer.Serialize(listData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync("https://graph.microsoft.com/v1.0/me/todo/lists", content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var listResponse = System.Text.Json.JsonSerializer.Deserialize<TaskListResponse>(responseJson);
                return listResponse?.Id ?? throw new Exception("No list ID received");
            }

            throw new Exception($"Failed to create task list: {response.StatusCode}");
        }

        public async Task<bool> CreateTaskAsync(string accessToken, string listId, string taskTitle, DateTime dueDate)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var taskData = new
            {
                title = taskTitle,
                dueDateTime = new
                {
                    dateTime = dueDate.ToString("yyyy-MM-ddTHH:mm:ss.fffK"),
                    timeZone = "UTC"
                }
            };

            var json = System.Text.Json.JsonSerializer.Serialize(taskData);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await httpClient.PostAsync(
                $"https://graph.microsoft.com/v1.0/me/todo/lists/{listId}/tasks", content);

            return response.IsSuccessStatusCode;
        }

        public async Task<List<TaskList>> GetTaskListsAsync(string accessToken)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync("https://graph.microsoft.com/v1.0/me/todo/lists");

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var listsResponse = System.Text.Json.JsonSerializer.Deserialize<TaskListsResponse>(responseJson);
                
                return listsResponse?.Value?.Select(l => new TaskList 
                { 
                    Id = l.Id, 
                    DisplayName = l.DisplayName 
                }).ToList() ?? new List<TaskList>();
            }

            throw new Exception($"Failed to get task lists: {response.StatusCode}");
        }

        public async Task<List<TodoTask>> GetTasksFromListAsync(string accessToken, string listId)
        {
            using var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            var response = await httpClient.GetAsync($"https://graph.microsoft.com/v1.0/me/todo/lists/{listId}/tasks");

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                var tasksResponse = System.Text.Json.JsonSerializer.Deserialize<TasksResponse>(responseJson);
                
                return tasksResponse?.Value?.Select(t => new TodoTask 
                { 
                    Id = t.Id, 
                    Title = t.Title,
                    DueDateTime = t.DueDateTime?.DateTime,
                    Status = t.Status
                }).ToList() ?? new List<TodoTask>();
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            throw new Exception($"Failed to get tasks from list: {response.StatusCode} - {errorContent}");
        }
    }

    // Clases auxiliares para deserialización
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }

    public class TaskListResponse
    {
        public string Id { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class TaskListsResponse
    {
        public List<TaskListResponse> Value { get; set; } = new();
    }

    public class TaskResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public TaskDateTimeResponse? DueDateTime { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class TaskDateTimeResponse
    {
        public DateTime DateTime { get; set; }
        public string TimeZone { get; set; } = string.Empty;
    }

    public class TasksResponse
    {
        public List<TaskResponse> Value { get; set; } = new();
    }
}