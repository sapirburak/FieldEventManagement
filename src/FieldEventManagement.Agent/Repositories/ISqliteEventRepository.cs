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
    /// מוחק את כל הרשומות שנמצאות במצב Error מהטבלה המקומית.
    /// </summary>
    /// <returns>מספר הרשומות שנמחקו.</returns>
    int DeleteErrorEvents();
}
