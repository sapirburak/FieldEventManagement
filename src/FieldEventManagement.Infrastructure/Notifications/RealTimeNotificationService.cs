using FieldEventManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FieldEventManagement.Infrastructure.Notifications;
/// <summary>
/// שירות לניהול תקשורת דו-כיוונית בזמן אמת מול הסדרנים.
/// מתרגם קריאות עסקיות של ה-Application לשידורי (Broadcast) SignalR.
/// </summary>
public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<EventHub> _hubContext;

    public RealTimeNotificationService(IHubContext<EventHub> hubContext) => _hubContext = hubContext;

    /// <summary>
    /// שולח לכל הסדרנים המחוברים על אירוע חדש שהגיע מה-Agent.
    /// </summary>
    public async Task NotifyDispatcherOfNewEventAsync(Guid eventId, string title, string location)
    {
        await _hubContext.Clients.Group("Schedulers").SendAsync("ReceiveNewEvent", new
        {
            eventId,
            title,
            location,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// שולח לכל הסדרנים שטכנאי עדכן סטטוס על אירוע פעיל.
    /// Angular event: "ReceiveStatusUpdate"
    /// </summary>
    public async Task NotifySchedulerOfStatusUpdateAsync(Guid eventId, string newStatus, string technicianId)
    {
        await _hubContext.Clients.Group("Schedulers").SendAsync("ReceiveStatusUpdate", new
        {
            eventId,
            newStatus,
            technicianId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// שולח לכל הסדרנים שטכנאי הוסיף הערה על אירוע.
    /// Angular event: "ReceiveNote"
    /// </summary>
    public async Task NotifySchedulerOfNoteAsync(Guid eventId, string note, string technicianId)
    {
        await _hubContext.Clients.Group("Schedulers").SendAsync("ReceiveNote", new
        {
            eventId,
            note,
            technicianId,
            timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// TODO: לממש לאחר שנוסיף ניהול ConnectionId לכל טכנאי.
    /// כרגע שולח לכל הסדרנים כ-fallback.
    /// בפרודקשן: נשלח ישירות לטכנאי לפי ConnectionId שמור.
    /// </summary>
    public async Task NotifyTechnicianOfAssignmentAsync(Guid eventId, string technicianId, string title)
    {
        // TODO: החלף בשליחה ישירה לConnectionId של הטכנאי
        await _hubContext.Clients.Group("Schedulers").SendAsync("ReceiveAssignment", new
        {
            eventId,
            technicianId,
            title,
            timestamp = DateTime.UtcNow
        });
    }
}
/// <summary>
/// ה-Hub של SignalR. מחלקה זו משמשת כנקודת הקצה לתקשורת בזמן אמת.
/// [Authorize] ברמת ה-Hub מבטיח שחיבור WebSocket מחייב JWT תקין.
/// ללא זה, כל אחד יכול להתחבר ולהאזין לאירועים.
/// </summary>
[Authorize]
public class EventHub : Hub
{
    /// <summary>
    /// מצרף את הלקוח לקבוצת הסדרנים לקבלת התראות.
    /// [Authorize(Roles = "Scheduler")] מבטיח שרק סדרנים יכולים להצטרף לקבוצה זו.
    /// שם התפקיד "Scheduler" מתאים לערך ה-Role בטבלת Users ב-DB.
    /// </summary>
    [Authorize(Roles = "Scheduler")]
    public async Task JoinSchedulerGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Schedulers");
    }
}