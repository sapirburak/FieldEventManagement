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
            // 1. בדיקה מול ה-DB (האם המשתמש קיים והסיסמה נכונה?)
            // שימי לב: בחיים האמיתיים תשתמשו ב-PasswordHasher ולא תשוו סיסמה בטקסט חופשי!
            var user = _userRepository.GetUserByCredentials(loginDto.Username, loginDto.Password);

            if (user == null)
                return Unauthorized("שם משתמש או סיסמה שגויים.");

            // 2. יצירת הטוקן (הנה השלב שבו המערכת מנפיקה תעודת זהות)
            var token = _tokenService.GenerateToken(user.Username, user.Role);

            // 3. החזרת הטוקן ללקוח (אנגולר/אייג'נט)
            return Ok(new { token = token });
        }
    }
}
