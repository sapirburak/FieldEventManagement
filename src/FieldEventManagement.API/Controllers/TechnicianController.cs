using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Services;
using FieldEventManagement.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FieldEventManagement.API.Controllers;

/// <summary>
/// Contains all endpoints accessible by a technician.
/// [Authorize(Roles = "Technician")] enforces at the Controller level that only technicians reach here.
/// The dispatcher is managed separately in EventsController – clear separation of concerns.
/// </summary>
[Authorize(Roles = "Technician")]
[ApiController]
[Route("api/technician/events")]
public class TechnicianController : ControllerBase
{
    private readonly TechnicianEventService _technicianService;

    public TechnicianController(TechnicianEventService technicianService)
    {
        _technicianService = technicianService;
    }

    /// <summary>
    /// A technician updates the status of an event assigned to them.
    /// The Domain State Machine verifies that the transition is valid (no skipping allowed).
    /// 
    /// PATCH /api/technician/events/{id}/status
    /// Body: { "newStatus": "InProgress" }
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusDto dto)
    {
        var technicianId = GetCurrentUserId();

        try
        {
            var result = await _technicianService.UpdateStatusAsync(id, dto.NewStatus, technicianId);

            return result.Status switch
            {
                "NotFound"      => NotFound(result.Message),
                "InvalidStatus" => UnprocessableEntity(result.Message),
                _               => Ok(result)
            };
        }
        catch (InvalidFieldEventStateException ex)
        {
            // State Machine rejected the transition (e.g. Completed → InProgress)
            return UnprocessableEntity(ex.Message);
        }
    }

    /// <summary>
    /// A technician sends a note to the dispatcher on an active event.
    /// The dispatcher receives a real-time notification via SignalR.
    /// 
    /// POST /api/technician/events/{id}/notes
    /// Body: { "text": "Need additional equipment" }
    /// </summary>
    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] AddNoteDto dto)
    {
        var technicianId = GetCurrentUserId();
        var result = await _technicianService.AddNoteAsync(id, dto.Text, technicianId);

        return result.Status switch
        {
            "NotFound"    => NotFound(result.Message),
            "InvalidNote" => BadRequest(result.Message),
            _             => Ok(result)
        };
    }

    /// <summary>
    /// A technician requests to claim an unassigned event.
    /// 
    /// POST /api/technician/events/{id}/request
    /// </summary>
    [HttpPost("{id:guid}/request")]
    public async Task<IActionResult> RequestEvent(Guid id)
    {
        var technicianId = GetCurrentUserId();

        try
        {
            var result = await _technicianService.RequestEventAsync(id, technicianId);

            return result.Status switch
            {
                "NotFound" => NotFound(result.Message),
                "Conflict" => Conflict(result.Message),
                _          => Ok(result)
            };
        }
        catch (InvalidFieldEventStateException ex)
        {
            return UnprocessableEntity(ex.Message);
        }
    }

    /// <summary>
    /// Extracts the userId of the currently authenticated technician from the JWT Claims.
    /// ClaimTypes.Name contains the username injected by TokenService.
    /// </summary>
    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
}
