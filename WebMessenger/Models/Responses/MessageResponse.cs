namespace WebMessenger.Models.Responses
{
    public class MessageResponse
    {
        public int Id { get; set; }
        public int ChatId { get; set; }
        public int? SenderId { get; set; }
        public string? SenderName { get; set; }
        public string? SenderDisplayName { get; set; }
        public string? SenderAvatarUrl { get; set; }
        public string? Text { get; set; }
        public DateTime SentAt { get; set; }
        public DateTime? EditedAt { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }

        public List<AttachmentResponse>? Attachments { get; set; }
    }
}