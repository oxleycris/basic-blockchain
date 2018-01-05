using System;
using System.Collections.Generic;

namespace BlockChain.Classes
{
    public class Block
    {
        public int Position { get; set; }

        public Guid MagicNumber { get; set; } = Guid.NewGuid();

        public int BlockSize { get; set; } // Size in bytes of total block.

        public BlockHeader Header { get; set; } = new BlockHeader();

        public int TransactionCount { get; set; }

        public IEnumerable<Transaction> Transactions { get; set; }
    }

    public class BlockHeader
    {
        public string Version { get; set; }

        public string PreviousHash { get; set; }

        public string MerkleRoot { get; set; }

        public DateTime Timestamp { get; set; }

        public string Difficulty { get; set; }

        public int Nonce { get; set; }

        public string ValidHash { get; set; }
    }
}
