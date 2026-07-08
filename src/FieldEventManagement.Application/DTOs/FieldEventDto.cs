namespace FieldEventManagement.Application.DTOs;

// הנתונים הפנימיים של האירוע כפי שה-Agent שלך אורז אותם
public record FieldEventDto(
    string Title,
    string Description,
    string Source,
    string Location,
     string Severity
);