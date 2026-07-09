using FieldEventManagement.Agent.Models;
using FieldEventManagement.Agent.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace FieldEventManagement.Agent.Services;

/// <summary>
/// מנהל את ערוץ האירועים המקומי של ה-Agent.
/// משמש כרכיב תיווך (Buffer) חכם המשלב תור מהיר בזיכרון (In-Memory Channel)
/// יחד עם מנגנון עמידות חסין אובדן מידע בדיסק (Disk Persistence).
/// </summary>
public class EventChannel
{
    /// <summary>
    /// צינור אסינכרוני מובנה ב-.NET לניהול תור ההודעות בזיכרון בצורה בטוחה (Thread-Safe).
    /// </summary>
    private readonly Channel<WrappedEvent> _channel;

    /// <summary>
    /// מאגר האירועים המקומי המבוסס על SQLite.
    /// </summary>
    private readonly ISqliteEventRepository _repository;

    /// <summary>
    /// רכיב הרישום של המערכת לתיעוד אירועים, אזהרות ושגיאות בזמן ריצה.
    /// </summary>
    private readonly ILogger<EventChannel> _logger;

    /// <summary>
    /// מאתחל מופע חדש של מחלקת <see cref="EventChannel"/>.
    /// מקים את תיקיות הדיסק הנדרשות, מגדיר את אופטימיזציית הצינור בזיכרון ומפעיל שחזור קבצים אוטומטי.
    /// </summary>
    /// <param name="configuration">ממשק גישה להגדרות האפליקציה (appsettings.json).</param>
    /// <param name="repository">מאגר האירועים המקומי עבור שמירה ושחזור.</param>
    /// <param name="logger">רכיב רישום הלוגים הייעודי של המחלקה.</param>
    public EventChannel(IConfiguration configuration, ISqliteEventRepository repository, ILogger<EventChannel> logger)
    {
        _logger = logger;
        _repository = repository;

        int capacity = configuration.GetValue<int>("AgentSettings:ChannelCapacity", 10000);
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<WrappedEvent>(options);

        _repository.Initialize();
        RecoverLocalFiles();
    }

    /// <summary>
    /// מסמן אירוע שנכשלו ניסיונות השליחה שלו כ-Error,
    /// לצורך בידוד ותחקור עתידי, ומסיר אותו מתזרים המערכת הנוכחי בזיכרון.
    /// </summary>
    /// <param name="eventId">המזהה הייחודי (Guid) של האירוע שנכשל.</param>
    public void MoveToError(Guid eventId)
    {
        try
        {
            _repository.MoveToError(eventId);
            _logger.LogWarning("[Storage] Event {Id} marked as Error in the SQLite repository.", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Critical Error moving event {Id} to Error state in SQLite.", eventId);
        }
    }

    /// <summary>
    /// מוסיף אירוע חדש למערכת בתצורת Store-and-Forward.
    /// </summary>
    /// <param name="dto">אובייקט הנתונים הגולמי שהתקבל מהשטח.</param>
    /// <param name="cancellationToken">אסימון לביטול הפעולה האסינכרונית במידת הצורך.</param>
    /// <returns>ערך המייצג את השלמת המשימה האסינכרונית ללא הקצאת זיכרון מיותרת (ValueTask).</returns>
    public async ValueTask AddEventAsync(FieldEventDto dto, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var wrappedEvent = new WrappedEvent(id, dto);

        try
        {
            _repository.AddEvent(wrappedEvent);
            _logger.LogInformation("[Storage] Event persisted to SQLite database. ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Failed to persist event {Id} to SQLite.", id);
            throw;
        }

        await _channel.Writer.WriteAsync(wrappedEvent, cancellationToken);
        _logger.LogInformation("[Storage] Event enqueued in memory channel. ID: {Id}", id);
    }

    /// <summary>
    /// מוחק את הרשומה לאחר אישור מסירה ל-Backend.
    /// </summary>
    /// <param name="id">המזהה הייחודי של האירוע שסופק בהצלחה.</param>
    public void ConfirmDelivery(Guid id)
    {
        try
        {
            _repository.ConfirmDelivery(id);
            _logger.LogInformation("[Storage] Delivery confirmed for event {Id}. SQLite row removed.", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Failed to confirm delivery for event {Id} in SQLite.", id);
        }
    }

    /// <summary>
    /// מוחק את כל הרשומות שנמצאות במצב Error מהמאגר המקומי.
    /// </summary>
    /// <returns>מספר הרשומות שנמחקו.</returns>
    public int DeleteErrorEvents()
    {
        try
        {
            var deletedCount = _repository.DeleteErrorEvents();
            _logger.LogInformation("[Storage] Cleanup removed {Count} Error rows from SQLite.", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Failed to cleanup Error rows from SQLite.");
            return 0;
        }
    }

    /// <summary>
    /// חושף זרם קריאה אסינכרוני מתמשך המאפשר ל-BackgroundWorker לצרוך אירועים מהתור.
    /// </summary>
    /// <param name="cancellationToken">אסימון לביטול הפעולה האסינכרונית במידת הצורך.</param>
    /// <returns>זרם נתונים אסינכרוני מסוג <see cref="IAsyncEnumerable{T}"/>.</returns>
    public IAsyncEnumerable<WrappedEvent> ReadAllEventsAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// משחזר את האירועים הממתינים מהמאגר המקומי לתור הזיכרון בעת עליית האפליקציה.
    /// </summary>
    private void RecoverLocalFiles()
    {
        try
        {
            var pendingEvents = _repository.GetPendingEvents();
            if (pendingEvents.Count > 0)
            {
                _logger.LogWarning("[Recovery] Found {Count} undelivered events in SQLite. Recovering into memory channel...", pendingEvents.Count);
            }

            int recoveredCount = 0;
            foreach (var recoveredEvent in pendingEvents)
            {
                if (_channel.Writer.TryWrite(recoveredEvent))
                {
                    recoveredCount++;
                    _logger.LogInformation("[Recovery] Recovered event {Id} back into memory queue.", recoveredEvent.Id);
                }
                else
                {
                    _logger.LogWarning("[Recovery] Failed to enqueue recovered event {Id} into the in-memory channel.", recoveredEvent.Id);
                }
            }

            _logger.LogInformation("[Recovery] Recovery completed. Recovered {RecoveredCount} of {TotalCount} pending events.", recoveredCount, pendingEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Recovery] Failed to recover pending events from SQLite.");
        }
    }
}
