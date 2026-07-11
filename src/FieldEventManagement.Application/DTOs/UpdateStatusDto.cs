namespace FieldEventManagement.Application.DTOs;

/// <summary>
/// DTO for a technician's request to change an event's status.
/// The status arrives as a string to avoid exposing the internal enum to the API.
/// The Service converts it to EventStatus before activating the State Machine.
/// </summary>
public record UpdateStatusDto(string NewStatus);
