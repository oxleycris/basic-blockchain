using System;

namespace BlockChain.Models
{
    public class User
    {
        public Guid Id { get; } = Guid.NewGuid();

        public string GivenName { get; set; }

        public string FamilyName { get; set; }

        public string EmailAddress { get; set; }
    }
}
