using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebMessenger.Entities
{
    public enum ChatType { Group, Private }

    public class Chat
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(100)]
        public string? Name { get; set; }

        [Required]
        public ChatType Type { get; set; }

        public DateTime CreatedAt { get; set; }

        [JsonIgnore]
        public ICollection<Participant> Participants { get; private set; } = [];

        [JsonIgnore]
        public ICollection<Message> Messages { get; private set; } = [];

        public Chat(string name, ChatType type)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Type = type;
            CreatedAt = DateTime.UtcNow;
        }

        private Chat() { }

        public Participant AddParticipant(User user, UserRole role = UserRole.Member)
        {
            if (Participants.Any(p => p.UserId == user.Id))
                throw new InvalidOperationException("User already in chat");

            var participant = new Participant
            {
                ChatId = Id,
                UserId = user.Id,
                Role = role,
                JoinedAt = DateTime.UtcNow
            };

            Participants.Add(participant);
            return participant;
        }
    }
}
