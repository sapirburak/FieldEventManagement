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
        /// משדר התראה לסדרנים המחוברים למערכת אודות אירוע חדש.
        /// </summary>
        Task NotifyDispatcherOfNewEventAsync(Guid eventId, string title, string location);
    }
}