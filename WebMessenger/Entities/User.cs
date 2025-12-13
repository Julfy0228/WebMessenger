using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebMessenger.Entities
{
    public class User : IdentityUser<int>
    {
        [MaxLength(50)]
        public string? DisplayName { get; set; }

        public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
        public DateTime LastOnline { get; private set; }
        public bool IsOnline { get; private set; }

        public void UpdateOnline(bool isOnline = true)
        {
            IsOnline = isOnline;
            if (!IsOnline)
                LastOnline = DateTime.UtcNow;
        }

        [MaxLength(2048)]
        public string? AvatarUrl { get; set; }

        [JsonIgnore]
        public ICollection<Participant> Chats { get; private set; } = [];

        [JsonIgnore]
        public ICollection<Message> Messages { get; private set; } = [];
    }
}
