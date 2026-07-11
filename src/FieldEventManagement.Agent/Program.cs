// Program.cs
using FieldEventManagement.Agent.Models;
using FieldEventManagement.Agent.Repositories;
using FieldEventManagement.Agent.Services;
using Microsoft.AspNetCore.Mvc;


var builder = WebApplication.CreateBuilder(args);

// --- 1. Register system services for Dependency Injection ---

// Register the SQLite repository and the Agent's in-memory queue as Singletons (one instance per application)
builder.Services.AddSingleton<ISqliteEventRepository, SqliteEventRepository>();
builder.Services.AddSingleton<EventChannel>();

// Register the background service that runs independently and listens to the queue
builder.Services.AddHostedService<AgentBackgroundWorker>();

builder.Services.AddTransient<JwtAuthHandler>();

// InsecureClient is intended only for local development environments without a valid SSL certificate.
// In production, the regular handler (with SSL validation) is used automatically.
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

// Register the HttpClient together with the built-in .NET Resilience mechanism (Polly)
builder.Services.AddHttpClient<IBackendClient, BackendClient>(client =>
{
    var backendUrl = builder.Configuration["AgentSettings:BackendUrl"]
        ?? throw new InvalidOperationException("AgentSettings:BackendUrl is not configured.");
    client.BaseAddress = new Uri(backendUrl);
})
.AddHttpMessageHandler<JwtAuthHandler>()
.AddStandardResilienceHandler(options =>
{
    // Total timeout for all attempts (including retries)
    options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(120);

    // Timeout for a single HTTP attempt
    options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(30);

    // Circuit Breaker: opens after 50% failures from at least 5 requests, pauses for 30 seconds
    options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    options.CircuitBreaker.FailureRatio = 0.5;
    options.CircuitBreaker.MinimumThroughput = 5;
    options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

    // Retry: 3 attempts with Exponential Backoff (also managed by AgentBackgroundWorker)
    options.Retry.MaxRetryAttempts = 3;
    options.Retry.Delay = TimeSpan.FromSeconds(2);
    options.Retry.BackoffType = Polly.DelayBackoffType.Exponential;
});

var app = builder.Build();

app.UseHttpsRedirection();

// --- 2. Expose the Minimal API Endpoint for external sources ---

app.MapPost("/api/agent/events", async (
    [FromBody] FieldEventDto fieldEvent,
    [FromHeader(Name = "X-Api-Key")] string? apiKey,
    [FromServices] EventChannel channel,
    [FromServices] IConfiguration configuration,
    CancellationToken cancellationToken) =>
{
    try
    {
        // Security: validate the API key received in the request header
        var expectedKey = configuration["AgentSettings:ExpectedApiKey"];
        if (string.IsNullOrEmpty(apiKey) || apiKey != expectedKey)
        {
            return Results.Unauthorized();
        }

        // Basic validation: ensure the core fields are not empty
        if (string.IsNullOrWhiteSpace(fieldEvent.Title) || string.IsNullOrWhiteSpace(fieldEvent.Source))
        {
            return Results.BadRequest("Title and Source are strictly required.");
        }

        // Fast async push to local disk and the in-memory queue
        await channel.AddEventAsync(fieldEvent, cancellationToken);

        // Return 202 Accepted. The external source is released immediately; the BackgroundWorker handles async delivery
        return Results.Accepted();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        logger.LogError(ex, "Error processing event"); // Check the terminal for the exception details
        return Results.Problem(ex.Message);
    }
});

// Log a startup banner indicating the Agent started successfully
var logger = app.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("=================================================");
logger.LogInformation("Field Event Management Agent is up and running!");
logger.LogInformation("=================================================");

app.Run();
