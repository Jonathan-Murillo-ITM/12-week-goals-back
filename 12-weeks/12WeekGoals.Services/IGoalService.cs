using _12WeekGoals.Domain.Models;

namespace _12WeekGoals.Services.Interfaces
{
    public interface IGoalService
    {
        Task<string> CreateGoalsAsync(GoalGroup goalGroup);
        Task<bool> ProcessGoalCreationAsync(string code, GoalGroup goalGroup);
    }
}