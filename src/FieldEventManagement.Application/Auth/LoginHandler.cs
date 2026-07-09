using FieldEventManagement.Core.Interfaces;

public class LoginHandler
{
    private readonly ITokenService _tokenService;

    public LoginHandler(ITokenService tokenService)
    {
        _tokenService = tokenService; // הזרקה (DI)
    }
    // ...
}