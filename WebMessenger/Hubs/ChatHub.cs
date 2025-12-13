using Microsoft.AspNetCore.SignalR;

namespace WebMessenger.Hubs
{
    public class ChatHub : Hub
    {
        public async Task JoinChat(int chatId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, chatId.ToString());
        }

        public async Task LeaveChat(int chatId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, chatId.ToString());
        }

        public async Task UserOnline(int userId)
        {
            await Clients.All.SendAsync("UserOnline", userId, DateTime.UtcNow);
        }

        public async Task UserOffline(int userId)
        {
            await Clients.All.SendAsync("UserOffline", userId, DateTime.UtcNow);
        }
    }
}
