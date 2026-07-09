namespace FieldEventManagement.Core.Interfaces;

public interface ITokenService
{
    string GenerateToken(string username, string role);
}