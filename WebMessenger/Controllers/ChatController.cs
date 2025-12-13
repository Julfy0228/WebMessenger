using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WebMessenger.Entities;
using WebMessenger.Hubs;
using WebMessenger.Models.Requests;
using WebMessenger.Models.Responses;

namespace WebMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController(AppDbContext db, UserManager<User> userManager, IHubContext<ChatHub> hubContext) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var chat = new Chat(request.Name, request.Type);
            db.Chats.Add(chat);
            await db.SaveChangesAsync();

            var owner = new Participant
            {
                ChatId = chat.Id,
                UserId = currentUser.Id,
                Role = UserRole.Owner,
                JoinedAt = DateTime.UtcNow
            };
            db.Participants.Add(owner);

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
                db.Participants.Add(participant);
            }

            await db.SaveChangesAsync();

            await hubContext.Clients.All.SendAsync("ChatCreated", chat.Id, chat.Name, chat.Type, chat.CreatedAt);

            var chatDto = new ChatResponse
            {
                Id = chat.Id,
                Name = chat.Name,
                Type = chat.Type,
                CreatedAt = chat.CreatedAt,
                Participants = new List<ParticipantResponse>
                {
                    new ParticipantResponse
                    {
                        UserId = owner.UserId,
                        UserName = currentUser.UserName,
                        DisplayName = currentUser.DisplayName,
                        Role = owner.Role
                    }
                }
            };

            return CreatedAtAction(nameof(GetChat), new { id = chat.Id }, chatDto);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetChat(int id)
        {
            var chat = await db.Chats
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chat == null) return NotFound(new { message = "Чат не найден" });

            var response = new ChatResponse
            {
                Id = chat.Id,
                Name = chat.Name,
                Type = chat.Type,
                CreatedAt = chat.CreatedAt,
                Participants = chat.Participants.Select(p => new ParticipantResponse
                {
                    UserId = p.UserId,
                    UserName = p.User?.UserName,
                    DisplayName = p.User?.DisplayName,
                    Role = p.Role
                }).ToList()
            };

            return Ok(response);
        }

        [HttpPost("{chatId}/participants")]
        public async Task<IActionResult> AddParticipant(int chatId, [FromBody] AddParticipantRequest request)
        {
            var chat = await db.Chats
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound(new { message = "Чат не найден" });

            if (chat.Participants.Any(p => p.UserId == request.UserId))
                return BadRequest(new { message = "Пользователь уже состоит в чате" });

            var participant = new Participant
            {
                ChatId = chat.Id,
                UserId = request.UserId,
                Role = request.Role,
                JoinedAt = DateTime.UtcNow
            };

            db.Participants.Add(participant);
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("ParticipantAdded", chatId, participant.UserId, participant.Role);

            return Ok(new ParticipantResponse
            {
                UserId = participant.UserId,
                UserName = participant.User?.UserName,
                DisplayName = participant.User?.DisplayName,
                Role = participant.Role
            });
        }

        [HttpGet("my")]
        public async Task<IActionResult> GetMyChats()
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var chats = await db.Chats
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .Where(c => c.Participants.Any(p => p.UserId == currentUser.Id))
                .Select(c => new ChatResponse
                {
                    Id = c.Id,
                    Name = c.Name,
                    Type = c.Type,
                    CreatedAt = c.CreatedAt,
                    Participants = c.Participants.Select(p => new ParticipantResponse
                    {
                        UserId = p.UserId,
                        UserName = p.User!.UserName,
                        DisplayName = p.User!.DisplayName,
                        Role = p.Role
                    }).ToList()
                })
                .ToListAsync();

            return Ok(chats);
        }

        #region Messages
        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var chat = await db.Chats
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound(new { message = "Чат не найден" });
            if (!chat.Participants.Any(p => p.UserId == currentUser.Id))
                return StatusCode(403, new { message = "Вы не состоите в этом чате" });

            var messages = await db.Messages
                .Include(m => m.Attachments)
                .Include(m => m.Sender)
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            var response = messages.Select(m => new MessageResponse
            {
                Id = m.Id,
                ChatId = m.ChatId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.UserName,
                Text = m.Text,
                SentAt = m.SentAt,
                EditedAt = m.EditedAt,
                IsRead = m.IsRead,
                ReadAt = m.ReadAt,
                Attachments = m.Attachments.Select(a => new AttachmentResponse
                {
                    Id = a.Id,
                    Type = a.Type,
                    Url = (a as FileAttachment)?.Url ?? (a as LinkAttachment)?.Url,
                    Name = (a as FileAttachment)?.Name,
                    Size = (a as FileAttachment)?.Size,
                    Extension = (a as FileAttachment)?.Extension,
                    Width = (a as ImageAttachment)?.Width ?? (a as VideoAttachment)?.Width,
                    Height = (a as ImageAttachment)?.Height ?? (a as VideoAttachment)?.Height,
                    Duration = (a as AudioAttachment)?.Duration ?? (a as VideoAttachment)?.Duration,
                    Artist = (a as AudioAttachment)?.Artist,
                    Album = (a as AudioAttachment)?.Album,
                    TrackNumber = (a as AudioAttachment)?.TrackNumber,
                    Bitrate = (a as AudioAttachment)?.Bitrate,
                    Latitude = (a as LocationAttachment)?.Latitude,
                    Longitude = (a as LocationAttachment)?.Longitude
                }).ToList()
            }).ToList();

            return Ok(response);
        }

        [HttpPost("{chatId}/messages")]
        public async Task<IActionResult> SendMessage(int chatId, [FromBody] SendMessageRequest request)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null) return NotFound(new { message = "Чат не найден" });
            if (!chat.Participants.Any(p => p.UserId == currentUser.Id))
                return StatusCode(403, new { message = "Вы не состоите в этом чате" });

            using var tx = await db.Database.BeginTransactionAsync();

            var message = new Message
            {
                ChatId = chatId,
                SenderId = currentUser.Id,
                Text = request.Text,
                SentAt = DateTime.UtcNow
            };

            db.Messages.Add(message);
            await db.SaveChangesAsync();

            var attachments = new List<Attachment>();
            if (request.Attachments != null)
            {
                foreach (var att in request.Attachments)
                {
                    if (string.IsNullOrWhiteSpace(att.Url) && att.Type != AttachmentType.Location)
                        continue;

                    Attachment entity = att.Type switch
                    {
                        AttachmentType.Image => new ImageAttachment { MessageId = message.Id, Url = att.Url!, Size = att.Size, Name = att.Name, Width = att.Width ?? 0, Height = att.Height ?? 0 },
                        AttachmentType.Audio => new AudioAttachment { MessageId = message.Id, Url = att.Url!, Size = att.Size, Name = att.Name, Duration = att.Duration ?? 0, Artist = att.Artist, Album = att.Album, TrackNumber = att.TrackNumber, Bitrate = att.Bitrate },
                        AttachmentType.Video => new VideoAttachment { MessageId = message.Id, Url = att.Url!, Size = att.Size, Name = att.Name, Duration = att.Duration ?? 0, Width = att.Width ?? 0, Height = att.Height ?? 0 },
                        AttachmentType.Document => new DocumentAttachment { MessageId = message.Id, Url = att.Url!, Size = att.Size, Name = att.Name },
                        AttachmentType.Link => new LinkAttachment { MessageId = message.Id, Url = att.Url! },
                        AttachmentType.Location => new LocationAttachment { MessageId = message.Id, Latitude = att.Latitude ?? 0, Longitude = att.Longitude ?? 0 },
                        _ => new FileAttachment { MessageId = message.Id, Url = att.Url!, Size = att.Size, Name = att.Name }
                    };

                    attachments.Add(entity);
                    db.Attachments.Add(entity);
                }
                await db.SaveChangesAsync();
            }

            await tx.CommitAsync();

            var response = new MessageResponse
            {
                Id = message.Id,
                ChatId = chatId,
                SenderId = currentUser.Id,
                SenderName = currentUser.UserName,
                Text = message.Text,
                SentAt = message.SentAt,
                Attachments = attachments.Select(a => new AttachmentResponse
                {
                    Id = a.Id,
                    Type = a.Type,
                    Url = (a as FileAttachment)?.Url ?? (a as LinkAttachment)?.Url,
                    Name = (a as FileAttachment)?.Name,
                    Size = (a as FileAttachment)?.Size,
                    Extension = (a as FileAttachment)?.Extension,
                    Width = (a as ImageAttachment)?.Width ?? (a as VideoAttachment)?.Width,
                    Height = (a as ImageAttachment)?.Height ?? (a as VideoAttachment)?.Height,
                    Duration = (a as AudioAttachment)?.Duration ?? (a as VideoAttachment)?.Duration,
                    Artist = (a as AudioAttachment)?.Artist,
                    Album = (a as AudioAttachment)?.Album,
                    TrackNumber = (a as AudioAttachment)?.TrackNumber,
                    Bitrate = (a as AudioAttachment)?.Bitrate,
                    Latitude = (a as LocationAttachment)?.Latitude,
                    Longitude = (a as LocationAttachment)?.Longitude
                }).ToList()
            };

            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("ReceiveMessage", response);

            return Ok(response);
        }

        [HttpPut("{chatId}/messages/{messageId}")]
        public async Task<IActionResult> EditMessage(int chatId, int messageId, [FromBody] EditMessageRequest request)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);

            if (message == null) return NotFound(new { message = "Сообщение не найдено" });
            if (message.SenderId != currentUser.Id) return StatusCode(403, new { message = "Вы можете редактировать только свои сообщения" });

            message.Edit(request.NewText);
            await db.SaveChangesAsync();

            var response = new MessageResponse
            {
                Id = message.Id,
                ChatId = message.ChatId,
                SenderId = message.SenderId,
                SenderName = currentUser.UserName,
                Text = message.Text,
                SentAt = message.SentAt,
                EditedAt = message.EditedAt
            };

            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("MessageEdited", response);

            return Ok(response);
        }

        [HttpDelete("{chatId}/messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int chatId, int messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);
            if (message == null) return NotFound(new { message = "Сообщение не найдено" });
            if (message.SenderId != currentUser.Id) return StatusCode(403, new { message = "Вы можете удалять только свои сообщения" });

            db.Messages.Remove(message);
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("MessageDeleted", messageId);

            return Ok(new { message = "Сообщение удалено", messageId });
        }
        #endregion
    }
}
