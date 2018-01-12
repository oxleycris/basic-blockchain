using System;
using System.ComponentModel.DataAnnotations;

namespace BlockChain.Models
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string GivenName { get; set; }

        [Required]
        public string FamilyName { get; set; }
    }
}
