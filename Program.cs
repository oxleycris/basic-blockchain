using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;

namespace BlockChain
{
    public class Program
    {
        public static int pass = 0;
        public static IList<string> blocks = new List<string>();

        public static void Main(string[] args)
        {
            // TODO: Return Transaction objects so we can bring in more data.
            var transactions = GenerateTransactionsList();

            // Initialise the blockchain with the genesis block.
            InitBlockChain();

            var hashedTransactions = HashTransactions(transactions);

            // Run through the hashed tranasction collection and process the merkle tree/binary hash tree.
            while (hashedTransactions.Count > 1)
            {
                hashedTransactions = HashTransactionPairs(hashedTransactions);
            }

            var rootHash = hashedTransactions.First().HashString;

            Console.WriteLine("Root hash: " + rootHash);
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

            var index = GetAllBlocks().Count() + 1;

            HashBlock(rootHash, DateTime.Now.ToString(), GetLastHash(), index);
        }

        private static IList<Transaction> HashTransactionPairs(IList<Transaction> transactions)
        {
            pass++;

            var lastTransaction = new Transaction();

            if (transactions.Count % 2 == 1)
            {
                lastTransaction = transactions.Last();
                transactions.Remove(lastTransaction);
            }

            var transactionPair = new List<string>();
            var hashedTransactions = new List<Transaction>();
            var idx = 0;

            while (transactions.Count > 0)
            {
                transactionPair.Clear();
                transactionPair.Add(transactions[0].HashString);
                transactionPair.Add(transactions[1].HashString);

                hashedTransactions.Add(new Transaction{ Idx = idx, HashString = HashPair(transactionPair) });

                for (int i = 0; i < 2; i++)
                {
                    transactions.Remove(transactions[0]);
                }

                idx++;
            }

            if (!string.IsNullOrEmpty(lastTransaction.HashString))
            {
                transactionPair.Clear();
                transactionPair.Add(lastTransaction.HashString);
                transactionPair.Add(lastTransaction.HashString);

                hashedTransactions.Add(new Transaction { Idx = idx, HashString = HashPair(transactionPair) });
            }

            Console.WriteLine("Hashing pairs (pass " + pass + ")...");
            Thread.Sleep(1500);

            Console.WriteLine("[Idx] | [Hash]");

            foreach (var hashedTransaction in hashedTransactions)
            {
                Console.WriteLine(hashedTransaction.Idx + " | " + hashedTransaction.HashString);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

            return hashedTransactions;
        }

        private static string HashPair(List<string> transactionList)
        {
            var sha256 = new SHA256Managed();

            var hashString = string.Empty;
            var transactionBytes = System.Text.Encoding.Default.GetBytes(string.Concat(transactionList[0] + transactionList[1]));
            // Double hashed.
            var encodedString = sha256.ComputeHash(sha256.ComputeHash(transactionBytes));

            for (int j = 0; j < encodedString.Length; j++)
            {
                hashString += encodedString[j].ToString("X");
            }

            return hashString;
        }

        private static IList<Transaction> HashTransactions(List<string> transactionList)
        {
            Console.WriteLine("Hashing transactions...");

            var hashedTransactionList = new List<Transaction>();
            var sha256 = new SHA256Managed();

            for (int i = 0; i < transactionList.Count; i++)
            {
                var hashString = string.Empty;
                var transactionBytes = System.Text.Encoding.Default.GetBytes(transactionList[i]);
                // Double hashed.
                var encodedString = sha256.ComputeHash(sha256.ComputeHash(transactionBytes));

                for (int j = 0; j < encodedString.Length; j++)
                {
                    hashString += encodedString[j].ToString("X");
                }

                hashedTransactionList.Add(new Transaction { Idx = i, HashString = hashString });
            }

            hashedTransactionList.OrderBy(x => x.Idx);

            Thread.Sleep(1500);
            Console.WriteLine("[Idx] | [Hash]");

            foreach (var kvp in hashedTransactionList)
            {
                Console.WriteLine(kvp.Idx + " | " + kvp.HashString);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

            return hashedTransactionList;
        }

        public static void InitBlockChain()
        {
            // Creates the genesis block - the first block within the chain.
            const string data = "Genesis block";
            const string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";

            Console.WriteLine("Initialising blockchain...");
            Thread.Sleep(1500);

            Console.WriteLine("Generating genesis block...");
            Thread.Sleep(1500);

            Console.WriteLine("Genesis block created...");
            Thread.Sleep(1500);

            HashBlock(data, DateTime.Now.ToString(), previousHash, 0);
        }

        public static void AddNewBlock(string data)
        {
            var index = blocks.Count;
            var previousHash = GetLastHash();

            HashBlock(data, DateTime.Now.ToString(), previousHash, index);
        }

        public static IEnumerable<string> GetAllBlocks()
        {
            return blocks;
        }

        private static void HashBlock(string data, string timestamp, string prevHash, int index)
        {
            var hash = string.Empty;
            var nonce = 0;
            var sha256 = new SHA256Managed();

            while (!IsHashValid(hash))
            {
                var hashString = string.Empty;
                var input = string.Format(data + timestamp + prevHash + index + nonce);
                var sha256Bytes = System.Text.Encoding.Default.GetBytes(input);
                var encodedString = sha256.ComputeHash(sha256Bytes);

                for (int i = 0; i < encodedString.Length; i++)
                {
                    hashString += encodedString[i].ToString("X");
                }

                hash = hashString;
                nonce += 1;

                //Console.WriteLine(nonce.ToString() + " | " + hash);
            }

            Console.WriteLine("[VALID] | Nonce: " + nonce + " | Hash: " + hash);

            blocks.Add(hash);

            Console.WriteLine("Block count: " + GetAllBlocks().Count());
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        private static string GetLastHash()
        {
            var lastHash = blocks.Last();

            return lastHash;
        }

        private static bool IsHashValid(string hash)
        {
            // Difficulty is the amount of numbers this has to satisfy.
            // This is very low difficulty.
            var hashValid = hash.StartsWith("00");

            return hashValid;
        }

        private static List<string> GenerateTransactionsList()
        {
            const string transactionA = "My first block chain string";
            const string transactionB = "My second block chain string";
            const string transactionC = "My third block chain string";
            const string transactionD = "My forth block chain string";
            const string transactionE = "My fifth block chain string";

            var transactionsList = new List<string>
            {
                transactionA,
                transactionB,
                transactionC,
                transactionD,
                transactionE
            };

            return transactionsList;
        }
    }

    public class Transaction
    {
        public int Idx { get; set; }

        public string HashString { get; set; }
    }
}
