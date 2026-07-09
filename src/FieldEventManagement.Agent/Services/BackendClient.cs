// Services/BackendClient.cs
using FieldEventManagement.Agent.Models;
using System.Net;
using System.Net.Http.Json;

namespace FieldEventManagement.Agent.Services;

/// <summary>
/// HTTP client for communicating with the central backend API.
/// </summary>
public class BackendClient : IBackendClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BackendClient> _logger;

    public BackendClient(HttpClient httpClient, ILogger<BackendClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<BackendResponseDto> SendEventToBackendAsync(FieldEventDto fieldEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("[HTTP Network] Attempting to forward event '{Title}' to central backend...", fieldEvent.Title);

        try
        {
            var wrappedEvent = new WrappedEvent(Guid.NewGuid(), fieldEvent);
            var response = await _httpClient.PostAsJsonAsync("/api/events/receiveEvent", wrappedEvent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[HTTP Network] Success! Backend accepted event '{Title}'. Status: {StatusCode}", fieldEvent.Title, response.StatusCode);
                return new BackendResponseDto { IsSuccess = true, StatusCode = response.StatusCode };
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning("[HTTP Network] Failed. Backend returned non-success code: {StatusCode}. Response: {Content}", response.StatusCode, content);

            return new BackendResponseDto { IsSuccess = false, StatusCode = response.StatusCode };
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[HTTP Network] Network connection failure to central backend.");
            throw;
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "[HTTP Network] Request was cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HTTP Network] Critical unexpected error occurred while communicating with backend.");
            return new BackendResponseDto { IsSuccess = false, StatusCode = HttpStatusCode.InternalServerError };
        }
    }
}
