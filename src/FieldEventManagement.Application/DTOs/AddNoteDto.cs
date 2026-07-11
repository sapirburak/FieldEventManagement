namespace FieldEventManagement.Application.DTOs;

/// <summary>
/// DTO for sending a note from a technician to the dispatcher on an active event.
/// </summary>
public record AddNoteDto(string Text);
