using WebMessenger.Entities;

namespace WebMessenger.Models.Responses
{
    public class ChatResponse
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ChatType Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<ParticipantResponse> Participants { get; set; } = [];
        public LastMessageResponse? LastMessage { get; set; }
    }
}
