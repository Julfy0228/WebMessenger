using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WebMessenger.Entities;
using WebMessenger.Models;

namespace WebMessenger.Controllers
{
    public class HomeController(UserManager<User> userManager) : Controller
    {
        public IActionResult Index()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToAction("Chats");
            return View();
        }

        [Microsoft.AspNetCore.Authorization.Authorize]
        public IActionResult Chats() => View();

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return RedirectToAction("Index", new { error = "InvalidToken" });

            var user = await userManager.FindByIdAsync(userId);
            if (user == null)
                return RedirectToAction("Index", new { error = "UserNotFound" });

            var result = await userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
                return RedirectToAction("Index", new { emailConfirmed = true });

            return RedirectToAction("Index", new { error = "ConfirmationFailed" });
        }

        public IActionResult Privacy() => View();

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error() =>
            View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}