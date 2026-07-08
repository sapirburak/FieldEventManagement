using System;
using System.Collections.Generic;

namespace FieldEventManagement.Core.Entities;

public class FieldEvent
{
    // Properties עם private set כדי למנוע שינוי מבחוץ ללא מעבר ב-State Machine
    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public string Source { get; private set; } = string.Empty;
    public string Location { get; private set; } = string.Empty;
    public EventStatus Status { get; private set; }
    public string? AssignedTechnicianId { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // ניהול ה-Audit Trail (היסטוריית המצבים)
    private readonly List<EventStateHistory> _history = new();
    public IReadOnlyCollection<EventStateHistory> History => _history.AsReadOnly();

    // קונסטרקטור פרטי הדרוש עבור EF Core בזמן שליפת נתונים
    private FieldEvent() { }

    // Factory Method ליצירת אירוע חדש במצב ראשוני
    public static FieldEvent Create(Guid id, string title, string description, string source, string location)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("כותרת האירוע היא שדה חובה.");

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

        // רישום המצב הראשוני בהיסטוריה
        fieldEvent._history.Add(new EventStateHistory(id, EventStatus.Unassigned, "System_Agent", DateTime.UtcNow));
        return fieldEvent;
    }

    // אכיפת ה-State Machine - הלב הארכיטקטוני של הדרישה
    public void TransitionTo(EventStatus newStatus, string updatedBy)
    {
        bool isValidTransition = (Status, newStatus) switch
        {
            // 1. ממצב ראשוני מותר להקצות או לבטל
            (EventStatus.Unassigned, EventStatus.Assigned) => true,
            (EventStatus.Unassigned, EventStatus.Cancelled) => true,

            // 2. ממצב מוקצה מותר לעבור לטיפול, להקצות מחדש לטכנאי אחר, או לבטל
            (EventStatus.Assigned, EventStatus.InProgress) => true,
            (EventStatus.Assigned, EventStatus.Assigned) => true, // תמיכה בהעברה מטכנאי לטכנאי
            (EventStatus.Assigned, EventStatus.Cancelled) => true,

            // 3. ממצב בטיפול מותר להשלים או לבטל
            (EventStatus.InProgress, EventStatus.Completed) => true,
            (EventStatus.InProgress, EventStatus.Cancelled) => true,

            // כל מעבר אחר (למשל מבוטל להושלם, או הושלם למוקצה) חסום הרמטית
            _ => false
        };

        if (!isValidTransition)
        {
            throw new InvalidOperationException($"מעבר מצב לא חוקי: לא ניתן לעבור ממצב {Status} למצב {newStatus}.");
        }

        Status = newStatus;
        _history.Add(new EventStateHistory(Id, newStatus, updatedBy, DateTime.UtcNow));
    }

    // מתודת עזר ייעודית להקצאת טכנאי
    public void AssignToTechnician(string technicianId, string dispatcherId)
    {
        TransitionTo(EventStatus.Assigned, dispatcherId);
        AssignedTechnicianId = technicianId;
    }
}