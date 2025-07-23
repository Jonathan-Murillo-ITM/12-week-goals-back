using Microsoft.Graph;
using _12WeekGoals.Services.Interfaces;
using _12WeekGoals.Domain.Models;
using _12WeekGoals.Services.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

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
            var scopes = "https://graph.microsoft.com/Tasks.ReadWrite https://graph.microsoft.com/User.Read";
            var authUrl = $"https://login.microsoftonline.com/consumers/oauth2/v2.0/authorize?" +
                         $"client_id={_settings.ClientId}&" +
                         $"response_type=code&" +
                         $"redirect_uri={Uri.EscapeDataString(_settings.RedirectUri)}&" +
                         $"scope={Uri.EscapeDataString(scopes)}&" +
                         $"response_mode=query";

            return Task.FromResult(authUrl);
        }

        public async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            try
            {
                using var httpClient = new HttpClient();
                var tokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

                var parameters = new Dictionary<string, string>
                {
                    {"grant_type", "authorization_code"},
                    {"code", code},
                    {"redirect_uri", _settings.RedirectUri},
                    {"client_id", _settings.ClientId}
                };

                // Agregar client_secret si está configurado
                if (!string.IsNullOrEmpty(_settings.ClientSecret))
                {
                    parameters.Add("client_secret", _settings.ClientSecret);
                }

                var content = new FormUrlEncodedContent(parameters);
                var response = await httpClient.PostAsync(tokenUrl, content);
                var responseContent = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var tokenData = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(responseContent);
                    var accessToken = tokenData?.AccessToken;
                    
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new Exception($"No access token received in response JSON. Response: {responseContent}");
                    }
                    
                    return accessToken;
                }

                // Si no es exitoso, construir error detallado
                var debugInfo = $@"
Token Exchange Failed:
- Status: {response.StatusCode} ({(int)response.StatusCode})
- URL: {tokenUrl}
- Client ID: {_settings.ClientId}
- Has Client Secret: {!string.IsNullOrEmpty(_settings.ClientSecret)}
- Redirect URI: {_settings.RedirectUri}
- Code (first 10 chars): {code.Substring(0, Math.Min(code.Length, 10))}...
- Response: {responseContent}
- Content-Type: {response.Content.Headers.ContentType}
- Parameters sent: {string.Join(", ", parameters.Select(p => $"{p.Key}={p.Value.Substring(0, Math.Min(p.Value.Length, 20))}..."))}";

                throw new Exception($"Failed to exchange code for token: {response.StatusCode}. Details: {debugInfo}");
            }
            catch (Exception ex)
            {
                // Re-lanzar con contexto adicional
                throw new Exception($"Error in ExchangeCodeForTokenAsync: {ex.Message}", ex);
            }
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
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
        
        [JsonPropertyName("token_type")]
        public string TokenType { get; set; } = string.Empty;
        
        [JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
        
        [JsonPropertyName("scope")]
        public string Scope { get; set; } = string.Empty;
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