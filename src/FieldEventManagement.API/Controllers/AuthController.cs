using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Core.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FieldEventManagement.API.Controllers
{
    
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ITokenService _tokenService;
        private readonly IUserRepository _userRepository; // נניח שיש לך כזה שבודק ב-DB

        public AuthController(ITokenService tokenService, IUserRepository userRepository)
        {
            _tokenService = tokenService;
            _userRepository = userRepository;
        }

        [HttpPost("login")]
        public IActionResult Login([FromBody] LoginDto loginDto)
        {
            // 1. Check against the DB (does the user exist and is the password correct?)
            // Note: in real life use a PasswordHasher and never compare plain-text passwords!
            var user = _userRepository.GetUserByCredentials(loginDto.Username, loginDto.Password);

            if (user == null)
                return Unauthorized("Invalid username or password.");

            // 2. Generate the token (this is where the system issues an identity credential)
            var token = _tokenService.GenerateToken(user.Username, user.Role);

            // 3. Return the token to the client (Angular / Agent)
            return Ok(new { token = token });
        }
    }
}
