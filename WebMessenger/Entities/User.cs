using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebMessenger.Entities
{
    public class User : IdentityUser<int>
    {
        [MaxLength(50)]
        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastOnline { get; set; }
        public bool IsOnline { get; set; }

        [MaxLength(2048)]
        public string? AvatarUrl { get; set; }

        [JsonIgnore]
        public ICollection<Participant> Chats { get; set; } = [];

        [JsonIgnore]
        public ICollection<Message> Messages { get; set; } = [];
    }
}
