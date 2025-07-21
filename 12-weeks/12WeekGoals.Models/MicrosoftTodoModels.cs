namespace _12WeekGoals.Domain.Models
{
    public class TaskList
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class TodoTask
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public DateTime? DueDateTime { get; set; }
    public string Status { get; set; } = "notStarted";
}
}