// Program.cs
using FieldEventManagement.Agent.Models;
using FieldEventManagement.Agent.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// --- 1. רישום שירותי המערכת ל-Dependency Injection ---

// רישום תור הזיכרון ומערכת הגיבוי המקומית כ-Singleton (עותק יחיד לכל האפליקציה)
builder.Services.AddSingleton<EventChannel>();

// רישום שירות הרקע שירוץ בצורה עצמאית ויקשיב לתור
builder.Services.AddHostedService<AgentBackgroundWorker>();

builder.Services.AddTransient<JwtAuthHandler>();

builder.Services.AddHttpClient("InsecureClient")
    .ConfigurePrimaryHttpMessageHandler(() =>
    {
        var handler = new HttpClientHandler();
        handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
        return handler;
    });

// רישום ה-HttpClient יחד עם מנגנון ה-Resilience המובנה של .NET 10 (Polly החדש)
builder.Services.AddHttpClient<IBackendClient, BackendClient>(client =>
{
    var backendUrl = builder.Configuration["AgentSettings:BackendUrl"] ?? "https://localhost:7257";
    client.BaseAddress = new Uri(backendUrl);
})
.AddHttpMessageHandler<JwtAuthHandler>()
.AddStandardResilienceHandler(options =>
{
    //..TODO
    // 1. הגדרת ה-Timeout הכולל של כל הניסיונות (למשל, דקה אחת)
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(500);

    // 2. הגדרת Timeout לניסיון בודד (אופציונלי, במידה והשרת מגיב לאט)
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
    // הגדרת ה-Circuit Breaker בצורה מפורשת
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5); // 300 שניות (גדול מ-180)
    options.CircuitBreaker.FailureRatio = 0.5; // פתיחת המפסק ב-50% כשלים
    options.CircuitBreaker.MinimumThroughput = 5; // מינימום בקשות כדי להפעיל הגנה
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

    // הגדרות מותאמות אישית לפוליסת ה-Retry האוטומטית
    options.Retry.MaxRetryAttempts = 3;                  // 3 ניסיונות חוזרים במידה והשרת המרכזי נפל
    options.Retry.Delay = TimeSpan.FromSeconds(2);        // המתנה של 2 שניות בין ניסיון לניסיון
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential; // הגדלה אקספוננציאלית של זמן ההמתנה
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
});

// הדפסת לוג יפה המציינת שה-Agent עלה בהצלחה
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=================================================");
logger.LogInformation("Field Event Management Agent is up and running!");
logger.LogInformation("=================================================");

app.Run();