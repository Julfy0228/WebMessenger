using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebMessenger.Entities;
using WebMessenger.Models.Requests;
using WebMessenger.Models.Responses;

namespace WebMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        IEmailSender emailSender) : ControllerBase
    {
        #region Авторизация
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(new { errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage) });

            var user = new User
            {
                UserName = request.UserName,
                Email = request.Email,
                DisplayName = request.DisplayName
            };

            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
                return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

            await SendConfirmationEmail(user);

            return Ok(new { message = "Регистрация успешна. Проверьте письмо на вашем почтовом адресе." });
        }

        private async Task SendConfirmationEmail(User user)
        {
            var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmUrl = Url.Action(nameof(ConfirmEmail), "User",
                new { userId = user.Id, token }, Request.Scheme);

            var html = System.IO.File.ReadAllText("Views/Email/ConfirmEmail.html")
                .Replace("@UserName", user.UserName)
                .Replace("@ConfirmUrl", confirmUrl);

            await emailSender.SendEmailAsync(user.Email!, "Подтвердите вашу почту", html);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await userManager.FindByNameAsync(request.Login)
                       ?? await userManager.FindByEmailAsync(request.Login);

            if (user == null)
                return Unauthorized(new { message = "Пользователь не найден или неверные данные." });

            var result = await signInManager.PasswordSignInAsync(
                user, request.Password, request.RememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized(new { message = "Неудачная попытка авторизации." });

            return Ok(new { message = "Вы успешно авторизировались." });
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("Пользователь не найден.");

            var result = await userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Почта успешно подтверждена.");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await signInManager.SignOutAsync();
            return Ok("Вы успешно вышли из системы.");
        }
        #endregion

        #region Данные профиля
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var user = await userManager.GetUserAsync(User);

            if (user == null)
                return Unauthorized("Пользователь не авторизован.");

            var response = new CurrentUserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                Email = user.Email,
                AvatarUrl = user.AvatarUrl,
                EmailConfirmed = user.EmailConfirmed,
                IsOnline = user.IsOnline,
                LastOnline = user.LastOnline,
                CreatedAt = user.CreatedAt
            };

            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await userManager.FindByIdAsync(id.ToString());
            if (user == null)
                return NotFound("Пользователь не найден.");

            var response = new OtherUserResponse
            {
                Id = user.Id,
                UserName = user.UserName,
                DisplayName = user.DisplayName,
                AvatarUrl = user.AvatarUrl,
                IsOnline = user.IsOnline,
                LastOnline = user.LastOnline
            };

            return Ok(response);
        }
        #endregion

        #region Обновление данных для авторизации
        [HttpPut("update-username")]
        public async Task<IActionResult> UpdateUsername([FromBody] string newUserName)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized("Пользователь не авторизован.");

            user.UserName = newUserName;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Имя пользователя обновлено.");
        }

        [HttpPut("update-email")]
        public async Task<IActionResult> UpdateEmail([FromBody] string newEmail)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized("Пользователь не авторизован.");

            user.Email = newEmail;
            user.EmailConfirmed = false;
            var result = await userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            await SendConfirmationEmail(user);

            return Ok("Email обновлён. Требуется повторное подтверждение.");
        }

        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            var user = await userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized("Пользователь не авторизован.");

            var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);

            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Пароль успешно изменён.");
        }
        #endregion
    }
}
