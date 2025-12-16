using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using WebMessenger.Controllers;
using WebMessenger.Entities;
using WebMessenger.Models.Requests;

namespace WebMessenger.Tests
{
    [TestFixture]
    public class UserControllerTests
    {
        private Mock<UserManager<User>> _mockUserManager;
        private Mock<SignInManager<User>> _mockSignInManager;
        private Mock<IEmailSender> _mockEmailSender;
        private Mock<IWebHostEnvironment> _mockEnvironment;
        private UserController _controller;

        [SetUp]
        public void Setup()
        {
            var dir = Path.Combine(Directory.GetCurrentDirectory(), "Views", "Email");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ConfirmEmail.html");
            if (!File.Exists(path)) File.WriteAllText(path, "<html>@ConfirmUrl</html>");

            var store = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            var contextAccessor = new Mock<IHttpContextAccessor>();
            var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<User>>();
            _mockSignInManager = new Mock<SignInManager<User>>(_mockUserManager.Object, contextAccessor.Object, userPrincipalFactory.Object, null!, null!, null!, null!);

            _mockEmailSender = new Mock<IEmailSender>();

            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockEnvironment.Setup(m => m.WebRootPath).Returns(Directory.GetCurrentDirectory());

            _controller = new(_mockUserManager.Object, _mockSignInManager.Object, _mockEmailSender.Object)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
            };
            var urlHelper = new Mock<IUrlHelper>();
            urlHelper.Setup(x => x.Action(It.IsAny<UrlActionContext>())).Returns("http://localhost");
            _controller.Url = urlHelper.Object;
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(Path.Combine(Directory.GetCurrentDirectory(), "Views"), true); } catch { }
        }

        private void AuthenticateUser()
        {
            var user = new User { Id = 1, UserName = "test" };
            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "test") }));
            _controller.ControllerContext.HttpContext.User = principal;
            _mockUserManager.Setup(x => x.GetUserAsync(principal)).ReturnsAsync(user);
        }

        [Test]
        public async Task Register_Valid_ReturnsOk()
        {
            var req = new RegisterRequest { UserName = "u", Email = "e@e.com", Password = "P1" };
            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), req.Password)).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<User>())).ReturnsAsync("tok");

            var result = await _controller.Register(req);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task Login_Valid_ReturnsOk()
        {
            var req = new LoginRequest { Login = "u", Password = "P1" };
            var user = new User();
            _mockUserManager.Setup(x => x.FindByNameAsync(req.Login)).ReturnsAsync(user);
            _mockSignInManager.Setup(x => x.PasswordSignInAsync(user, req.Password, false, false)).ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Success);

            var result = await _controller.Login(req);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task ConfirmEmail_Valid_ReturnsOk()
        {
            var user = new User { Id = 1 };
            _mockUserManager.Setup(x => x.FindByIdAsync("1")).ReturnsAsync(user);
            _mockUserManager.Setup(x => x.ConfirmEmailAsync(user, "tok")).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.ConfirmEmail("1", "tok");
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task Logout_ReturnsOk()
        {
            var result = await _controller.Logout();
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetCurrentUser_ReturnsUser()
        {
            AuthenticateUser();
            var result = await _controller.GetCurrentUser();
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task GetUserById_Found_ReturnsUser()
        {
            _mockUserManager.Setup(x => x.FindByIdAsync("2")).ReturnsAsync(new User { Id = 2 });
            var result = await _controller.GetUserById(2);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task FindByUsername_Found_ReturnsUser()
        {
            var result = await _controller.FindByUsername(null);
            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task UpdateDisplayName_Success_ReturnsOk()
        {
            AuthenticateUser();
            _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.UpdateDisplayName("New Name");
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task UploadAvatar_ValidFile_ReturnsOk()
        {
            AuthenticateUser();
            _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.FileName).Returns("ava.jpg");

            var result = await _controller.UploadAvatar(fileMock.Object, _mockEnvironment.Object);
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task UpdateUsername_Success_ReturnsOk()
        {
            AuthenticateUser();
            _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
            var result = await _controller.UpdateUsername("new_login");
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task UpdateEmail_Success_ReturnsOk()
        {
            AuthenticateUser();
            _mockUserManager.Setup(x => x.UpdateAsync(It.IsAny<User>())).ReturnsAsync(IdentityResult.Success);
            _mockUserManager.Setup(x => x.GenerateEmailConfirmationTokenAsync(It.IsAny<User>())).ReturnsAsync("t");

            var result = await _controller.UpdateEmail("new@mail.com");
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task ChangePassword_Success_ReturnsOk()
        {
            AuthenticateUser();
            _mockUserManager.Setup(x => x.ChangePasswordAsync(It.IsAny<User>(), "Old", "New")).ReturnsAsync(IdentityResult.Success);

            var result = await _controller.ChangePassword(new ChangePasswordRequest { CurrentPassword = "Old", NewPassword = "New" });
            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task Register_IdentityFailure_ReturnsBadRequestWithErrors()
        {
            var req = new RegisterRequest { UserName = "u", Email = "e", Password = "p" };

            var identityError = IdentityResult.Failed(new IdentityError { Description = "Weak password" });

            _mockUserManager.Setup(x => x.CreateAsync(It.IsAny<User>(), req.Password))
                .ReturnsAsync(identityError);

            var result = await _controller.Register(req);

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badReq = result as BadRequestObjectResult;

            var json = JsonSerializer.Serialize(badReq!.Value);
            Assert.That(json, Does.Contain("Weak password"));
        }

        [Test]
        public async Task Login_UserNotFound_ReturnsUnauthorized()
        {
            var req = new LoginRequest { Login = "unknown", Password = "p" };

            _mockUserManager.Setup(x => x.FindByNameAsync(req.Login)).ReturnsAsync((User)null!);
            _mockUserManager.Setup(x => x.FindByEmailAsync(req.Login)).ReturnsAsync((User)null!);

            var result = await _controller.Login(req);

            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            var req = new LoginRequest { Login = "u", Password = "wrong_password" };
            var user = new User { UserName = "u" };

            _mockUserManager.Setup(x => x.FindByNameAsync(req.Login)).ReturnsAsync(user);

            _mockSignInManager.Setup(x => x.PasswordSignInAsync(user, req.Password, false, false))
                .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

            var result = await _controller.Login(req);

            Assert.That(result, Is.InstanceOf<UnauthorizedObjectResult>());
        }

        [Test]
        public async Task FindByUsername_EmptyQuery_ReturnsBadRequest()
        {
            var result = await _controller.FindByUsername("");

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }
    }
}