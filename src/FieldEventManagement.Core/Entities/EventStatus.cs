namespace FieldEventManagement.Core.Entities;

public enum EventStatus
{
    Unassigned = 1,  // מצב ראשוני: אירוע נכנס, טרם הוקצה
    Assigned = 2,    // הוקצה לטכנאי
    InProgress = 3,  // בטיפול (מצב ביניים)
    Completed = 4,   // הושלם / סגור
    Cancelled = 5    // מבוטל
}