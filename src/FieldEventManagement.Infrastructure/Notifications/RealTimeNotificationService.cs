using FieldEventManagement.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace FieldEventManagement.Infrastructure.Notifications;
/// <summary>
/// Service for managing real-time bidirectional communication with dispatchers.
/// Translates Application business calls into SignalR broadcasts.
/// </summary>
public class RealTimeNotificationService : IRealTimeNotificationService
{
    private readonly IHubContext<EventHub> _hubContext;

    public RealTimeNotificationService(IHubContext<EventHub> hubContext) => _hubContext = hubContext;

    /// <summary>
    /// Notifies all connected dispatchers about a new event received from the Agent.
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
    /// Notifies all dispatchers that a technician updated the status of an active event.
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
    /// Notifies all dispatchers that a technician added a note on an event.
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
    /// TODO: implement after adding ConnectionId management per technician.
    /// Currently sends to all dispatchers as a fallback.
    /// In production: send directly to the technician by stored ConnectionId.
    /// </summary>
    public async Task NotifyTechnicianOfAssignmentAsync(Guid eventId, string technicianId, string title)
    {
        // TODO: replace with direct send to the technician's ConnectionId
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
/// The SignalR Hub. This class serves as the endpoint for real-time communication.
/// [Authorize] at the Hub level ensures that a WebSocket connection requires a valid JWT.
/// Without this, anyone could connect and listen to events.
/// </summary>
[Authorize]
public class EventHub : Hub
{
    /// <summary>
    /// Adds the client to the Schedulers group to receive notifications.
    /// [Authorize(Roles = "Scheduler")] ensures only dispatchers can join this group.
    /// The role name "Scheduler" matches the Role value in the Users table in the DB.
    /// </summary>
    [Authorize(Roles = "Scheduler")]
    public async Task JoinSchedulerGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Schedulers");
    }
}
