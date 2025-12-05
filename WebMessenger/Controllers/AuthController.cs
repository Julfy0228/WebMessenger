using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebMessenger.Entities;
using WebMessenger.Models.Requests;

namespace WebMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IEmailSender emailSender) : ControllerBase
    {
        private readonly UserManager<User> _userManager = userManager;
        private readonly SignInManager<User> _signInManager = signInManager;
        private readonly IEmailSender _emailSender = emailSender;

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                DisplayName = request.DisplayName
            };

            var result = await _userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmUrl = Url.Action(nameof(ConfirmEmail), "Auth",
                new { userId = user.Id, token }, Request.Scheme);

            var html = System.IO.File.ReadAllText("Views/Email/ConfirmEmail.html")
                .Replace("@UserName", user.UserName)
                .Replace("@ConfirmUrl", confirmUrl);

            await _emailSender.SendEmailAsync(user.Email!, "Подтвердите вашу почту", html);

            return Ok("Регистрация успешна. Проверьте письмо на вашем почтовом адресе, чтобы завершить регистрацию.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByNameAsync(request.Login)
                       ?? await _userManager.FindByEmailAsync(request.Login);

            if (user == null)
                return Unauthorized("Неудачная попытка авторизации.");

            var result = await _signInManager.PasswordSignInAsync(
                user, request.Password, request.RememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized("Неудачная попытка авторизации.");

            return Ok("Вы успешно авторизировались.");
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("Пользователь не найден.");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Почта успешно подтверждена.");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok("Вы успешно вышли из системы.");
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized("Пользователь не авторизован.");

            return Ok(new
            {
                user.Id,
                user.UserName,
                user.Email,
                user.DisplayName,
                user.IsOnline,
                user.LastOnline,
                user.CreatedAt
            });
        }
    }
}
