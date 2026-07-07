// Models/FieldEventDto.cs
namespace FieldEventManagement.Agent.Models;

/// <summary>
/// אובייקט העברת הנתונים של אירוע שטח כפי שמתקבל ממקורות חיצוניים
/// </summary>
public record FieldEventDto(
    string Title,
    string Description,
    string Source,
    string Location,
    string Severity // Low, Medium, High, Critical
);

/// <summary>
/// אובייקט עזר פנימי המוסיף מזהה ייחודי (GUID) לצורך ניהול קבצי גיבוי מקומיים בדיסק
/// </summary>WrappedEvent
public record WrappedEvent(Guid Id, FieldEventDto Data);