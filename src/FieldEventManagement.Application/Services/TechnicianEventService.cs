using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Core.Entities;

namespace FieldEventManagement.Application.Services;

/// <summary>
/// Centralises all business operations a technician can perform on events.
/// Every method follows the same pattern:
///   1. Fetch from DB
///   2. Validate through the Domain (State Machine / permissions)
///   3. Save
///   4. Notify in real time
/// </summary>
public class TechnicianEventService
{
    private readonly IFieldEventRepository _repository;
    private readonly IRealTimeNotificationService _notificationService;

    public TechnicianEventService(
        IFieldEventRepository repository,
        IRealTimeNotificationService notificationService)
    {
        _repository = repository;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Updates the event status by the technician.
    /// The Domain State Machine enforces that the transition is valid before writing to the DB.
    /// After saving, the dispatcher receives a real-time update via SignalR.
    /// </summary>
    public async Task<ProcessResult> UpdateStatusAsync(Guid eventId, string newStatus, string technicianId)
    {
        // 1. Fetch
        var fieldEvent = await _repository.GetByIdAsync(eventId);
        if (fieldEvent is null)
            return new ProcessResult("NotFound", $"Event {eventId} was not found.");

        // 2. Convert + validate via the State Machine
        if (!Enum.TryParse<EventStatus>(newStatus, ignoreCase: true, out var parsedStatus))
            return new ProcessResult("InvalidStatus", $"'{newStatus}' is not a valid EventStatus.");

        // TransitionTo throws InvalidFieldEventStateException if the transition is invalid
        fieldEvent.TransitionTo(parsedStatus, technicianId, actingRole: "Technician");

        // 3. Save
        await _repository.SaveChangesAsync();

        // 4. Notify the dispatcher
        await _notificationService.NotifySchedulerOfStatusUpdateAsync(eventId, newStatus, technicianId);

        return new ProcessResult("Updated", $"Event status changed to {newStatus}.");
    }

    /// <summary>
    /// Adds a note from the technician on an active event and notifies the dispatcher in real time.
    /// TODO: persist the note to the DB (requires adding an EventNotes table and AddNoteAsync to the Repository).
    /// </summary>
    public async Task<ProcessResult> AddNoteAsync(Guid eventId, string note, string technicianId)
    {
        if (string.IsNullOrWhiteSpace(note))
            return new ProcessResult("InvalidNote", "Note text cannot be empty.");

        var fieldEvent = await _repository.GetByIdAsync(eventId);
        if (fieldEvent is null)
            return new ProcessResult("NotFound", $"Event {eventId} was not found.");

        // TODO: _repository.AddNoteAsync(eventId, note, technicianId);
        // TODO: await _repository.SaveChangesAsync();

        await _notificationService.NotifySchedulerOfNoteAsync(eventId, note, technicianId);

        return new ProcessResult("NoteAdded", "Note sent to scheduler.");
    }

    /// <summary>
    /// Allows a technician to request claiming an unassigned event.
    /// In practice assigns the event to the technician and notifies the dispatcher – the dispatcher can approve/reject (TODO).
    /// </summary>
    public async Task<ProcessResult> RequestEventAsync(Guid eventId, string technicianId)
    {
        var fieldEvent = await _repository.GetByIdAsync(eventId);
        if (fieldEvent is null)
            return new ProcessResult("NotFound", $"Event {eventId} was not found.");

        if (fieldEvent.Status != EventStatus.Unassigned)
            return new ProcessResult("Conflict", "Event is no longer available.");

        // AssignToTechnician calls TransitionTo + saves the technicianId
        fieldEvent.AssignToTechnician(technicianId, dispatcherId: technicianId);

        await _repository.SaveChangesAsync();

        await _notificationService.NotifyTechnicianOfAssignmentAsync(eventId, technicianId, fieldEvent.Title);

        return new ProcessResult("Requested", "Event assigned to technician.");
    }
}
