using FieldEventManagement.Core.Exceptions;
using System;
using System.Collections.Generic;

namespace FieldEventManagement.Core.Entities;

public class FieldEvent
{
    // Properties with private set to prevent external modification without going through the State Machine
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public EventStatus Status { get; private set; }
    public string? AssignedTechnicianId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Audit Trail management (status history)
    private readonly List<EventStateHistory> _history = new();
    public IReadOnlyCollection<EventStateHistory> History => _history.AsReadOnly();

    // Private constructor required by EF Core when loading data
    private FieldEvent() { }

    // Factory Method for creating a new event in the initial state
    public static FieldEvent Create(Guid id, string title, string description, string source, string location)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Event title is a required field.");

        var fieldEvent = new FieldEvent
        {
            Id = id,
            Title = title,
            Description = description,
            Source = source,
            Location = location,
            Status = EventStatus.Unassigned,
            CreatedAt = DateTime.UtcNow
        };

        // Record the initial state in the history
        fieldEvent._history.Add(new EventStateHistory(id, EventStatus.Unassigned, "System_Agent", DateTime.UtcNow));
        return fieldEvent;
    }

    // State Machine enforcement - the architectural heart of the requirement
    public void TransitionTo(EventStatus newStatus, string updatedBy, string? actingRole = null)
    {
        if (newStatus == EventStatus.Cancelled && !IsCancellationAuthorized(actingRole))
        {
            throw new InvalidFieldEventStateException("Cancelling an event is only permitted for users with the Dispatcher role.");
        }

        bool isValidTransition = (Status, newStatus) switch
        {
            // 1. From the initial state, allowed to assign or cancel
            (EventStatus.Unassigned, EventStatus.Assigned) => true,
            (EventStatus.Unassigned, EventStatus.Cancelled) => true,

            // 2. From assigned, allowed to move to in-progress, reassign to another technician, or cancel
            (EventStatus.Assigned, EventStatus.InProgress) => true,
            (EventStatus.Assigned, EventStatus.Assigned) => true, // supports transfer between technicians
            (EventStatus.Assigned, EventStatus.Cancelled) => true,

            // 3. From in-progress, allowed to complete or cancel
            (EventStatus.InProgress, EventStatus.Completed) => true,
            (EventStatus.InProgress, EventStatus.Cancelled) => true,

            // Any other transition (e.g. cancelled to completed, or completed to assigned) is hermetically blocked
            _ => false
        };

        if (!isValidTransition)
        {
            throw new InvalidFieldEventStateException($"Invalid state transition: cannot transition from {Status} to {newStatus}.");
        }

        Status = newStatus;
        _history.Add(new EventStateHistory(Id, newStatus, updatedBy, DateTime.UtcNow));
    }

    private static bool IsCancellationAuthorized(string? actingRole)
        => string.Equals(actingRole, "Dispatcher", StringComparison.OrdinalIgnoreCase);

    // Dedicated helper method for assigning a technician
    public void AssignToTechnician(string technicianId, string dispatcherId)
    {
        TransitionTo(EventStatus.Assigned, dispatcherId);
        AssignedTechnicianId = technicianId;
    }
}