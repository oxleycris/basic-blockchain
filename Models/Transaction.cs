using System;

namespace BlockChain.Models
{
    public class Transaction
    {
        public Guid Id { get; } = Guid.NewGuid();

        public Guid SourceWalletId { get; set; }

        public Guid DestinationWalletId { get; set; }

        public decimal TransferedAmount { get; set; }

        public decimal TransferFee { get; set; } = Math.Round(new Random().Next(1, 9999) * 0.000001m, 8);

        public DateTime Timestamp { get; set; }

        public int Size { get; set; } = new Random().Next(1, 8);
    }
}
