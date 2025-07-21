using Microsoft.Graph;
using _12WeekGoals.Services.Interfaces;
using _12WeekGoals.Domain.Models;

namespace _12WeekGoals.Services
{
    public class MicrosoftGraphService : IMicrosoftGraphService
    {
        private readonly string _clientId = "4f21c94e-03ae-4718-839c-bf46f777bed6";
        private readonly string _tenantId = "f8cdef31-a31e-4b4a-93e4-5f571e91255a";
        private readonly string _redirectUri = "http://localhost:8000/callback";

        public async Task<string> GetAuthorizationUrlAsync()
        {
            var scopes = "Tasks.ReadWrite User.Read";
            var authUrl = $"https://login.live.com/oauth20_authorize.srf?" +
                         $"client_id={_clientId}&" +
                         $"response_type=code&" +
                         $"redirect_uri={Uri.EscapeDataString(_redirectUri)}&" +
                         $"scope={Uri.EscapeDataString(scopes)}";

            return authUrl;
        }

        public async Task<string> ExchangeCodeForTokenAsync(string code)
        {
            using var httpClient = new HttpClient();
            var tokenUrl = "https://login.microsoftonline.com/consumers/oauth2/v2.0/token";

            var parameters = new Dictionary<string, string>
            {
                {"grant_type", "authorization_code"},
                {"code", code},
                {"redirect_uri", _redirectUri},
                {"client_id", _clientId},
                {"scope", "Tasks.ReadWrite User.Read"}
            };

            var content = new FormUrlEncodedContent(parameters);
            var response = await httpClient.PostAsync(tokenUrl, content);

            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var tokenData = System.Text.Json.JsonSerializer.Deserialize<TokenResponse>(jsonResponse);
                return tokenData?.AccessToken ?? throw new Exception("No access token received");
            }

            throw new Exception($"Failed to get access token: {response.StatusCode}");
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
    }

    // Clases auxiliares para deserialización
    public class TokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }

    public class TaskListResponse
    {
        public string Id { get; set; } = string.Empty;
    }
}