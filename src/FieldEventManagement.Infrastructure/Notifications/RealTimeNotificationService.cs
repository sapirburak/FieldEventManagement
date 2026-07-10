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
    /// משדר הודעה לכל הלקוחות המחוברים לקבוצת 'Schedulers'.
    /// מנתק את ה-Application מהתלות בספריית ה-SignalR עצמה (Dependency Inversion).
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