using System;

namespace BlockChain.Models
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string GivenName { get; set; }

        public string FamilyName { get; set; }
    }
}
