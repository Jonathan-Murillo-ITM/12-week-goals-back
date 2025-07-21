using _12WeekGoals.Services;
using _12WeekGoals.Services.Interfaces;
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Dependency Injection
builder.Services.AddScoped<IMicrosoftGraphService, MicrosoftGraphService>();
builder.Services.AddScoped<IGoalService, GoalService>();

var app = builder.Build();

// Habilitar Swagger también en producción para testing
app.UseSwagger();
app.UseSwaggerUI();

// Comentar HTTPS redirect para Railway
// app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Agregar una ruta de prueba
app.MapGet("/", () => "12 Week Goals API is running!");
app.MapGet("/health", () => "OK");

app.Run();