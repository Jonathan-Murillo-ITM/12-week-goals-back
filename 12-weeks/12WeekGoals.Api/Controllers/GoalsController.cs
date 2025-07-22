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

    [HttpGet("callbackCreate")]
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

    [HttpGet("week-calculator")]
    public IActionResult CalculateCurrentWeek([FromQuery] string? startDate = null)
    {
        try
        {
            DateTime start;
            
            if (string.IsNullOrEmpty(startDate))
            {
                // Si no se proporciona fecha, mostrar instrucciones
                return Ok(new 
                {
                    message = "Calculadora de Semana Actual - 12 Week Goals",
                    instructions = new[]
                    {
                        "Uso: /api/goals/week-calculator?startDate=YYYY-MM-DD",
                        "Ejemplo: /api/goals/week-calculator?startDate=2025-04-04",
                        "La fecha debe ser el inicio de tus 12 semanas"
                    },
                    currentDate = DateTime.Now.ToString("yyyy-MM-dd"),
                    examples = new[]
                    {
                        $"/api/goals/week-calculator?startDate=2025-04-04",
                        $"/api/goals/week-calculator?startDate=2025-07-01",
                        $"/api/goals/week-calculator?startDate={DateTime.Now.AddDays(-30):yyyy-MM-dd}"
                    }
                });
            }

            // Intentar parsear la fecha
            if (!DateTime.TryParse(startDate, out start))
            {
                return BadRequest(new 
                { 
                    error = "Formato de fecha inválido",
                    message = "Usa el formato: YYYY-MM-DD (ejemplo: 2025-04-04)",
                    received = startDate
                });
            }

            // Calcular la semana actual
            var currentDate = DateTime.Now;
            var daysSinceStart = (currentDate - start).Days;
            
            // Si estamos antes de la fecha de inicio
            if (daysSinceStart < 0)
            {
                return Ok(new
                {
                    currentWeek = 0,
                    message = "Aún no has comenzado tus 12 semanas",
                    startDate = start.ToString("dd/MM/yyyy"),
                    currentDate = currentDate.ToString("dd/MM/yyyy"),
                    daysUntilStart = Math.Abs(daysSinceStart),
                    willStartIn = $"{Math.Abs(daysSinceStart)} día(s)"
                });
            }

            var currentWeek = Math.Max(1, (daysSinceStart / 7) + 1);
            var actualWeek = Math.Min(currentWeek, 12);
            var isCompleted = currentWeek > 12;

            return Ok(new 
            { 
                currentWeek = actualWeek,
                message = isCompleted 
                    ? $"¡Felicidades! Completaste tus 12 semanas. Estás en la semana {currentWeek}."
                    : $"Estás en la semana {actualWeek} de tus 12 semanas de metas.",
                startDate = start.ToString("dd/MM/yyyy"),
                currentDate = currentDate.ToString("dd/MM/yyyy"),
                totalWeeks = 12,
                weeksCompleted = actualWeek,
                weeksRemaining = Math.Max(0, 12 - actualWeek),
                progressPercentage = Math.Round((double)actualWeek / 12 * 100, 1),
                daysSinceStart = daysSinceStart,
                isCompleted = isCompleted,
                nextWeekStartsOn = start.AddDays((actualWeek) * 7).ToString("dd/MM/yyyy")
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

}
}