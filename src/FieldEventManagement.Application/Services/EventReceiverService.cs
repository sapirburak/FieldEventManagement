using System;
using System.Threading.Tasks;
using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Core.Entities;

namespace FieldEventManagement.Application.Services;
/// <summary>
/// אחראית על תזמור (Orchestration) התהליך העסקי של קליטת אירוע חדש מה-Agent.
/// המחלקה מיישמת את דפוס ה-Use Case בתוך שכבת ה-Application, 
/// תוך הפרדה מוחלטת מהמימוש הטכנולוגי של מסד הנתונים או אמצעי התקשורת.
/// </summary>
public class EventReceiverService
{
    private readonly IFieldEventRepository _repository;
    private readonly IRealTimeNotificationService _notificationService;

    /// <param name="repository">ממשק לגישה לנתונים (Persistence Ignorance).</param>
    /// <param name="notificationService">ממשק להפצת התראות בזמן אמת.</param>
    public EventReceiverService(IFieldEventRepository repository, IRealTimeNotificationService notificationService)
    {
        _repository = repository;
        _notificationService = notificationService;
    }

    /// <summary>
    /// מעבדת אירוע נכנס. המערכת מבטיחה טיפול אטומי (Idempotent) באמצעות בדיקת כפילויות,
    /// ומפעילה את ה-State Machine של ה-Domain כדי לשמור על תקינות המצב העסקי.
    /// </summary>
    /// <param name="incomingEvent">האובייקט שעבר עטיפה מה-Agent.</param>
    /// <exception cref="ArgumentException">נזרקת כאשר הנתונים חסרים, גורמת ל-API להחזיר 422.</exception>
    public async Task<ProcessResult> ProcessIncomingEventAsync(WrappedEvent incomingEvent)
    {
        // 1. הגנה מפני הודעות רעל (ולידציה ברמת ה-Application)
        if (incomingEvent == null || incomingEvent.Data == null || string.IsNullOrWhiteSpace(incomingEvent.Data.Title))
        {
            // שגיאה זו תתורגם ב-API ל-422 Unprocessable Entity, מה שיגרום ל-Agent שלך להעביר את ההודעה ל-Poison Queue
            throw new ArgumentException("מבנה האירוע אינו תקין או שחסרים נתונים קריטיים.");
        }

        // 2. מנגנון Idempotency (מניעת כפילויות בגלל ה-Retry של ה-Agent)
        var existingEvent = await _repository.ExistsAsync(incomingEvent.Id);
        // מקרה א': האירוע קיים )
        if (existingEvent)
        {
            return new ProcessResult("Ignored", "Event already in advanced status. Update ignored.");
        }
        
        // מקרה ב': האירוע לא קיים (יצירה)
        // 3. הפעלת לוגיקת ה-Domain (יצירת הישות הטהורה עם ה-State Machine שלה)
        // ברגע זה, נוצרת אוטומטית שורת ההיסטוריה הראשונה בתוך ה-Core!
        var fieldEvent = FieldEvent.Create(
            incomingEvent.Id,
            incomingEvent.Data.Title,
            incomingEvent.Data.Description,
            incomingEvent.Data.Source,
            incomingEvent.Data.Location
        );
        // 4. שימוש ב-Repository כדי לדחוף ל-DB
        await _repository.AddAsync(fieldEvent);
        await _repository.SaveChangesAsync();
        // 5. הפעלת שירות ההתראות האבסטרקטי כדי להקפיץ לסדרן הודעה ב-UI בזמן אמת
        await _notificationService.NotifyDispatcherOfNewEventAsync(
            fieldEvent.Id,
            fieldEvent.Title,
            fieldEvent.Location
        );
        return new ProcessResult("Created", "New event created successfully.");
            }
}
