using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebMessenger.Entities;
using System.IO;
using WebMessenger.Models.Requests;

namespace WebMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IEmailSender _emailSender;

        public AuthController(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            IEmailSender emailSender)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
        }

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

            await _emailSender.SendEmailAsync(user.Email!, "Confirm your email", html);

            return Ok("Registration successful. Please check your email to confirm.");
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var user = await _userManager.FindByNameAsync(request.Login)
                       ?? await _userManager.FindByEmailAsync(request.Login);

            if (user == null)
                return Unauthorized("Invalid login attempt.");

            var result = await _signInManager.PasswordSignInAsync(
                user, request.Password, request.RememberMe, lockoutOnFailure: false);

            if (!result.Succeeded)
                return Unauthorized("Invalid login attempt.");

            return Ok("Login successful.");
        }

        [HttpGet("confirm-email")]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound("User not found.");

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (!result.Succeeded)
                return BadRequest(result.Errors);

            return Ok("Email confirmed successfully.");
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return Ok("Logged out successfully.");
        }
    }
}
