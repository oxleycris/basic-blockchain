using System;
using System.Collections.Generic;
using System.Reflection.Metadata;
using System.Text;

namespace BlockChain.Classes
{
    public class Wallet
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public decimal Balance { get; set; }

        public Guid UserId { get; set; }
    }
}
