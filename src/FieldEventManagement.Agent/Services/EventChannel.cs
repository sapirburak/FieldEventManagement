using FieldEventManagement.Agent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
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
    /// הנתיב המוחלט לתיקיית העבודה הראשי בדיסק שבה נשמרים האירועים הממתינים לשילוח.
    /// </summary>
    private readonly string _storageDirectory;

    /// <summary>
    /// הנתיב המוחלט לתיקיית הודעות הרעל (Poison) שבה מבודדים קבצים שנכשלו לצמיתות.
    /// </summary>
    private readonly string _poisonDirectory;

    /// <summary>
    /// רכיב הרישום של המערכת לתיעוד אירועים, אזהרות ושגיאות בזמן ריצה.
    /// </summary>
    private readonly ILogger<EventChannel> _logger;

    /// <summary>
    /// מאתחל מופע חדש של מחלקת <see cref="EventChannel"/>.
    /// מקים את תיקיות הדיסק הנדרשות, מגדיר את אופטימיזציית הצינור בזיכרון ומפעיל שחזור קבצים אוטומטי.
    /// </summary>
    /// <param name="configuration">ממשק גישה להגדרות האפליקציה (appsettings.json).</param>
    /// <param name="env">ממשק המספק מידע על סביבת הריצה ונתיבי המערכת של השרת.</param>
    /// <param name="logger">רכיב רישום הלוגים הייעודי של המחלקה.</param>
    public EventChannel(IConfiguration configuration, IHostEnvironment env, ILogger<EventChannel> logger)
    {
        _logger = logger;

        // יצירת תיקייה מקומית בשם "LocalQueue" בתוך תיקיית הריצה של השרת
        _storageDirectory = Path.Combine(env.ContentRootPath, "LocalQueue");
        if (!Directory.Exists(_storageDirectory))
        {
            Directory.CreateDirectory(_storageDirectory);
        }

        // יצירת תיקיית שגויים נפרדת ("LocalPoisonQueue") במקביל, כדי למנוע ערבוב קבצים וטעינה חוזרת
        _poisonDirectory = Path.Combine(env.ContentRootPath, "LocalPoisonQueue");
        if (!Directory.Exists(_poisonDirectory))
        {
            Directory.CreateDirectory(_poisonDirectory);
        }

        // הגדרת תור מוגבל בזיכרון כדי למנוע הצפת RAM (Bounded Channel)
        int capacity = configuration.GetValue<int>("AgentSettings:ChannelCapacity", 10000);
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait, // אם התור מלא, הבקשות הבאות ימתינו אסינכרונית (Backpressure)
            SingleReader = true,                   // אופטימיזציה: רק ה-BackgroundWorker קורא מהתור (צרכן יחיד)
            SingleWriter = false                   // יצרנים רבים: מספר רב של API Endpoints יכולים לכתוב במקביל
        };

        _channel = Channel.CreateBounded<WrappedEvent>(options);

        // בעליית ה-Agent: שחזור אוטומטי של קבצים שלא נשלחו בהצלחה לפני קריסה/כיבוי
        RecoverLocalFiles();
    }

    /// <summary>
    /// מעביר קובץ אירוע שנכשלו ניסיונות השליחה שלו לתיקיית הודעות רעל (Poison) 
    /// לצורך בידוד ותחקור עתידי, ומסיר אותו כליל מתזרים המערכת הנוכחי בזיכרון.
    /// </summary>
    /// <param name="eventId">המזהה הייחודי (Guid) של האירוע שנכשל.</param>
    public void MoveToPoison(Guid eventId)
    {
        string sourceFile = Path.Combine(_storageDirectory, $"{eventId}.json");
        string targetFile = Path.Combine(_poisonDirectory, $"{eventId}.json");

        try
        {
            if (File.Exists(sourceFile))
            {
                // העברת הקובץ פיזית בדיסק לתיקיית הודעות הרעל עם דריסה במקרה קצה (true)
                File.Move(sourceFile, targetFile, overwrite: true);
                _logger.LogWarning("[Storage] Event {Id} physically moved to Poison folder due to persistent failures.", eventId);
            }
            else
            {
                _logger.LogWarning("[Storage] Warning: Tried to move event {Id} to Poison, but source file was not found.", eventId);
            }
        }
        catch (Exception ex)
        {
            // הגנה מפני שגיאות מערכת קבצים (דיסק נעול, חוסר בהרשאות כתיבה וכד')
            _logger.LogError(ex, "[Storage] Critical Error moving event {Id} to Poison.", eventId);
        }
    }

    /// <summary>
    /// מוסיף אירוע חדש למערכת בתצורת Store-and-Forward:
    /// קודם כל מבצע שמירה פיזית קשיחה לדיסק (Persistence) ולאחר מכן דוחף את האובייקט לערוץ המהיר בזיכרון.
    /// </summary>
    /// <param name="dto">אובייקט הנתונים הגולמי שהתקבל מהשטח.</param>
    /// <param name="cancellationToken">אסימון לביטול הפעולה האסינכרונית במידת הצורך.</param>
    /// <returns>ערך המייצג את השלמת המשימה האסינכרונית ללא הקצאת זיכרון מיותרת (ValueTask).</returns>
    public async ValueTask AddEventAsync(FieldEventDto dto, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var wrappedEvent = new WrappedEvent(id, dto);
        // 1. הגדרת האפשרויות כך שלא יקודד תווים מחוץ ל-ASCII (כלומר ישאיר עברית כעברית)
        var options = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(
            UnicodeRanges.Hebrew,
            UnicodeRanges.BasicLatin // הגרש נמצא בטווח הזה!
        ),
            WriteIndented = true
        };
        // 1. שמירה פיזית לדיסק (Persistence)
        string filePath = Path.Combine(_storageDirectory, $"{id}.json");
        string json = JsonSerializer.Serialize(wrappedEvent,options);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);
        _logger.LogInformation("[Storage] Event written to local disk backup. ID: {Id}", id);

        // 2. כתיבה לערוץ הזיכרון (In-Memory Queue)
        await _channel.Writer.WriteAsync(wrappedEvent, cancellationToken);
    }

    /// <summary>
    /// מוחק את קובץ הגיבוי המקומי מהדיסק לאחר קבלת אישור סופי (200 OK)
    /// שה-Backend המרכזי קלט ועיבד את האירוע בהצלחה.
    /// </summary>
    /// <param name="id">המזהה הייחודי של האירוע שסופק בהצלחה.</param>
    public void ConfirmDelivery(Guid id)
    {
        string filePath = Path.Combine(_storageDirectory, $"{id}.json");
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            _logger.LogInformation("[Storage] Delivery confirmed. Cleaned up local file. ID: {Id}", id);
        }
    }

    /// <summary>
    /// חושף זרם קריאה אסינכרוני מתמשך המאפשר ל-BackgroundWorker לצרוך אירועים מהתור.
    /// במידה והתור ריק, הקורא ימתין באופן אסינכרוני יעיל מבלי לחסום Thread.
    /// </summary>
    /// <param name="cancellationToken">אסימון לביטול פעולת הזרמת הנתונים.</param>
    /// <returns>זרם נתונים אסינכרוני מסוג <see cref="IAsyncEnumerable{T}"/>.</returns>
    public IAsyncEnumerable<WrappedEvent> ReadAllEventsAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// סורק את תיקיית העבודה בדיסק בעת עליית האפליקציה,
    /// ומחזיר את כל הקבצים שלא הספיקו להישלח לפני הכיבוי/הקריסה חזרה אל תור הזיכרון הראשי.
    /// </summary>
    private void RecoverLocalFiles()
    {
        var files = Directory.GetFiles(_storageDirectory, "*.json");
        if (files.Length > 0)
        {
            _logger.LogWarning("[Recovery] Found {Count} undelivered events on disk. Recovering...", files.Length);
        }

        foreach (var file in files)
        {
            try
            {
                string json = File.ReadAllText(file);
                var recoveredEvent = JsonSerializer.Deserialize<WrappedEvent>(json);
                if (recoveredEvent != null)
                {
                    // שימוש ב-TryWrite מאחר ואנו בשלב אתחול השרת והתור בזיכרון עדיין ריק לגמרי
                    _channel.Writer.TryWrite(recoveredEvent);
                    _logger.LogInformation("[Recovery] Recovered event {Id} back into memory queue.", recoveredEvent.Id);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Recovery] Failed to read recovery file: {File}", file);
            }
        }
    }
}