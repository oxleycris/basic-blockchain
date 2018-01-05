using System;
using System.ComponentModel.DataAnnotations;

namespace BlockChain.Classes
{
    public class Transaction
    {
        public Account SourceAccount { get; set; }

        public Account DestinationAccount { get; set; }

        [Required]
        public decimal TransferedAmount { get; set; }

        [Required]
        public DateTime Timestamp { get; set; }
    }
}
