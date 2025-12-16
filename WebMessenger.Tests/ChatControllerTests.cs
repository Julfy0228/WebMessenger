using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Security.Claims;
using WebMessenger.Controllers;
using WebMessenger.Entities;
using WebMessenger.Hubs;
using WebMessenger.Models.Requests;
using WebMessenger.Models.Responses;

namespace WebMessenger.Tests
{
    [TestFixture]
    public class ChatControllerTests
    {
        private AppDbContext _context;
        private SqliteConnection _connection;
        private Mock<UserManager<User>> _mockUserManager;
        private Mock<IHubContext<ChatHub>> _mockHubContext;
        private Mock<IClientProxy> _mockClientProxy;
        private Mock<IWebHostEnvironment> _mockEnvironment;
        private ChatController _controller;

        [SetUp]
        public void Setup()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options;
            _context = new AppDbContext(options);
            _context.Database.EnsureCreated();

            var store = new Mock<IUserStore<User>>();
            _mockUserManager = new Mock<UserManager<User>>(store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

            _mockHubContext = new Mock<IHubContext<ChatHub>>();
            _mockClientProxy = new Mock<IClientProxy>();
            var mockClients = new Mock<IHubClients>();
            mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);
            mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);
            _mockHubContext.Setup(h => h.Clients).Returns(mockClients.Object);

            _mockEnvironment = new Mock<IWebHostEnvironment>();
            _mockEnvironment.Setup(m => m.WebRootPath).Returns(Directory.GetCurrentDirectory());

            _controller = new ChatController(_context, _mockUserManager.Object, _mockHubContext.Object, _mockEnvironment.Object);
        }

        [TearDown]
        public void TearDown()
        {
            _context.Dispose();
            _connection.Close();
        }

        private void AuthenticateUser(int userId)
        {
            var user = new User { Id = userId, UserName = $"User{userId}", DisplayName = $"Display{userId}" };
            if (!_context.Users.Any(u => u.Id == userId))
            {
                _context.Users.Add(user);
                _context.SaveChanges();
            }

            var principal = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }));
            _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } };
            _mockUserManager.Setup(x => x.GetUserAsync(principal)).ReturnsAsync(user);
        }

        [Test]
        public async Task CreateChat_Group_ReturnsCreated()
        {
            AuthenticateUser(1);
            var req = new CreateChatRequest { Name = "G1", Type = ChatType.Group };

            var result = await _controller.CreateChat(req);

            Assert.That(result, Is.InstanceOf<CreatedAtActionResult>());
            Assert.That(_context.Chats.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task CreateChat_Private_ReturnsExisting_IfDuplicate()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2, UserName = "U2" }; _context.Users.Add(user2);
            var chat = new Chat { Name = "P1", Type = ChatType.Private };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.AddRange(
                new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner },
                new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Member }
            );
            _context.SaveChanges();

            var req = new CreateChatRequest { Name = "New P1", Type = ChatType.Private, ParticipantIds = new List<int> { 2 } };

            var result = await _controller.CreateChat(req);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_context.Chats.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task GetChat_ValidId_ReturnsChat()
        {
            var chat = new Chat { Name = "C1", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            var result = await _controller.GetChat(chat.Id);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
        }

        [Test]
        public async Task AddParticipant_Group_Success()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2 }; _context.Users.Add(user2);
            var chat = new Chat { Name = "G1", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            var req = new AddParticipantRequest { UserId = 2, Role = UserRole.Member };

            var result = await _controller.AddParticipant(chat.Id, req);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_context.Participants.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task GetMyChats_ReturnsOnlyUserChats()
        {
            AuthenticateUser(1);
            var chat1 = new Chat { Name = "My", Type = ChatType.Group };
            var chat2 = new Chat { Name = "NotMy", Type = ChatType.Group };
            _context.Chats.AddRange(chat1, chat2);
            _context.SaveChanges();
            _context.Participants.Add(new Participant { ChatId = chat1.Id, UserId = 1, Role = UserRole.Member });
            _context.SaveChanges();

            var result = await _controller.GetMyChats();

            var list = (result as OkObjectResult)!.Value as List<ChatResponse>;
            Assert.That(list!.Count, Is.EqualTo(1));
            Assert.That(list[0].Name, Is.EqualTo("My"));
        }

        [Test]
        public async Task SendMessage_InChat_SavesMessage()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "C1", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Member });
            _context.SaveChanges();

            var req = new SendMessageRequest { Text = "Hello" };

            var result = await _controller.SendMessage(chat.Id, req);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_context.Messages.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task GetMessages_ReturnsList()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "C1", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Member });
            _context.SaveChanges();
            _context.Messages.Add(new Message { ChatId = chat.Id, SenderId = 1, Text = "M1" });
            _context.SaveChanges();

            var result = await _controller.GetMessages(chat.Id);

            var list = (result as OkObjectResult)!.Value as IEnumerable<MessageResponse>;
            Assert.That(list!.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task DeleteMessage_Sender_Success()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "C1", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            var msg = new Message { ChatId = chat.Id, SenderId = 1, Text = "Del" };
            _context.Messages.Add(msg);
            _context.SaveChanges();

            var result = await _controller.DeleteMessage(chat.Id, msg.Id);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_context.Messages.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task TransferOwnership_Owner_Success()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2 }; _context.Users.Add(user2);
            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.AddRange(
                new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner },
                new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Member }
            );
            _context.SaveChanges();

            var result = await _controller.TransferOwnership(chat.Id, new TransferOwnerRequest { NewOwnerId = 2 });

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            var p1 = await _context.Participants.FirstAsync(p => p.UserId == 1);
            Assert.That(p1.Role, Is.EqualTo(UserRole.Member));
        }

        [Test]
        public async Task DeleteChat_Owner_Success()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "Del", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner });
            _context.SaveChanges();

            var result = await _controller.DeleteChat(chat.Id);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());
            Assert.That(_context.Chats.Count(), Is.EqualTo(0));
        }

        [Test]
        public async Task PromoteToAdmin_Owner_Success()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2 }; _context.Users.Add(user2);
            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.AddRange(
                new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner },
                new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Member }
            );
            _context.SaveChanges();

            var result = await _controller.PromoteToAdmin(chat.Id, new ChatActionRequest { UserId = 2 });

            Assert.That(result, Is.InstanceOf<OkResult>());
            var p2 = await _context.Participants.FirstAsync(p => p.UserId == 2);
            Assert.That(p2.Role, Is.EqualTo(UserRole.Admin));
        }

        [Test]
        public async Task DemoteFromAdmin_Owner_Success()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2 }; _context.Users.Add(user2);
            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.AddRange(
                new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner },
                new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Admin }
            );
            _context.SaveChanges();

            var result = await _controller.DemoteFromAdmin(chat.Id, new ChatActionRequest { UserId = 2 });

            Assert.That(result, Is.InstanceOf<OkResult>());
            var p2 = await _context.Participants.FirstAsync(p => p.UserId == 2);
            Assert.That(p2.Role, Is.EqualTo(UserRole.Member));
        }

        [Test]
        public async Task KickParticipant_OwnerKicksMember_Success()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2 }; _context.Users.Add(user2);
            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.AddRange(
                new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner },
                new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Member }
            );
            _context.SaveChanges();

            var result = await _controller.KickParticipant(chat.Id, new ChatActionRequest { UserId = 2 });

            Assert.That(result, Is.InstanceOf<OkResult>());
            Assert.That(_context.Participants.Count(), Is.EqualTo(1));
        }

        [Test]
        public async Task UploadChatAvatar_Owner_Success()
        {
            AuthenticateUser(1);

            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner });
            _context.SaveChanges();

            var fileMock = new Mock<IFormFile>();
            fileMock.Setup(f => f.Length).Returns(100);
            fileMock.Setup(f => f.FileName).Returns("test.jpg");

            var result = await _controller.UploadChatAvatar(chat.Id, fileMock.Object, _mockEnvironment.Object);

            Assert.That(result, Is.InstanceOf<OkObjectResult>());

            var updatedChat = await _context.Chats.FirstAsync();

            Assert.That(updatedChat.AvatarUrl, Does.StartWith("/uploads/avatars/chat_"));
            Assert.That(updatedChat.AvatarUrl, Does.EndWith(".jpg"));
        }

        [Test]
        public async Task CreateChat_WithSelf_ReturnsBadRequest()
        {
            AuthenticateUser(1);
            var req = new CreateChatRequest { Name = "P", Type = ChatType.Private, ParticipantIds = new List<int> { 1 } };

            var result = await _controller.CreateChat(req);

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var badReq = result as BadRequestObjectResult;
            Assert.That(badReq!.Value!.ToString(), Does.Contain("самим собой"));
        }

        [Test]
        public async Task AddParticipant_ToPrivateChat_ReturnsBadRequest()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "P", Type = ChatType.Private };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            var req = new AddParticipantRequest { UserId = 2 };

            var result = await _controller.AddParticipant(chat.Id, req);

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
            var val = (result as BadRequestObjectResult)!.Value;
            Assert.That(val!.ToString(), Does.Contain("Нельзя добавлять"));
        }

        [Test]
        public async Task AddParticipant_UserAlreadyExists_ReturnsBadRequest()
        {
            AuthenticateUser(1);

            var user2 = new User { Id = 2, UserName = "User2" };
            _context.Users.Add(user2);

            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Member });
            _context.SaveChanges();

            var req = new AddParticipantRequest { UserId = 2 };

            var result = await _controller.AddParticipant(chat.Id, req);

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }

        [Test]
        public async Task KickParticipant_MemberTriesToKick_ReturnsForbidden()
        {
            AuthenticateUser(1);
            var user2 = new User { Id = 2 }; _context.Users.Add(user2);

            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            _context.Participants.AddRange(
                new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Member },
                new Participant { ChatId = chat.Id, UserId = 2, Role = UserRole.Member }
            );
            _context.SaveChanges();

            var result = await _controller.KickParticipant(chat.Id, new ChatActionRequest { UserId = 2 });

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task DeleteMessage_NotSenderAndNotAdmin_ReturnsForbidden()
        {
            AuthenticateUser(1);

            var user5 = new User { Id = 5, UserName = "Author" };
            _context.Users.Add(user5);

            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Member });

            var msg = new Message { ChatId = chat.Id, SenderId = 5, Text = "Not mine" };
            _context.Messages.Add(msg);
            _context.SaveChanges();

            var result = await _controller.DeleteMessage(chat.Id, msg.Id);

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task DeleteChat_NotOwner_ReturnsForbidden()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();

            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Admin });
            _context.SaveChanges();

            var result = await _controller.DeleteChat(chat.Id);

            Assert.That(result, Is.InstanceOf<ObjectResult>());
            Assert.That(((ObjectResult)result).StatusCode, Is.EqualTo(403));
        }

        [Test]
        public async Task TransferOwnership_TargetUserNotInChat_ReturnsBadRequest()
        {
            AuthenticateUser(1);
            var chat = new Chat { Name = "G", Type = ChatType.Group };
            _context.Chats.Add(chat);
            _context.SaveChanges();
            _context.Participants.Add(new Participant { ChatId = chat.Id, UserId = 1, Role = UserRole.Owner });
            _context.SaveChanges();

            var req = new TransferOwnerRequest { NewOwnerId = 999 };

            var result = await _controller.TransferOwnership(chat.Id, req);

            Assert.That(result, Is.InstanceOf<BadRequestObjectResult>());
        }
    }
}