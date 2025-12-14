using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using WebMessenger.Entities;

namespace WebMessenger.Hubs
{
    [Authorize]
    public class ChatHub(UserManager<User> userManager, AppDbContext db) : Hub
    {
        public override async Task OnConnectedAsync()
        {
            var user = await userManager.GetUserAsync(Context.User!);
            if (user != null)
            {
                user.IsOnline = true;
                user.LastOnline = DateTime.UtcNow;
                await db.SaveChangesAsync();

                await Clients.All.SendAsync("UserOnline", user.Id, user.LastOnline);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var user = await userManager.GetUserAsync(Context.User!);
            if (user != null)
            {
                user.IsOnline = false;
                user.LastOnline = DateTime.UtcNow;
                await db.SaveChangesAsync();

                await Clients.All.SendAsync("UserOffline", user.Id, user.LastOnline);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public async Task JoinChat(int chatId)
        {
            var userId = int.Parse(Context.UserIdentifier!);
            var isMember = db.Participants.Any(p => p.ChatId == chatId && p.UserId == userId);

            if (isMember)
                await Groups.AddToGroupAsync(Context.ConnectionId, chatId.ToString());
        }

        public async Task LeaveChat(int chatId) =>
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());
    }
}