using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Core.Entities;

namespace FieldEventManagement.Application.Services;

/// <summary>
/// מרכז את כל הפעולות העסקיות שטכנאי יכול לבצע על אירועים.
/// כל מתודה עוקבת אחרי אותו תבנית:
///   1. שלוף מה-DB
///   2. אמת דרך ה-Domain (State Machine / הרשאות)
///   3. שמור
///   4. הודע בזמן אמת
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
    /// מעדכן את סטטוס האירוע על-ידי הטכנאי.
    /// ה-State Machine ב-Domain אוכף שהמעבר חוקי לפני הכתיבה ל-DB.
    /// לאחר השמירה, הסדרן מקבל עדכון בזמן אמת דרך SignalR.
    /// </summary>
    public async Task<ProcessResult> UpdateStatusAsync(Guid eventId, string newStatus, string technicianId)
    {
        // 1. שליפה
        var fieldEvent = await _repository.GetByIdAsync(eventId);
        if (fieldEvent is null)
            return new ProcessResult("NotFound", $"Event {eventId} was not found.");

        // 2. המרה + ולידציה ע"י ה-State Machine
        if (!Enum.TryParse<EventStatus>(newStatus, ignoreCase: true, out var parsedStatus))
            return new ProcessResult("InvalidStatus", $"'{newStatus}' is not a valid EventStatus.");

        // TransitionTo זורק InvalidFieldEventStateException אם המעבר לא חוקי
        fieldEvent.TransitionTo(parsedStatus, technicianId, actingRole: "Technician");

        // 3. שמירה
        await _repository.SaveChangesAsync();

        // 4. התראה לסדרן
        await _notificationService.NotifySchedulerOfStatusUpdateAsync(eventId, newStatus, technicianId);

        return new ProcessResult("Updated", $"Event status changed to {newStatus}.");
    }

    /// <summary>
    /// מוסיף הערה מהטכנאי על אירוע פעיל ומודיע לסדרן בזמן אמת.
    /// TODO: לשמור את ההערה ב-DB (נדרש הוספת טבלת EventNotes ו-AddNoteAsync לRepository).
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
    /// מאפשר לטכנאי לבקש לקבל על עצמו אירוע פנוי (Unassigned).
    /// בפועל מקצה את האירוע לטכנאי ומודיע לסדרן – הסדרן יכול לאשר/לדחות (TODO).
    /// </summary>
    public async Task<ProcessResult> RequestEventAsync(Guid eventId, string technicianId)
    {
        var fieldEvent = await _repository.GetByIdAsync(eventId);
        if (fieldEvent is null)
            return new ProcessResult("NotFound", $"Event {eventId} was not found.");

        if (fieldEvent.Status != EventStatus.Unassigned)
            return new ProcessResult("Conflict", "Event is no longer available.");

        // AssignToTechnician מפעיל TransitionTo + שומר את הtechnicianId
        fieldEvent.AssignToTechnician(technicianId, dispatcherId: technicianId);

        await _repository.SaveChangesAsync();

        await _notificationService.NotifyTechnicianOfAssignmentAsync(eventId, technicianId, fieldEvent.Title);

        return new ProcessResult("Requested", "Event assigned to technician.");
    }
}
