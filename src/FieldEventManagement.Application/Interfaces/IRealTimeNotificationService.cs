namespace FieldEventManagement.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for real-time notification services.
    /// Using this interface allows the system to switch from SignalR to any other Push solution (e.g. Firebase/Azure Push)
    /// without changing the business logic in the Application layer.
    /// </summary>
    public interface IRealTimeNotificationService
    {
        /// <summary>
        /// Broadcasts a notification to connected dispatchers about a new event received from the Agent.
        /// </summary>
        Task NotifyDispatcherOfNewEventAsync(Guid eventId, string title, string location);

        /// <summary>
        /// Broadcasts to dispatchers that an existing event's status was changed by a technician.
        /// Required when a technician updates status (e.g. Assigned → InProgress).
        /// </summary>
        Task NotifySchedulerOfStatusUpdateAsync(Guid eventId, string newStatus, string technicianId);

        /// <summary>
        /// Broadcasts to dispatchers that a technician sent a note on an active event.
        /// </summary>
        Task NotifySchedulerOfNoteAsync(Guid eventId, string note, string technicianId);

        /// <summary>
        /// Broadcasts to a specific technician that a new event has been assigned to them.
        /// Sends by ConnectionId – not to a group – because the message targets one person only.
        /// TODO: implement after adding ConnectionId management per technician.
        /// </summary>
        Task NotifyTechnicianOfAssignmentAsync(Guid eventId, string technicianId, string title);
    }
}
