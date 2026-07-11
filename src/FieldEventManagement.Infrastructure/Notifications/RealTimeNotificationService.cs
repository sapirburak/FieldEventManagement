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
    /// The payload shape matches the Angular FieldEvent interface exactly:
    /// id (not eventId), status, description are included so the frontend can
    /// construct a full FieldEvent without a separate GET request.
    /// </summary>
    public async Task NotifyDispatcherOfNewEventAsync(Guid eventId, string title, string location)
    {
        await _hubContext.Clients.Group("Dispatchers").SendAsync("ReceiveNewEvent", new
        {
            id = eventId,          // matches FieldEvent.id in the Angular model
            title,
            description = string.Empty,
            status = "Unassigned", // all new events start as Unassigned
            location,
            timestamp = DateTime.UtcNow,
            assignedTechnicianId = (string?)null
        });
    }

    /// <summary>
    /// Notifies all dispatchers that a technician updated the status of an active event.
    /// Angular event: "ReceiveStatusUpdate"
    /// </summary>
    public async Task NotifyDispatcherOfStatusUpdateAsync(Guid eventId, string newStatus, string technicianId)
    {
        await _hubContext.Clients.Group("Dispatchers").SendAsync("ReceiveStatusUpdate", new
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
    public async Task NotifyDispatcherOfNoteAsync(Guid eventId, string note, string technicianId)
    {
        await _hubContext.Clients.Group("Dispatchers").SendAsync("ReceiveNote", new
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
        await _hubContext.Clients.Group("Dispatchers").SendAsync("ReceiveAssignment", new
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
    /// Adds the client to the Dispatchers group to receive notifications.
    /// [Authorize(Roles = "Dispatcher")] ensures only dispatchers can join this group.
    /// The role name "Dispatcher" matches the Role value in the Users table in the DB.
    /// </summary>
    [Authorize(Roles = "Dispatcher")]
    public async Task JoinDispatcherGroup()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "Dispatchers");
    }
}
