using _12WeekGoals.Services;
using _12WeekGoals.Services.Interfaces;
using _12WeekGoals.Services.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "https://jonathan-murillo-itm.github.io",
            "http://localhost:3000",
            "http://localhost:5173",
            "http://localhost:4200"
        )
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials();
    });
});

// Configure MicrosoftGraph settings
builder.Services.Configure<MicrosoftGraphSettings>(
    builder.Configuration.GetSection("MicrosoftGraph"));

// Dependency Injection
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();
builder.Services.AddScoped<IGoalService, GoalService>();
builder.Services.AddSingleton<ITokenCacheService, TokenCacheService>();

var app = builder.Build();

// Use CORS
app.UseCors("AllowFrontend");

// Habilitar Swagger también en producción para testing
app.UseSwagger();
app.UseSwaggerUI();

// Comentar HTTPS redirect para Railway
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Agregar una ruta de prueba
app.MapGet("/", () => "12 Week Goals API is running!");

app.Run();