// Services/IBackendClient.cs
using FieldEventManagement.Agent.Models;

namespace FieldEventManagement.Agent.Services;

public interface IBackendClient
{
    /// <summary>
    /// שולח אירוע שטח בודד אל השרת המרכזי
    /// </summary>
    Task<BackendResponseDto> SendEventToBackendAsync(FieldEventDto fieldEvent, CancellationToken cancellationToken);
}