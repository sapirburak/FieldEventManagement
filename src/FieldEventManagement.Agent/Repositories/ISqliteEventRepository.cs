using FieldEventManagement.Agent.Models;

namespace FieldEventManagement.Agent.Repositories;

/// <summary>
/// ממשק עבור מאגר האירועים המקומי המבוסס על SQLite.
/// מספק שכבת הפרדה בין לוגיקת התור של ה-Agent לבין השמירה הפיזית של האירועים בדיסק.
/// </summary>
public interface ISqliteEventRepository
{
    /// <summary>
    /// מאתחל את מסד הנתונים ואת הסכימה הדרושה אם הם עדיין לא קיימים.
    /// </summary>
    void Initialize();

    /// <summary>
    /// מוסיף אירוע חדש לטבלת האירועים המקומיים עם סטטוס Pending.
    /// </summary>
    /// <param name="wrappedEvent">האובייקט המלא עם המזהה והנתונים.</param>
    void AddEvent(WrappedEvent wrappedEvent);

    /// <summary>
    /// מסמן אירוע כשנמסר בהצלחה ל-Backend ומוחק אותו מהטבלה המקומית.
    /// </summary>
    /// <param name="eventId">המזהה הייחודי של האירוע.</param>
    void ConfirmDelivery(Guid eventId);

    /// <summary>
    /// משנה את סטטוס האירוע ל-Error בעקבות כשל לוגי/ארכיטקטוני קבוע.
    /// </summary>
    /// <param name="eventId">המזהה הייחודי של האירוע.</param>
    void MoveToError(Guid eventId);

    /// <summary>
    /// מחזיר את כל האירועים שנשארו ממתינים לשילוח מהטבלה המקומית.
    /// </summary>
    /// <returns>רשימה של אירועים לא מסופקים שניתן לשחזר לתור הזיכרון.</returns>
    IReadOnlyList<WrappedEvent> GetPendingEvents();

    /// <summary>
    /// מוחק רשומות עם סטטוס Error או Completed שנמצאות מעל פרקי הזמן המוגדרים עבור כל סטטוס.
    /// </summary>
    /// <param name="errorRetentionPeriod">פרק הזמן המינימלי לשמירת רשומות Error לפני מחיקה.</param>
    /// <param name="completedRetentionPeriod">פרק הזמן המינימלי לשמירת רשומות Completed לפני מחיקה.</param>
    /// <returns>מספר הרשומות שנמחקו.</returns>
    int DeleteExpiredEvents(TimeSpan errorRetentionPeriod, TimeSpan completedRetentionPeriod);
}
