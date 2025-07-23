using Microsoft.AspNetCore.Mvc;
using _12WeekGoals.Domain.Models;
using _12WeekGoals.Services.Interfaces;
using _12WeekGoals.Models;

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

    [HttpGet("config")]
    public IActionResult GetConfiguration()
    {
        var request = HttpContext.Request;
        var currentUrl = $"{request.Scheme}://{request.Host}";
        
        return Ok(new 
        {
            message = "Configuración actual del servidor",
            server = new
            {
                currentUrl,
                scheme = request.Scheme,
                host = request.Host.ToString(),
                port = request.Host.Port,
                isHttps = request.IsHttps
            },
            endpoints = new
            {
                weekCalculator = $"{currentUrl}/api/goals/week-calculator?startDate=2025-01-01",
                auth = $"{currentUrl}/api/goals/auth",
                lists = $"{currentUrl}/api/goals/lists?code=YOUR_CODE",
                create = $"{currentUrl}/api/goals/create",
                callback = $"{currentUrl}/api/goals/callbackCreate?code=YOUR_CODE"
            },
            development = new
            {
                localUrl = "http://localhost:5194",
                httpsUrl = "https://localhost:7102"
            },
            production = new
            {
                railwayUrl = "https://12-week-goals-back-production.up.railway.app"
            },
            frontend = new
            {
                githubPages = "https://jonathan-murillo-itm.github.io",
                allowedCorsOrigins = new[]
                {
                    "http://localhost:3000",
                    "http://localhost:5173", 
                    "http://localhost:4200",
                    "https://jonathan-murillo-itm.github.io"
                }
            }
        });
    }

    [HttpGet("auth")]
    public async Task<IActionResult> GetAuthUrl()
    {
        try
        {
            var authUrl = await _goalService.GetAuthorizationUrlForWeekAsync();
            return Ok(new 
            { 
                message = "Autorización para acceder a Microsoft To Do",
                authUrl,
                instructions = new[]
                {
                    "1. Ve a la URL de arriba en tu navegador",
                    "2. Autoriza la aplicación con tu cuenta de Microsoft", 
                    "3. Copia el código de la URL de callback",
                    "4. Usa ese código en /api/goals/lists?code=TU_CODIGO"
                }
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("callback")]
    public IActionResult Callback([FromQuery] string? code = null, [FromQuery] string? error = null)
    {
        if (!string.IsNullOrEmpty(error))
        {
            return Ok(new 
            { 
                success = false,
                error = "Error en la autorización",
                details = error,
                message = "La autorización falló. Intenta de nuevo."
            });
        }

        if (string.IsNullOrEmpty(code))
        {
            return Ok(new 
            { 
                success = false,
                error = "No se recibió código de autorización",
                message = "Intenta el proceso de autorización nuevamente."
            });
        }

        return Ok(new 
        { 
            success = true,
            message = "¡Código de autorización recibido!",
            code,
            nextStep = $"Ahora usa este código en: /api/goals/lists?code={code}",
            instructions = new[]
            {
                "1. Copia el código de arriba",
                "2. Ve a /api/goals/lists?code=TU_CODIGO",
                "3. Reemplaza TU_CODIGO con el código real"
            }
        });
    }

    [HttpPost("create-sample")]
    public async Task<IActionResult> CreateSampleData([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new 
            { 
                error = "Se requiere código de autorización",
                message = "Primero llama a /api/goals/auth para obtener la URL de autorización"
            });
        }

        try
        {
            var result = await _goalService.CreateSampleTasksAsync(code);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("my-lists")]
    public async Task<IActionResult> GetMyLists()
    {
        try
        {
            var result = await _goalService.GetMyListsSimpleAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("get-lists-with-browser-and-cache")]
    public async Task<IActionResult> GetListsWithBrowserAndCache([FromBody] AutoLoginRequest request)
    {
        if (string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest(new 
            { 
                error = "Se requieren username y password"
            });
        }

        try
        {
            var result = await _goalService.GetListsWithVisibleBrowserAndCacheAsync(request.Username, request.Password);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("get-lists-from-cache")]
    public async Task<IActionResult> GetListsFromCache()
    {
        try
        {
            var result = await _goalService.GetListsWithCachedTokenAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("get-lists-with-code")]
    public async Task<IActionResult> GetListsWithCode([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new 
            { 
                error = "Se requiere código de autorización"
            });
        }

        try
        {
            var result = await _goalService.GetListsNamesOnlyAsync(code);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("debug-all")]
    public async Task<IActionResult> DebugAll([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new 
            { 
                error = "Se requiere código de autorización",
                message = "Primero llama a /api/goals/auth para obtener la URL de autorización"
            });
        }

        try
        {
            var result = await _goalService.DebugAllMicrosoftToDoAsync(code);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("lists")]
    public async Task<IActionResult> GetLists([FromQuery] string code)
    {
        if (string.IsNullOrEmpty(code))
        {
            return BadRequest(new 
            { 
                error = "Se requiere código de autorización",
                message = "Primero llama a /api/goals/auth para obtener la URL de autorización"
            });
        }

        try
        {
            var result = await _goalService.GetAllTaskListsWithTasksAsync(code);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

}
}