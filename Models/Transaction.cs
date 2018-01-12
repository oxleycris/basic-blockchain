using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using BlockChain.Models.OxCoin;

namespace BlockChain.Models
{
    public class Transaction
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SourceWalletId { get; set; }

        [Required]
        public Guid DestinationWalletId { get; set; }

        [Required]
        public decimal TransferedAmount { get; set; }

        [Required]
        public decimal TransferFee { get; set; } = Math.Round(new Random().Next(1, 99) * 0.000001m, 8);

        [Required]
        public DateTime Timestamp { get; set; }

        [Required]
        public int Size { get; set; } = new Random().Next(200, 700);

        [Required]
        public IEnumerable<OxPiece> OxPieces { get; set; } = new List<OxPiece>();
    }
}
