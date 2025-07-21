namespace _12WeekGoals.Domain.Models
{
    public class Goal
    {
        public string Name { get; set; } = string.Empty;
        public List<string> Tasks { get; set; } = new();
    }

    public class GoalGroup
    {
        public string GoalGroupName { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public List<Goal> Goals { get; set; } = new();
    }
}