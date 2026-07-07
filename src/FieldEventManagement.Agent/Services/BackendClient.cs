// Services/BackendClient.cs
using System.Net;
using System.Net.Http.Json;
using FieldEventManagement.Agent.Models;

namespace FieldEventManagement.Agent.Services;

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
            var response = await _httpClient.PostAsJsonAsync("/api/events", fieldEvent, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("[HTTP Network] Success! Backend accepted event '{Title}'. Status: {StatusCode}", fieldEvent.Title, response.StatusCode);
                return new BackendResponseDto { IsSuccess = true, StatusCode = response.StatusCode };
            }

            _logger.LogWarning("[HTTP Network] Failed. Backend returned non-success code: {StatusCode}", response.StatusCode);
            return new BackendResponseDto { IsSuccess = false, StatusCode = response.StatusCode };
        }
        catch (HttpRequestException ex)
        {
            // שגיאות רשת קשות (כמו שרת כבוי) נזרקות מעלה כדי שה-Worker ידע להקפיא את התור
            _logger.LogError(ex, "[HTTP Network] Network connection failure to central backend.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[HTTP Network] Critical unexpected error occurred while communicating with backend.");
            return new BackendResponseDto { IsSuccess = false, StatusCode = HttpStatusCode.InternalServerError };
        }
    }
}