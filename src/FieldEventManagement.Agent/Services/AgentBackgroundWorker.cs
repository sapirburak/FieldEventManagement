
   // Services/AgentBackgroundWorker.cs
using FieldEventManagement.Agent.Models;
using System.Net;

namespace FieldEventManagement.Agent.Services;

/// <summary>
/// Autonomous background engine (Hosted Service) that manages the lifecycle of event delivery to the Backend.
/// </summary>
public class AgentBackgroundWorker : BackgroundService
{
    private readonly EventChannel _eventChannel;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AgentBackgroundWorker> _logger;
    private readonly TimeSpan _cleanupInterval;
    private readonly TimeSpan _errorRetentionPeriod;
    private readonly TimeSpan _completedRetentionPeriod;
    // Exponential backoff schedule: each consecutive failure moves to the next slot.
    // Trade-off: recovery detection is slower (up to 5 min) but the dead backend gets
    // far fewer pointless requests and logs stay readable.
    // A successful delivery resets the attempt counter to zero.
    private static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromSeconds(10),  // attempt 1
        TimeSpan.FromSeconds(30),  // attempt 2
        TimeSpan.FromSeconds(60),  // attempt 3
        TimeSpan.FromMinutes(5)    // attempt 4+ (cap)
    ];

    public AgentBackgroundWorker(
        EventChannel eventChannel,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<AgentBackgroundWorker> logger)
    {
        _eventChannel = eventChannel;
        _serviceProvider = serviceProvider;
        _logger = logger;

        // Parameter 1: how many hours between checks for old records that need cleanup/deletion.
        // Example: 24 => check once per day.
        var cleanupIntervalHours = configuration.GetValue<int>("AgentSettings:CleanupIntervalHours", 24);

        // Parameter 2: how many hours after creation to delete Error records.
        // Example: 168 => delete after 7 days.
        var deleteErrorAfterHours = configuration.GetValue<int>("AgentSettings:DeleteErrorAfterHours", 24 * 7);

        // Parameter 3: how many hours after creation to delete Completed records.
        // Example: 24 => delete after one day.
        var deleteCompletedAfterHours = configuration.GetValue<int>("AgentSettings:DeleteCompletedAfterHours", 24);

        _cleanupInterval = TimeSpan.FromHours(Math.Max(1, cleanupIntervalHours));
        _errorRetentionPeriod = TimeSpan.FromHours(Math.Max(1, deleteErrorAfterHours));
        _completedRetentionPeriod = TimeSpan.FromHours(Math.Max(1, deleteCompletedAfterHours));
    }

    /// <summary>
    /// Core method – listens to the event pipeline and fires processing for each dequeued event.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Engine] Agent Background Worker initialized and listening to the internal channel.");

        var cleanupTask = RunCleanupLoopAsync(stoppingToken);

        // Non-blocking async loop over the Channel
        await foreach (var wrappedEvent in _eventChannel.ReadAllEventsAsync(stoppingToken))
        {
            _logger.LogInformation("[Engine] Processing event '{Title}' from queue.", wrappedEvent.Data.Title);

            // Delegate processing of the individual event to a dedicated method
            await ProcessSingleEventWithRetryAsync(wrappedEvent, stoppingToken);
        }

        await cleanupTask;
    }

    private async Task RunCleanupLoopAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_cleanupInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var deletedCount = _eventChannel.DeleteExpiredEvents(_errorRetentionPeriod, _completedRetentionPeriod);
                if (deletedCount > 0)
                {
                    _logger.LogInformation("[Engine] Cleanup removed {Count} expired Error/Completed rows from the local database.", deletedCount);
                }
                else
                {
                    _logger.LogInformation("[Engine] Cleanup completed. No expired Error/Completed rows were found.");
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Engine] Weekly cleanup failed.");
            }
        }
    }

    /// <summary>
    /// Manages the retry loop for a specific event with Exponential Backoff.
    /// Each consecutive failure increases the wait time according to RetryDelays. A success resets the counter.
    ///
    /// Event lifecycle inside the function:
    ///
    ///  ┌─────────────────────────────────────────────────────┐
    ///  │              ProcessSingleEventWithRetryAsync        │
    ///  │                                                     │
    ///  │   attemptIndex = 0                                  │
    ///  │         │                                           │
    ///  │         ▼                                           │
    ///  │   ┌─────────────┐                                   │
    ///  │   │TrySendEvent │                                   │
    ///  │   └──────┬──────┘                                   │
    ///  │          │                                          │
    ///  │    ┌─────┴──────────────┐                          │
    ///  │    │ success?          │ failure?                  │
    ///  │    ▼                   ▼                           │
    ///  │  isResolved=true   attemptIndex++                  │
    ///  │  attemptIndex=0    delay = RetryDelays[min(idx,3)] │
    ///  │  exit loop         await Task.Delay(delay)         │
    ///  │                    loop back to start              │
    ///  └─────────────────────────────────────────────────────┘
    ///
    /// Timing schedule:
    ///   failure 1 → 10s | failure 2 → 30s | failure 3 → 60s | failure 4+ → 5min (cap)
    /// </summary>
    private async Task ProcessSingleEventWithRetryAsync(WrappedEvent wrappedEvent, CancellationToken stoppingToken)
    {
        bool isResolved = false;
        int attemptIndex = 0;

        while (!isResolved && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                isResolved = await TrySendEventAsync(wrappedEvent, stoppingToken);

                if (isResolved)
                    attemptIndex = 0; // reset backoff on success so the next event starts fresh
            }
            catch (HttpRequestException ex)
            {
                var delay = RetryDelays[Math.Min(attemptIndex, RetryDelays.Length - 1)];
                _logger.LogWarning(
                    "[Engine] Network unavailable (attempt #{Attempt}). Retrying in {Seconds}s. Details: {Msg}",
                    attemptIndex + 1, delay.TotalSeconds, ex.Message);
            }
            catch (Exception ex)
            {
                var delay = RetryDelays[Math.Min(attemptIndex, RetryDelays.Length - 1)];
                _logger.LogError(ex,
                    "[Engine] Unexpected error on event {Id} (attempt #{Attempt}). Retrying in {Seconds}s.",
                    wrappedEvent.Id, attemptIndex + 1, delay.TotalSeconds);
            }

            if (!isResolved)
            {
                var delay = RetryDelays[Math.Min(attemptIndex, RetryDelays.Length - 1)];
                attemptIndex++;
                await Task.Delay(delay, stoppingToken);
            }
        }
    }

    /// <summary>
    /// מבצעת את פניית ה-HTTP בפועל ומנתחת את הסטטוס החוזר (הצלחה, שגיאת רשת, או הודעת שגיאה).
    /// מחזירה true אם האירוע "נפתר" (נשלח או סומן כ-Error) וניתן להמשיך הלאה, או false אם יש לנסות שוב.
    /// </summary>
    // From the TrySendEventAsync method inside AgentBackgroundWorker.cs

    private async Task<bool> TrySendEventAsync(WrappedEvent wrappedEvent, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var backendClient = scope.ServiceProvider.GetRequiredService<IBackendClient>();

        // Pass the full wrappedEvent (including the SQLite Id) to preserve Idempotency.
        var response = await backendClient.SendEventToBackendAsync(wrappedEvent, stoppingToken);

        // Scenario A: full success
        if (response.IsSuccess)
        {
            _eventChannel.ConfirmDelivery(wrappedEvent.Id);
            return true;
        }

        // Scenario B: poison message / logically invalid message (server is working but rejected the content)
        if (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogError("[Engine] Error response detected (Status {Code})! Marking event {Id} as Error.", response.StatusCode, wrappedEvent.Id);

            _eventChannel.MoveToError(wrappedEvent.Id);
            return true; // resolved (removed from queue), can proceed to the next message
        }

        // Scenario C: transient server errors (e.g. 500 Internal Server Error or 429 Too Many Requests)
        _logger.LogWarning("[Engine] Server returned temporary error {Code}. Suspecting transient issue. Will retry.", response.StatusCode);
        return false; // not resolved, the loop will wait and retry

    }
}