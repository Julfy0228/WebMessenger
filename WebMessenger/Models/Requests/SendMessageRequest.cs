namespace WebMessenger.Models.Requests
{
    public class SendMessageRequest
    {
        public string Text { get; set; } = string.Empty;

        public List<AttachmentRequest>? Attachments { get; set; }
    }
}
