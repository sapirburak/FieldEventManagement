// Program.cs
using FieldEventManagement.Agent.Models;
using FieldEventManagement.Agent.Repositories;
using FieldEventManagement.Agent.Services;
using Microsoft.AspNetCore.Mvc;


var builder = WebApplication.CreateBuilder(args);

// --- 1. רישום שירותי המערכת ל-Dependency Injection ---

// רישום מאגר SQLite ותור הזיכרון של ה-Agent כ-Singleton (עותק יחיד לכל האפליקציה)
builder.Services.AddSingleton<ISqliteEventRepository, SqliteEventRepository>();
builder.Services.AddSingleton<EventChannel>();

// רישום שירות הרקע שירוץ בצורה עצמאית ויקשיב לתור
builder.Services.AddHostedService<AgentBackgroundWorker>();

builder.Services.AddTransient<JwtAuthHandler>();

// InsecureClient מיועד אך ורק לסביבת פיתוח מקומית שבה אין תעודת SSL תקינה.
// בפרודקשן, ה-handler הרגיל (עם ולידציית SSL) ישמש אוטומטית.
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient("InsecureClient")
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new HttpClientHandler();
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
            return handler;
        });
}
else
{
    builder.Services.AddHttpClient("InsecureClient");
}

// רישום ה-HttpClient יחד עם מנגנון ה-Resilience המובנה של .NET (Polly)
builder.Services.AddHttpClient<IBackendClient, BackendClient>(client =>
{
    var backendUrl = builder.Configuration["AgentSettings:BackendUrl"]
        ?? throw new InvalidOperationException("AgentSettings:BackendUrl is not configured.");
    client.BaseAddress = new Uri(backendUrl);
})
.AddHttpMessageHandler<JwtAuthHandler>()
.AddStandardResilienceHandler(options =>
{
    // Timeout כולל לכלל הניסיונות (כולל retry)
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);

    // Timeout לניסיון HTTP בודד
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);

    // Circuit Breaker: יפתח לאחר 50% כשלים מתוך לפחות 5 בקשות, וישהה 30 שניות
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

    // Retry: 3 ניסיונות עם Exponential Backoff (מנוהל גם על ידי AgentBackgroundWorker)
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
});

var app = builder.Build();

app.UseHttpsRedirection();

// --- 2. חשיפת נקודת הקצה (Minimal API Endpoint) עבור מקורות חיצוניים ---

app.MapPost("/api/agent/events", async (
    [FromBody] FieldEventDto fieldEvent,
    [FromHeader(Name = "X-Api-Key")] string? apiKey,
    [FromServices] EventChannel channel,
    [FromServices] IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    try
    {
        // אבטחה: אימות מפתח ה-API שהתקבל בכותרת הבקשה
        var expectedKey = configuration["AgentSettings:ExpectedApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
        {
            return Results.Unauthorized();
        }

        // וולידציה בסיסית: בדיקה שהנתונים המרכזיים אינם ריקים
        if (string.IsNullOrWhiteSpace(fieldEvent.Title) || string.IsNullOrWhiteSpace(fieldEvent.Source))
        {
            return Results.BadRequest("Title and Source are strictly required.");
        }

        // דחיפה אסינכרונית מהירה לדיסק המקומי ולתור בזיכרון
        await channel.AddEventAsync(fieldEvent, cancellationToken);

        // החזרת קוד 202 Accepted. המקור החיצוני משתחרר מיד, וה-BackgroundWorker יטפל בשליחה אסינכרונית
        return Results.Accepted();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Error processing event"); // בדוק בטרמינל מה ה-Exception
        return Results.Problem(ex.Message);
    }
});

// הדפסת לוג יפה המציינת שה-Agent עלה בהצלחה
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=================================================");
logger.LogInformation("Field Event Management Agent is up and running!");
logger.LogInformation("=================================================");

app.Run();