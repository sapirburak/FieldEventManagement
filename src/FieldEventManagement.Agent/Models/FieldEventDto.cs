// Models/FieldEventDto.cs
namespace FieldEventManagement.Agent.Models;

/// <summary>
/// Data Transfer Object for a field event as received from external sources.
/// </summary>
public record FieldEventDto(
    string Title,
    string Description,
    string Source,
    string Location,
    string Severity // Low, Medium, High, Critical
);

/// <summary>
/// Internal helper object that adds a unique identifier (GUID) for managing local backup files on disk.
/// </summary>
public record WrappedEvent(Guid Id, FieldEventDto Data);
