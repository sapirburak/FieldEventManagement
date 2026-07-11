using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;

namespace FieldEventManagement.Api.Controllers;

[Authorize(Roles = "Scheduler")] // Only users with the Scheduler role in their token are allowed
[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly EventReceiverService _eventService;

    public EventsController(EventReceiverService eventService)
    {
        _eventService = eventService;
    }

    [Authorize] // Anyone holding a valid token (regardless of role)
    [HttpPost("receiveEvent")]
    public async Task<IActionResult> ReceiveEvent([FromBody] WrappedEvent incomingEvent)
    {
        try
        {
            // The API calls the Application Service here
            ProcessResult result = await _eventService.ProcessIncomingEventAsync(incomingEvent);
            return Ok(result); // Returns JSON: { "status": "Updated", "message": "..." }
        }
        catch (DbException ex) // שגיאת תשתית
        {
          
            return StatusCode(500, "Database connection failed.");
        }
        catch (ArgumentException ex)
        {
            // Validation error -> 422 Unprocessable Entity
            return UnprocessableEntity(ex.Message);
        }
        catch (Exception)
        {
            // Unexpected error -> 500
            return StatusCode(500, "Internal server error.");
        }
    }
}