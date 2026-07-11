namespace FieldEventManagement.Core.Entities;

public enum EventStatus
{
    Unassigned = 1,  // Initial state: event received, not yet assigned
    Assigned = 2,    // Assigned to a technician
    InProgress = 3,  // Being handled (intermediate state)
    Completed = 4,   // Completed / closed
    Cancelled = 5    // Cancelled
}