
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
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(10);

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
    /// מנהלת את לולאת הניסיונות החוזרים עבור אירוע ספציפי, עד שהוא נמסר או מסווג כהודעה שגויה.
    /// </summary>
    private async Task ProcessSingleEventWithRetryAsync(WrappedEvent wrappedEvent, CancellationToken stoppingToken)
    {
        bool isResolved = false;

        while (!isResolved && !stoppingToken.IsCancellationRequested)
        {
            try
            {
                // ניסיון שליחה בודד וקבלת החלטה ארכיטקטונית
                isResolved = await TrySendEventAsync(wrappedEvent, stoppingToken);
            }
            catch (HttpRequestException ex)
            {
                // כשל תקשורת קשיח - הרשת נפלה או השרת כבוי לחלוטין. מקפיאים את התור מבלי לקדם אותו.
                _logger.LogWarning("[Engine] Network unavailable (Server down). Freezing queue. Retrying in {Seconds}s... Details: {Msg}", RetryDelay.TotalSeconds, ex.Message);
            }
            catch (Exception ex)
            {
                // הגנה מפני קריסות פנימיות לא צפויות
                _logger.LogError(ex, "[Engine] Unexpected internal error processing event {Id}. Retrying in {Seconds}s...", wrappedEvent.Id, RetryDelay.TotalSeconds);
            }

            // אם האירוע לא נפתר (עקב שגיאת רשת או שרת), ממתינים לפני הניסיון הבא
            if (!isResolved)
            {
                await Task.Delay(RetryDelay, stoppingToken);
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

        // קריאה לפונקציה המקורית, שמחזירה כעת תשובה מפורטת
        var response = await backendClient.SendEventToBackendAsync(wrappedEvent.Data, stoppingToken);

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