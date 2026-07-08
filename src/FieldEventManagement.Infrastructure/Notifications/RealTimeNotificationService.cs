using FieldEventManagement.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;

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
    /// משדר הודעה לכל הלקוחות המחוברים לקבוצת 'Dispatchers'.
    /// מנתק את ה-Application מהתלות בספריית ה-SignalR עצמה (Dependency Inversion).
    /// </summary>
    public async Task NotifyDispatcherOfNewEventAsync(Guid eventId, string title, string location)
    {
        // שליחה לכל מי שמחובר לקבוצת ה-Dispatchers
        await _hubContext.Clients.Group("Dispatchers").SendAsync("ReceiveNewEvent", new
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
/// </summary>
public class EventHub : Hub
{

    // ניתן להוסיף כאן לוגיקה של OnConnectedAsync אם נרצה לנהל קבוצות (Groups)
    public async Task JoinDispatcherGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Dispatchers");
    }
}