namespace FieldEventManagement.Application.DTOs;

// The internal event data as packaged by the Agent
public record FieldEventDto(
    string Title,
    string Description,
    string Source,
    string Location,
     string Severity
);