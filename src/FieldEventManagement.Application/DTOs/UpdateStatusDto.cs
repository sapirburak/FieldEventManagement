namespace FieldEventManagement.Application.DTOs;

/// <summary>
/// DTO לבקשת שינוי סטטוס אירוע על-ידי טכנאי.
/// הסטטוס מגיע כמחרוזת כדי לא לחשוף את ה-enum הפנימי ל-API.
/// ה-Service ממיר אותו ל-EventStatus לפני שמפעיל את ה-State Machine.
/// </summary>
public record UpdateStatusDto(string NewStatus);
