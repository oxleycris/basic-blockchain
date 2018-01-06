using System;

namespace BlockChain.Models
{
    public class Wallet
    {
        public Guid Id { get; } = Guid.NewGuid();

        public decimal Balance { get; set; }

        public Guid UserId { get; set; }
    }
}
