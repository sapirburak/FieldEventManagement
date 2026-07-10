namespace FieldEventManagement.Application.Interfaces
{
    /// <summary>
    /// מגדיר את החוזה (Contract) לשירותי התראות בזמן אמת.
    /// שימוש בממשק זה מאפשר למערכת לעבור מ-SignalR לכל פתרון Push אחר (כגון Firebase/Azure Push)
    /// ללא צורך בשינוי הלוגיקה העסקית בשכבת ה-Application.
    /// </summary>
    public interface IRealTimeNotificationService
    {
        /// <summary>
        /// משדר התראה לסדרנים המחוברים למערכת אודות אירוע חדש שהגיע מה-Agent.
        /// </summary>
        Task NotifyDispatcherOfNewEventAsync(Guid eventId, string title, string location);

        /// <summary>
        /// משדר לסדרנים שסטטוס אירוע קיים השתנה על-ידי טכנאי.
        /// נדרש כאשר טכנאי מעדכן סטטוס (לדוגמה: Assigned → InProgress).
        /// </summary>
        Task NotifySchedulerOfStatusUpdateAsync(Guid eventId, string newStatus, string technicianId);

        /// <summary>
        /// משדר לסדרנים שטכנאי שלח הערה על אירוע פעיל.
        /// </summary>
        Task NotifySchedulerOfNoteAsync(Guid eventId, string note, string technicianId);

        /// <summary>
        /// משדר לטכנאי ספציפי שאירוע חדש הוקצה אליו.
        /// שולח לפי ConnectionId – לא לקבוצה – כי ההודעה מיועדת לאדם אחד בלבד.
        /// TODO: לממש לאחר הוספת ניהול ConnectionId לכל טכנאי.
        /// </summary>
        Task NotifyTechnicianOfAssignmentAsync(Guid eventId, string technicianId, string title);
    }
}