using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using System.Data.Common;

namespace FieldEventManagement.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EventsController : ControllerBase
{
    private readonly EventReceiverService _eventService;

    public EventsController(EventReceiverService eventService)
    {
        _eventService = eventService;
    }

   
    [HttpPost("receiveEvent")]
    public async Task<IActionResult> ReceiveEvent([FromBody] WrappedEvent incomingEvent)
    {
        try
        {
            // כאן ה-API קורא ל-Service של ה-Application
            ProcessResult result = await _eventService.ProcessIncomingEventAsync(incomingEvent);
            return Ok(result); // יחזיר JSON: { "status": "Updated", "message": "..." }
        }
        catch (DbException ex) // שגיאת תשתית
        {
          
            return StatusCode(500, "Database connection failed.");
        }
        catch (ArgumentException ex)
        {
            // שגיאת ולידציה -> 422 Unprocessable Entity
            return UnprocessableEntity(ex.Message);
        }
        catch (Exception)
        {
            // שגיאה לא צפויה -> 500
            return StatusCode(500, "שגיאה פנימית בשרת.");
        }
    }
}