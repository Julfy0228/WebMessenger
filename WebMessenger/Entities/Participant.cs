using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebMessenger.Entities
{
    public enum UserRole { Member, Admin, Owner }

    public class Participant
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ChatId { get; set; }

        [Required]
        public int UserId { get; set; }

        [Required]
        public UserRole Role { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public bool IsMuted { get; set; }

        [JsonIgnore]
        public Chat? Chat { get; set; }

        [JsonIgnore]
        public User? User { get; set; }
    }
}
