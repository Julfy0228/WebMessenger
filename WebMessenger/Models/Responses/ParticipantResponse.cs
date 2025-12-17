using WebMessenger.Entities;

namespace WebMessenger.Models.Responses
{
    public class ParticipantResponse
    {
        public int UserId { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public UserRole Role { get; set; }
        public bool IsMuted { get; set; }
    }
}
