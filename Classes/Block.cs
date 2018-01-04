using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlockChain.Classes
{
    public class Block
    {
        public Guid MagicNumber { get; set; } = new Guid();

        public int BlockSize { get; set; } // Size in bytes of total block.

        public BlockHeader Header { get; set; }

        public int TransactionCount { get; set; }

        public IEnumerable<Transaction> Transactions { get; set; }
    }

    public abstract class BlockHeader
    {
        public string Version { get; set; }

        public string PreviousHash { get; set; }

        public string MerkleRoot { get; set; }

        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Number of leading zeros, not zero-based!
        /// </summary>
        public int Difficulty { get; set; }

        public int Nonce { get; set; }
    }
}
