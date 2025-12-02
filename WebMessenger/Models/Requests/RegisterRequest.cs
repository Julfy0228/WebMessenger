using System.ComponentModel.DataAnnotations;

namespace WebMessenger.Models.Requests
{
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string Password { get; set; } = string.Empty;

        [Required, MaxLength(50)]
        [RegularExpression(@"^[a-zA-Z0-9_]+$", ErrorMessage = "Username can only contain letters, numbers and underscores")]
        public string UserName { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? DisplayName { get; set; }
    }
}
