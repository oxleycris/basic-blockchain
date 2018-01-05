using System;

namespace BlockChain.Classes
{
    public class Transaction
    {
        public Transaction()
        {
            Size = new Random().Next(1, 5);
        }

        public Guid SourceWalletId { get; set; }

        public Guid DestinationWalletId { get; set; }

        public decimal TransferedAmount { get; set; }

        public DateTime Timestamp { get; set; }

        public int Size { get; set; } 
    }
}
