using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Services;
using FieldEventManagement.Core.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FieldEventManagement.API.Controllers;

/// <summary>
/// מכיל את כל ה-Endpoints שטכנאי יכול לגשת אליהם.
/// [Authorize(Roles = "Technician")] אוכף ברמת הController שרק טכנאים יגיעו לכאן.
/// הסדרן מנוהל ב-EventsController בנפרד – הפרדת אחריות ברורה.
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
    /// טכנאי מעדכן סטטוס אירוע שהוקצה אליו.
    /// ה-State Machine ב-Domain מוודא שהמעבר חוקי (לא ניתן לדלג).
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
            // State Machine דחה את המעבר (לדוגמה: Completed → InProgress)
            return UnprocessableEntity(ex.Message);
        }
    }

    /// <summary>
    /// טכנאי שולח הערה לסדרן על אירוע פעיל.
    /// הסדרן מקבל התראה בזמן אמת דרך SignalR.
    /// 
    /// POST /api/technician/events/{id}/notes
    /// Body: { "text": "צריך ציוד נוסף" }
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
    /// טכנאי מבקש לקבל על עצמו אירוע פנוי (Unassigned).
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
    /// חולץ את ה-userId של הטכנאי המחובר מה-JWT Claims.
    /// ClaimTypes.Name מכיל את ה-username שהוזרק ב-TokenService.
    /// </summary>
    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.Name) ?? "unknown";
}
