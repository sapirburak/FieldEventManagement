using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FieldEventManagement.Agent.Models;

namespace FieldEventManagement.Agent.Services;

/// <summary>
/// A DelegatingHandler that manages JWT authentication for outgoing HTTP requests.
/// It automatically handles token caching, thread-safe refreshing, and dynamic expiry parsing.
/// </summary>
public class JwtAuthHandler : DelegatingHandler
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<JwtAuthHandler> _logger;

    // Semaphore for ensuring thread-safety in concurrent scenarios
    private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    private string? _cachedToken;
    private DateTime _tokenExpiry = DateTime.MinValue;

    public JwtAuthHandler(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<JwtAuthHandler> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Intercepts the outgoing HTTP request to inject a valid JWT Bearer token.
    /// Implements a self-healing mechanism: if the server returns a 401 Unauthorized, 
    /// it forcefully refreshes the token and retries the request once.
    /// </summary>
    /// <param name="request">The original HTTP request message.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The HttpResponseMessage from the server, possibly after a retry.</returns>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // 1. Ensure we have a valid token (either from cache or by performing a login).
        var token = await GetValidTokenAsync(cancellationToken);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 2. Send the initial request.
        var response = await base.SendAsync(request, cancellationToken);

        // 3. Handle 401 Unauthorized: This might happen if the token expired 
        // prematurely on the server side despite our local cache status.
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("401 Unauthorized detected. Forcefully refreshing token and retrying...");

            // 4. Forcefully clear the cache so the next call forces a new login.
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _cachedToken = null;
                _tokenExpiry = DateTime.MinValue;
            }
            finally
            {
                _semaphore.Release();
            }

            // 5. Retrieve a fresh token from the server.
            token = await GetValidTokenAsync(cancellationToken);

            // 6. Clone the original request and update the authorization header.
            // We clone because HttpRequestMessage cannot be sent more than once.
            var newRequest = CloneRequest(request);
            newRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // 7. Retry the request with the new token.
            return await base.SendAsync(newRequest, cancellationToken);
        }

        return response;
    }

    /// <summary>
    /// Retrieves a valid token, refreshing it if necessary using a thread-safe approach.
    /// </summary>
    private async Task<string> GetValidTokenAsync(CancellationToken cancellationToken)
    {
        // Fast check: is the token already present and valid? (no lock required)
        if (IsTokenValid()) return _cachedToken!;

        // If not, acquire the lock to prevent multiple simultaneous Login calls
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring the lock (Double-check locking pattern)
            if (IsTokenValid()) return _cachedToken!;

            return await RefreshTokenAsync(cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    /// <summary>
    /// Clones an HttpRequestMessage to allow retrying the request.
    /// Copies the Method, RequestUri, Headers, and Content.
    /// </summary>
    private HttpRequestMessage CloneRequest(HttpRequestMessage request)
    {
        var newRequest = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
            Content = request.Content // Note: if Content is a Stream, you may need to Seek it back to position 0
        };

        foreach (var header in request.Headers)
        {
            newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return newRequest;
    }

    /// <summary>
    /// Checks if the cached token is currently valid based on expiry time.
    /// </summary>
    private bool IsTokenValid() =>
        !string.IsNullOrWhiteSpace(_cachedToken) && DateTime.UtcNow < _tokenExpiry;

    /// <summary>
    /// Performs the actual HTTP call to the login endpoint and updates the token and expiry.
    /// </summary>
    private async Task<string> RefreshTokenAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Refreshing JWT token...");

        var backendUrl = _configuration["AgentSettings:BackendUrl"]
            ?? throw new InvalidOperationException("AgentSettings:BackendUrl is not configured.");

        var username = _configuration["BackendAuth:Username"]
            ?? throw new InvalidOperationException("BackendAuth:Username is not configured.");

        var password = _configuration["BackendAuth:Password"]
            ?? throw new InvalidOperationException("BackendAuth:Password is not configured.");

        using var authClient = _httpClientFactory.CreateClient("InsecureClient");
        var response = await authClient.PostAsJsonAsync($"{backendUrl}/api/auth/login", new
        {
            username,
            password
        }, cancellationToken);

        response.EnsureSuccessStatusCode();

        var loginResponse = await response.Content.ReadFromJsonAsync<LoginResponse>(cancellationToken: cancellationToken);
        _cachedToken = loginResponse!.Token;

        _tokenExpiry = ParseExpiryFromToken(_cachedToken);

        return _cachedToken;
    }

    /// <summary>
    /// Parses the 'exp' claim from the JWT to determine when it expires.
    /// </summary>
    private DateTime ParseExpiryFromToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(token);
            var exp = jwtToken.Claims.FirstOrDefault(c => c.Type == "exp")?.Value;

            if (exp != null && long.TryParse(exp, out var seconds))
            {
                // Convert from Unix format to DateTime with a 5-minute buffer offset
                return DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime.AddMinutes(-5);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to parse token expiry, using default fallback.");
        }

        return DateTime.UtcNow.AddMinutes(55); // Fallback if parsing fails
    }
}