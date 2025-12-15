using System.ComponentModel.DataAnnotations;

namespace WebMessenger.Models.Requests
{
    public class TransferOwnerRequest
    {
        [Required]
        public int NewOwnerId { get; set; }
    }
}
