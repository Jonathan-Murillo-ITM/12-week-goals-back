namespace _12WeekGoals.Services.Configuration
{
    public class MicrosoftGraphSettings
    {
        public string ClientId { get; set; } = string.Empty;
        public string ClientSecret { get; set; } = string.Empty;
        public string TenantId { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string CallbackPath { get; set; } = string.Empty;
        
        public string RedirectUri => $"{BaseUrl.TrimEnd('/')}{CallbackPath}";
    }
}
