// Services/IBackendClient.cs
using FieldEventManagement.Agent.Models;

namespace FieldEventManagement.Agent.Services;

public interface IBackendClient
{
    /// <summary>
    /// שולח אירוע שטח בודד אל השרת המרכזי.
    /// מקבל את ה-WrappedEvent המלא כדי לשמר את ה-Id המקורי מ-SQLite
    /// ולאפשר בדיקת Idempotency תקינה בצד השרת.
    /// </summary>
    Task<BackendResponseDto> SendEventToBackendAsync(WrappedEvent wrappedEvent, CancellationToken cancellationToken);
}