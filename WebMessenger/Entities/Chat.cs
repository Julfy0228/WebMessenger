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

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [JsonIgnore]
        public ICollection<Participant> Participants { get; set; } = [];

        [JsonIgnore]
        public ICollection<Message> Messages { get; set; } = [];
    }
}
