using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebMessenger.Entities;
using WebMessenger.Models.Requests;

namespace WebMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly UserManager<User> _userManager;

        public ChatController(AppDbContext db, UserManager<User> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var chat = new Chat(request.Name, request.Type);
            _db.Chats.Add(chat);
            await _db.SaveChangesAsync();

            var owner = new Participant
            {
                ChatId = chat.Id,
                UserId = currentUser.Id,
                Role = UserRole.Owner,
                JoinedAt = DateTime.UtcNow
            };
            _db.Participants.Add(owner);

            foreach (var userId in request.ParticipantIds.Distinct())
            {
                if (userId == currentUser.Id) continue;

                var participant = new Participant
                {
                    ChatId = chat.Id,
                    UserId = userId,
                    Role = UserRole.Member,
                    JoinedAt = DateTime.UtcNow
                };
                _db.Participants.Add(participant);
            }

            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(GetChat), new { id = chat.Id }, chat);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetChat(int id)
        {
            var chat = await _db.Chats
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Include(c => c.Messages)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chat == null) return NotFound();
            return Ok(chat);
        }

        [HttpPost("{chatId}/participants")]
        public async Task<IActionResult> AddParticipant(int chatId, [FromBody] AddParticipantRequest request)
        {
            var chat = await _db.Chats
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound("Чат не найден");

            if (chat.Participants.Any(p => p.UserId == request.UserId))
                return BadRequest("Пользователь уже состоит в чате");

            var participant = new Participant
            {
                ChatId = chat.Id,
                UserId = request.UserId,
                Role = request.Role,
                JoinedAt = DateTime.UtcNow
            };

            _db.Participants.Add(participant);
            await _db.SaveChangesAsync();

            return Ok(participant);
        }

    }
}
