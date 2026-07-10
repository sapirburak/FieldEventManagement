
   // Services/AgentBackgroundWorker.cs
using FieldEventManagement.Agent.Models;
using System.Net;

namespace FieldEventManagement.Agent.Services;

/// <summary>
/// מנוע רקע עצמאי (Hosted Service) המנהל את מחזור החיים של שליחת האירועים ל-Backend.
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

        // פרמטר 1: כל כמה שעות לבדוק אם יש צורך בניקוי/מחיקה של רשומות ישנות.
        // לדוגמה: 24 => בדיקה פעם ביום.
        var cleanupIntervalHours = configuration.GetValue<int>("AgentSettings:CleanupIntervalHours", 24);

        // פרמטר 2: לאחר כמה שעות ממועד היצירה, למחוק רשומות Error.
        // לדוגמה: 168 => מחיקה לאחר 7 ימים.
        var deleteErrorAfterHours = configuration.GetValue<int>("AgentSettings:DeleteErrorAfterHours", 24 * 7);

        // פרמטר 3: לאחר כמה שעות ממועד היצירה, למחוק רשומות Completed.
        // לדוגמה: 24 => מחיקה לאחר יום.
        var deleteCompletedAfterHours = configuration.GetValue<int>("AgentSettings:DeleteCompletedAfterHours", 24);

        _cleanupInterval = TimeSpan.FromHours(Math.Max(1, cleanupIntervalHours));
        _errorRetentionPeriod = TimeSpan.FromHours(Math.Max(1, deleteErrorAfterHours));
        _completedRetentionPeriod = TimeSpan.FromHours(Math.Max(1, deleteCompletedAfterHours));
    }

    /// <summary>
    /// מתודת הליבה - מאזינה לצינור האירועים ומזניקה עיבוד לכל אירוע שנשלף.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("[Engine] Agent Background Worker initialized and listening to the internal channel.");

        var cleanupTask = RunCleanupLoopAsync(stoppingToken);

        // לולאה אסינכרונית לא חוסמת על פני ה-Channel
        await foreach (var wrappedEvent in _eventChannel.ReadAllEventsAsync(stoppingToken))
        {
            _logger.LogInformation("[Engine] Processing event '{Title}' from queue.", wrappedEvent.Data.Title);

            // העברת הטיפול באירוע הבודד למתודה ייעודית
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
    /// מנהלת את לולאת הניסיונות החוזרים עבור אירוע ספציפי עם Exponential Backoff.
    /// כל כשל רצוף מגדיל את זמן ההמתנה לפי RetryDelays. הצלחה מאפסת את הספירה.
    ///
    /// מחזור החיים של האירוע בתוך הפונקציה:
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
    ///  │    │ הצלחה?            │ כשל?                     │
    ///  │    ▼                   ▼                           │
    ///  │  isResolved=true   attemptIndex++                  │
    ///  │  attemptIndex=0    delay = RetryDelays[min(idx,3)] │
    ///  │  יציאה מהלולאה    await Task.Delay(delay)         │
    ///  │                    חזרה לתחילת הלולאה             │
    ///  └─────────────────────────────────────────────────────┘
    ///
    /// לוח זמנים:
    ///   כשל 1 → 10s | כשל 2 → 30s | כשל 3 → 60s | כשל 4+ → 5min (גג)
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
    // מתוך מתודת TrySendEventAsync בתוך AgentBackgroundWorker.cs

    private async Task<bool> TrySendEventAsync(WrappedEvent wrappedEvent, CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var backendClient = scope.ServiceProvider.GetRequiredService<IBackendClient>();

        // מעבירים את ה-wrappedEvent המלא (כולל ה-Id מ-SQLite) כדי לשמר Idempotency.
        var response = await backendClient.SendEventToBackendAsync(wrappedEvent, stoppingToken);

        // תרחיש א': הצלחה מלאה
        if (response.IsSuccess)
        {
            _eventChannel.ConfirmDelivery(wrappedEvent.Id);
            return true;
        }

        // תרחיש ב': הודעת רעל / הודעה שגויה מבחינה לוגית (השרת עובד אך דחה את התוכן)
        if (response.StatusCode == HttpStatusCode.BadRequest ||
            response.StatusCode == HttpStatusCode.UnprocessableEntity)
        {
            _logger.LogError("[Engine] Error response detected (Status {Code})! Marking event {Id} as Error.", response.StatusCode, wrappedEvent.Id);

            _eventChannel.MoveToError(wrappedEvent.Id);
            return true; // נפתר (הוסר מהתור), ניתן להמשיך להודעה הבאה
        }

        // תרחיש ג': שגיאות שרת זמניות (למשל 500 Internal Server Error או 429 Too Many Requests)
        _logger.LogWarning("[Engine] Server returned temporary error {Code}. Suspecting transient issue. Will retry.", response.StatusCode);
        return false; // לא נפתר, הלולאה תמתין ותנסה שוב

    }
}