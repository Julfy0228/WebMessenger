using WebMessenger.Entities;

namespace WebMessenger.Models.Requests
{
    public class AddParticipantRequest
    {
        public int ChatId { get; set; }
        public int UserId { get; set; }
        public UserRole Role { get; set; } = UserRole.Member;
    }
}
