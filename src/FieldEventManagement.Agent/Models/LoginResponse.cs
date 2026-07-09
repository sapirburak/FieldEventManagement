namespace FieldEventManagement.Agent.Models;

/// <summary>
/// DTO for the login response from the authentication API
/// </summary>
public class LoginResponse
{
    public string Token { get; set; } = string.Empty;
}
