using System;
using System.Collections.Generic;
using System.Security.Cryptography;

namespace BlockChain.Models
{
    public class Block
    {
        public Block()
        {
            var bytes = new byte[16];
            var rng = new RNGCryptoServiceProvider();
            rng.GetBytes(bytes);

            MagicNumber = BitConverter.ToString(bytes).Replace("-", "").ToLower();
        }

        public int Position { get; set; }

        public string MagicNumber { get; }

        public int MaximumBlockSize { get; set; }

        public int BlockSize { get; set; } // Sum of all transaction sizes contained within the block.

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
