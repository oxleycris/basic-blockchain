using System;
using System.Collections.Generic;
using System.Text;

namespace BlockChain.Classes
{
    public class User
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string GivenName { get; set; }

        public string FamilyName { get; set; }

        public string EmailAddress { get; set; }
    }
}
