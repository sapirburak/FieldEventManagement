using System;

namespace FieldEventManagement.Core.Entities;

public class EventStateHistory
{
    public Guid Id { get; private set; }
    public Guid FieldEventId { get; private set; }
    public EventStatus Status { get; private set; }
    public string ChangedBy { get; private set; } = string.Empty;
    public DateTime Timestamp { get; private set; }

    private EventStateHistory() { }

    public EventStateHistory(Guid fieldEventId, EventStatus status, string changedBy, DateTime timestamp)
    {
        Id = Guid.NewGuid();
        FieldEventId = fieldEventId;
        Status = status;
        ChangedBy = changedBy;
        Timestamp = timestamp;
    }
}