using Microsoft.AspNetCore.Mvc;
using _12WeekGoals.Domain.Models;
using _12WeekGoals.Services.Interfaces;

namespace _12WeekGoals.Api.Controllers
{
    [ApiController]
[Route("api/[controller]")]
public class GoalsController : ControllerBase
{
    private readonly IGoalService _goalService;
    private static GoalGroup? _currentGoalGroup; // Temporal storage

    public GoalsController(IGoalService goalService)
    {
        _goalService = goalService;
    }

    [HttpPost("create")]
    public async Task<IActionResult> CreateGoals([FromBody] GoalGroup goalGroup)
    {
        _currentGoalGroup = goalGroup;
        var authUrl = await _goalService.CreateGoalsAsync(goalGroup);

        return Ok(new { message = "Redirige a tu navegador para iniciar sesión", authUrl });
    }

    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code)
    {
        if (_currentGoalGroup == null)
        {
            return BadRequest("No goal group found");
        }

        var success = await _goalService.ProcessGoalCreationAsync(code, _currentGoalGroup);

        if (success)
        {
            return Ok(new { message = $"Metas del grupo '{_currentGoalGroup.GoalGroupName}' creadas exitosamente." });
        }

        return BadRequest("Failed to create goals");
    }
}
}