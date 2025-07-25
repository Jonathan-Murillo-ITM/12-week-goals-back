namespace _12WeekGoals.Models
{
    public class AutoLoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }

    public class AuthCodeRequest
    {
        public string Code { get; set; } = string.Empty;
    }
}
