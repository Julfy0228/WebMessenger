using WebMessenger.Entities;

namespace WebMessenger.Models.Requests
{
    public class CreateChatRequest
    {
        public string Name { get; set; } = string.Empty;
        public ChatType Type { get; set; }

        public List<int> ParticipantIds { get; set; } = [];
    }
}
