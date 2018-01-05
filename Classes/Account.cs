using System.ComponentModel.DataAnnotations;

namespace BlockChain.Classes
{
    public class Account
    {
        [Required]
        [StringLength(6, MinimumLength = 6)]
        public string SortCode { get; set; }

        [Required]
        [StringLength(8, MinimumLength = 8)]
        public string AccountNumber { get; set; }
    }
}
