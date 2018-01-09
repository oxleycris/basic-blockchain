using System;

namespace BlockChain.Models
{
    public class Miner
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid WalletId { get; set; }

        public virtual Wallet Wallet { get; set; }
    }
}
