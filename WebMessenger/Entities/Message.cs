using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace WebMessenger.Entities
{
    public class Message
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ChatId { get; set; }

        [Required]
        public int SenderId { get; set; }

        [MaxLength(5000)]
        public string? Text { get; set; }

        public ICollection<Attachment> Attachments { get; private set; } = [];

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public DateTime? EditedAt { get; set; }
        public bool IsRead { get; private set; }
        public DateTime? ReadAt { get; private set; }

        [JsonIgnore]
        public Chat? Chat { get; set; }

        [JsonIgnore]
        public User? Sender { get; set; }

        public void MarkAsRead()
        {
            IsRead = true;
            ReadAt = DateTime.UtcNow;
        }

        public void Edit(string newText)
        {
            Text = newText;
            EditedAt = DateTime.UtcNow;
        }
    }
}
