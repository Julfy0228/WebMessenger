using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using WebMessenger.Entities;
using WebMessenger.Hubs;
using WebMessenger.Models.Requests;
using WebMessenger.Models.Responses;

namespace WebMessenger.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ChatController(
        AppDbContext db,
        UserManager<User> userManager,
        IHubContext<ChatHub> hubContext,
        IWebHostEnvironment env) : ControllerBase
    {
        [HttpPost]
        public async Task<IActionResult> CreateChat([FromBody] CreateChatRequest request)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            if (request.Type == ChatType.Private && request.ParticipantIds.Count == 1)
            {
                var targetUserId = request.ParticipantIds.First();
                if (targetUserId == currentUser.Id)
                    return BadRequest(new { message = "Нельзя создать чат с самим собой" });

                var existingChat = await db.Chats
                    .Where(c => c.Type == ChatType.Private)
                    .Where(c => c.Participants.Any(p => p.UserId == currentUser.Id))
                    .Where(c => c.Participants.Any(p => p.UserId == targetUserId))
                    .Include(c => c.Participants).ThenInclude(p => p.User)
                    .FirstOrDefaultAsync();

                if (existingChat != null)
                    return Ok(MapToResponse(existingChat));
            }

            var chat = new Chat
            {
                Name = request.Name,
                Type = request.Type
            };
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

            await hubContext.Clients.All.SendAsync("ChatCreated");

            await db.Entry(chat).Collection(c => c.Participants).Query().Include(p => p.User).LoadAsync();

            return CreatedAtAction(nameof(GetChat), new { id = chat.Id }, MapToResponse(chat));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetChat(int id)
        {
            var chat = await db.Chats
                .Include(c => c.Participants).ThenInclude(p => p.User)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (chat == null) return NotFound(new { message = "Чат не найден" });

            return Ok(MapToResponse(chat));
        }

        [HttpPost("{chatId}/participants")]
        public async Task<IActionResult> AddParticipant(int chatId, [FromBody] AddParticipantRequest request)
        {
            if (request.Role == UserRole.Owner)
                return BadRequest(new { message = "Нельзя назначить роль Owner через этот метод" });

            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);
            if (chat == null) return NotFound(new { message = "Чат не найден" });

            if (chat.Type == ChatType.Private)
                return BadRequest(new { message = "Нельзя добавлять участников в приватный чат" });

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
                    AvatarUrl = c.AvatarUrl,
                    Type = c.Type,
                    CreatedAt = c.CreatedAt,
                    Participants = c.Participants.Select(p => new ParticipantResponse
                    {
                        UserId = p.UserId,
                        UserName = p.User!.UserName,
                        DisplayName = p.User!.DisplayName,
                        Role = p.Role
                    }).ToList(),
                    LastMessage = db.Messages
                        .Where(m => m.ChatId == c.Id)
                        .OrderByDescending(m => m.SentAt)
                        .Select(m => new LastMessageResponse
                        {
                            Id = m.Id,
                            SenderId = m.SenderId,
                            SenderName = m.Sender!.UserName,
                            Text = m.Text,
                            SentAt = m.SentAt,
                            AttachmentsCount = m.Attachments.Count()
                        }).FirstOrDefault()
                }).ToListAsync();

            return Ok(chats);
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
            if (request.Attachments != null && request.Attachments.Any())
            {
                var uploadPath = Path.Combine(env.WebRootPath, "uploads");
                if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

                foreach (var att in request.Attachments)
                {
                    string finalUrl = att.Url ?? "";
                    long fileSize = att.Size ?? 0;

                    if (!string.IsNullOrEmpty(att.Url) && att.Url.StartsWith("data:"))
                    {
                        try
                        {
                            var match = Regex.Match(att.Url, @"data:(?<type>.+?);base64,(?<data>.+)");
                            if (match.Success)
                            {
                                var base64Data = match.Groups["data"].Value;
                                var contentType = match.Groups["type"].Value;
                                var extension = GetExtension(contentType);
                                var fileName = $"{Guid.NewGuid()}{extension}";
                                var filePath = Path.Combine(uploadPath, fileName);

                                var bytes = Convert.FromBase64String(base64Data);
                                await System.IO.File.WriteAllBytesAsync(filePath, bytes);

                                finalUrl = $"/uploads/{fileName}";
                                fileSize = bytes.Length;
                            }
                        }
                        catch
                        {
                            continue;
                        }
                    }

                    Attachment entity = att.Type switch
                    {
                        AttachmentType.Image => new ImageAttachment { MessageId = message.Id, Url = finalUrl, Size = fileSize, Name = att.Name, Width = att.Width ?? 0, Height = att.Height ?? 0 },
                        _ => new FileAttachment { MessageId = message.Id, Url = finalUrl, Size = fileSize, Name = att.Name }
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
                Attachments = attachments.Select(MapAttachment).ToList()
            };

            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("ReceiveMessage", response);

            return Ok(response);
        }

        [HttpGet("{chatId}/messages")]
        public async Task<IActionResult> GetMessages(int chatId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            var messages = await db.Messages
                .Include(m => m.Attachments)
                .Include(m => m.Sender)
                .Where(m => m.ChatId == chatId)
                .OrderBy(m => m.SentAt)
                .ToListAsync();

            return Ok(messages.Select(m => new MessageResponse
            {
                Id = m.Id,
                ChatId = m.ChatId,
                SenderId = m.SenderId,
                SenderName = m.Sender?.UserName,
                Text = m.Text,
                SentAt = m.SentAt,
                Attachments = m.Attachments.Select(MapAttachment).ToList()
            }));
        }

        [HttpDelete("{chatId}/messages/{messageId}")]
        public async Task<IActionResult> DeleteMessage(int chatId, int messageId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var message = await db.Messages.FirstOrDefaultAsync(m => m.Id == messageId && m.ChatId == chatId);
            if (message == null) return NotFound(new { message = "Сообщение не найдено" });

            bool isSender = message.SenderId == currentUser.Id;

            bool isAdminOrOwner = false;

            var participant = await db.Participants
                .FirstOrDefaultAsync(p => p.ChatId == chatId && p.UserId == currentUser.Id);

            if (participant != null && (participant.Role == UserRole.Owner || participant.Role == UserRole.Admin))
            {
                isAdminOrOwner = true;
            }

            if (!isSender && !isAdminOrOwner)
                return StatusCode(403, new { message = "Вы не можете удалить это сообщение" });

            db.Messages.Remove(message);
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString())
                .SendAsync("MessageDeleted", messageId);

            return Ok(new { message = "Сообщение удалено", messageId });
        }

        [HttpPost("{chatId}/transfer-ownership")]
        public async Task<IActionResult> TransferOwnership(int chatId, [FromBody] TransferOwnerRequest req)
        {
            var currentUser = await userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized(new { message = "Вы не авторизированы" });

            var chat = await db.Chats
                .Include(c => c.Participants)
                .FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound(new { message = "Чат не найден" });

            var caller = chat.Participants.FirstOrDefault(p => p.UserId == currentUser.Id);
            if (caller == null || caller.Role != UserRole.Owner)
                return StatusCode(403, new { message = "Только владелец может передавать права" });

            var newOwner = chat.Participants.FirstOrDefault(p => p.UserId == req.NewOwnerId);
            if (newOwner == null)
                return BadRequest(new { message = "Пользователь не найден в этом чате" });

            if (newOwner.UserId == currentUser.Id)
                return BadRequest(new { message = "Вы уже являетесь владельцем" });

            caller.Role = UserRole.Member;
            newOwner.Role = UserRole.Owner;

            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString()).SendAsync("OwnershipTransferred", chatId);

            return Ok(new { message = "Права успешно переданы" });
        }

        [HttpDelete("{chatId}")]
        public async Task<IActionResult> DeleteChat(int chatId)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound();

            var participant = chat.Participants.FirstOrDefault(p => p.UserId == currentUser!.Id);
            if (participant == null || participant.Role != UserRole.Owner)
                return StatusCode(403, new { message = "Только владелец может удалить чат" });

            db.Chats.Remove(chat);
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString()).SendAsync("ChatDeleted", chatId);

            return Ok(new { message = "Чат удален" });
        }

        [HttpPost("{chatId}/promote")]
        public async Task<IActionResult> PromoteToAdmin(int chatId, [FromBody] ChatActionRequest req)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound();

            var caller = chat.Participants.FirstOrDefault(p => p.UserId == currentUser!.Id);
            if (caller == null || caller.Role != UserRole.Owner) return Forbid();

            var target = chat.Participants.FirstOrDefault(p => p.UserId == req.UserId);
            if (target == null) return BadRequest(new { message = "Пользователь не найден" });

            target.Role = UserRole.Admin;
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString()).SendAsync("RoleUpdated", chatId);
            return Ok();
        }

        [HttpPost("{chatId}/demote")]
        public async Task<IActionResult> DemoteFromAdmin(int chatId, [FromBody] ChatActionRequest req)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound();

            var caller = chat.Participants.FirstOrDefault(p => p.UserId == currentUser!.Id);
            if (caller == null || caller.Role != UserRole.Owner) return Forbid();

            var target = chat.Participants.FirstOrDefault(p => p.UserId == req.UserId);
            if (target == null) return BadRequest(new { message = "Пользователь не найден" });

            if (target.Role != UserRole.Admin)
                return BadRequest(new { message = "Пользователь не является администратором" });

            target.Role = UserRole.Member;
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString()).SendAsync("RoleUpdated", chatId);
            return Ok();
        }

        [HttpPost("{chatId}/kick")]
        public async Task<IActionResult> KickParticipant(int chatId, [FromBody] ChatActionRequest req)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound();

            var caller = chat.Participants.FirstOrDefault(p => p.UserId == currentUser!.Id);
            var target = chat.Participants.FirstOrDefault(p => p.UserId == req.UserId);

            if (caller == null || target == null) return BadRequest();

            bool canKick = false;

            if (caller.Role == UserRole.Owner)
            {
                if (target.UserId != currentUser!.Id) canKick = true;
            }
            else if (caller.Role == UserRole.Admin)
            {
                if (target.Role == UserRole.Member) canKick = true;
            }

            if (!canKick) return StatusCode(403, new { message = "Недостаточно прав для исключения этого участника" });

            db.Participants.Remove(target);
            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString()).SendAsync("UserKicked", chatId, req.UserId);
            return Ok();
        }

        [HttpPost("{chatId}/avatar")]
        public async Task<IActionResult> UploadChatAvatar(int chatId, IFormFile file, [FromServices] IWebHostEnvironment env)
        {
            var currentUser = await userManager.GetUserAsync(User);
            var chat = await db.Chats.Include(c => c.Participants).FirstOrDefaultAsync(c => c.Id == chatId);

            if (chat == null) return NotFound(new { message = "Чат не найден" });

            var participant = chat.Participants.FirstOrDefault(p => p.UserId == currentUser!.Id);

            if (participant == null || (participant.Role != UserRole.Owner && participant.Role != UserRole.Admin))
                return StatusCode(403, new { message = "Нет прав на смену аватара чата" });

            if (file == null || file.Length == 0) return BadRequest("Файл не выбран");

            var uploadPath = Path.Combine(env.WebRootPath, "uploads", "avatars");
            if (!Directory.Exists(uploadPath)) Directory.CreateDirectory(uploadPath);

            var ext = Path.GetExtension(file.FileName);
            var fileName = $"chat_{chat.Id}_{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            var relativeUrl = $"/uploads/avatars/{fileName}";
            chat.AvatarUrl = relativeUrl;

            await db.SaveChangesAsync();

            await hubContext.Clients.Group(chatId.ToString()).SendAsync("ChatAvatarUpdated", chatId, relativeUrl);

            return Ok(new { avatarUrl = relativeUrl });
        }

        private ChatResponse MapToResponse(Chat chat)
        {
            return new ChatResponse
            {
                Id = chat.Id,
                Name = chat.Name,
                AvatarUrl = chat.AvatarUrl,
                Type = chat.Type,
                CreatedAt = chat.CreatedAt,
                Participants = [.. chat.Participants.Select(p => new ParticipantResponse
                {
                    UserId = p.UserId,
                    UserName = p.User?.UserName,
                    DisplayName = p.User?.DisplayName,
                    Role = p.Role
                })]
            };
        }

        private AttachmentResponse MapAttachment(Attachment a)
        {
            return new AttachmentResponse
            {
                Id = a.Id,
                Type = a.Type,
                Url = (a as FileAttachment)?.Url ?? (a as LinkAttachment)?.Url,
                Name = (a as FileAttachment)?.Name,
                Size = (a as FileAttachment)?.Size,
                Extension = (a as FileAttachment)?.Extension,
            };
        }

        private string GetExtension(string contentType)
        {
            return contentType switch
            {
                "image/jpeg" => ".jpg",
                "image/png" => ".png",
                "image/gif" => ".gif",
                "application/pdf" => ".pdf",
                "text/plain" => ".txt",
                "audio/mpeg" => ".mp3",
                "video/mp4" => ".mp4",
                _ => ".bin"
            };
        }
    }
}