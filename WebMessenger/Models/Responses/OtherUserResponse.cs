namespace WebMessenger.Models.Responses
{
    public class OtherUserResponse
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? DisplayName { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsOnline { get; set; }
        public DateTime LastOnline { get; set; }
    }
}
