using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using BlockChain.Classes;

namespace BlockChain
{
    public class Program
    {
        private const string Difficulty = "00";
        private static readonly IList<Block> BlockChain = new List<Block>();

        public static void Main(string[] args)
        {
            // If the blockchain has nothing in it then initialise it, and add the genesis block to it.
            if (BlockChain.Count == 0)
            {
                InitiliseBlockChain();
            }

            var blockchain = GetBlockchain();

            // For the sake of demo we will add 4 blocks to the blockchain, once the genesis block has been created.
            for (var i = 0; i < 3; i++)
            {
                // Set up some random transactions.
                var transactions = GenerateTransactionsList();

                // Hash all the transactional data and return a list of the hashes created. 
                var hashedTransactions = GenerateTransactionHashes(transactions);

                // Process the merkle tree, to return the merkle root, using the hashed values from above.
                var merkleRoot = ProcessMerkleRootTree(hashedTransactions);

                // Stick the transactions list, previous hash value, merkle root, and a load of other data into a block.
                var block = new Block
                {
                    Position = NextAvailableIndexPosition(),
                    TransactionCount = transactions.Count,
                    Transactions = transactions,
                    Header =
                    {
                        PreviousHash = GetLastHash(),
                        Timestamp = DateTime.Now,
                        Difficulty = Difficulty,
                        MerkleRoot = merkleRoot,
                        Version = "0.1"
                    }
                };

                // Process this block to generate a valid hash, checking against our difficulty level.
                var validBlock = ValidateBlock(block);

                // Once valid, add to the blockchain.
                AddBlock(validBlock);
            }

            // Profit!
            blockchain = GetBlockchain();
        }

        private static IEnumerable<Block> GetBlockchain()
        {
            return BlockChain.OrderBy(x => x.Position).ToList();
        }

        private static string ProcessMerkleRootTree(IEnumerable<Tuple<int, string>> hashKvps)
        {
            Console.WriteLine("Processing merkle tree...");

            var hashKvpsToProcess = hashKvps.OrderBy(x => x.Item1).ToList();

            // This is where the hashes will be stored that are created from the transactions processed from the list above.
            var workingHashKvpList = new List<Tuple<int, string>>();

            if (hashKvpsToProcess.Count > 1)
            {
                // How many times we have had to go through the hashing process to roll the merkle tree up towards its root hash.
                var passCount = 0;

                // If the count is equal to one here then we have found our root value, otherwise continue processing.
                while (workingHashKvpList.Count != 1)
                {
                    // If at the end of the pass there are 2 or more hashes left then we need to go through the process again.
                    if (workingHashKvpList.Count > 1)
                    {
                        // Repopulate the processing list, so that we can use it in the WHILE loop below.
                        hashKvpsToProcess = workingHashKvpList.OrderBy(x => x.Item1).ToList();

                        // Clear out the working list so that we can repopulate with our generated hashes in the WHILE loop below.
                        workingHashKvpList.Clear();
                    }

                    while (hashKvpsToProcess.Count > 1)
                    {
                        passCount++;

                        Thread.Sleep(1200);
                        Console.WriteLine("Pass: " + passCount);
                        Tuple<int, string> lastHashKvp = null;

                        // If there are an odd number of items to process then we remove the last and save it for later, allowing us to pair up an even number.
                        if (hashKvpsToProcess.Count % 2 == 1)
                        {
                            lastHashKvp = hashKvpsToProcess.Last();
                            hashKvpsToProcess.Remove(lastHashKvp);
                        }

                        var idx = 0;

                        while (hashKvpsToProcess.Count > 0)
                        {
                            // Add the newly hashed pair, plus their index, to the working list.
                            workingHashKvpList.Add(new Tuple<int, string>(idx, GenerateHash(hashKvpsToProcess[0].Item2, hashKvpsToProcess[1].Item2)));

                            // Remove the items we have processed for when we run through the WHILE loop again.
                            for (var i = 0; i < 2; i++)
                            {
                                hashKvpsToProcess.Remove(hashKvpsToProcess[0]);
                            }

                            idx++;
                        }

                        // Once out of the WHILE loop we can process the "last" transaction we removed when we had an odd number..remember?
                        if (!string.IsNullOrEmpty(lastHashKvp?.Item2))
                        {
                            // As we only have a single item to hash, we put it in twice.
                            workingHashKvpList.Add(new Tuple<int, string>(idx, GenerateHash(lastHashKvp.Item2, lastHashKvp.Item2)));
                        }

                        Console.WriteLine("[Idx] | [Hash]");

                        foreach (var workingHashKvp in workingHashKvpList)
                        {
                            Console.WriteLine(workingHashKvp.Item1 + " | " + workingHashKvp.Item2);
                        }

                        Console.WriteLine("Press any key to continue...");
                        Console.ReadLine();
                    }
                }

                Console.WriteLine("Merkle root: " + workingHashKvpList.First().Item2);
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();

                return workingHashKvpList.First().Item2;
            }

            // Only one transaction was passed into the method - probably for creation of the genesis block.
            // Therefore we put the value in twice, like we did above.
            var root = GenerateHash(hashKvpsToProcess.First().Item2, hashKvpsToProcess.First().Item2);

            Console.WriteLine("Merkle root: " + root);
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

            return root;
        }

        private static IEnumerable<Tuple<int, string>> GenerateTransactionHashes(IList<Transaction> transactions)
        {
            var sha256 = new SHA256Managed();

            Console.WriteLine("Hashing transactions...");

            var transactionHashKvpList = new List<Tuple<int, string>>();

            for (var i = 0; i < transactions.Count; i++)
            {
                // Concatenate all the data from thet transaction.
                var input = ConcatenateTransactionData(transactions[i]);
                var hash = GenerateHash(input);

                // Add the hash to our list of hashes to return.
                transactionHashKvpList.Add(new Tuple<int, string>(i, hash));
            }

            Thread.Sleep(1500);
            Console.WriteLine("[Idx] | [Hash]");

            foreach (var kvp in transactionHashKvpList.OrderBy(x => x.Item1))
            {
                Console.WriteLine(kvp.Item1 + " | " + kvp.Item2);
            }

            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

            return transactionHashKvpList;
        }

        private static string ConcatenateTransactionData(Transaction transaction)
        {
            return string.Format(transaction.SourceAccount.SortCode +
                                 transaction.SourceAccount.AccountNumber +
                                 transaction.DestinationAccount.SortCode +
                                 transaction.DestinationAccount.AccountNumber +
                                 transaction.TransferedAmount +
                                 transaction.Timestamp);
        }

        private static string GenerateHash(string firstString, string secondString)
        {
            var input = string.Concat(firstString, secondString);

            return GenerateHash(input);
        }

        private static string GenerateHash(string input)
        {
            var sha256 = new SHA256Managed();
            var hashString = string.Empty;
            var transactionBytes = System.Text.Encoding.Default.GetBytes(input);
            var encodedString = sha256.ComputeHash(sha256.ComputeHash(transactionBytes));

            foreach (var b in encodedString)
            {
                hashString += b.ToString("X");
            }

            return hashString;
        }

        public static void InitiliseBlockChain()
        {
            Console.WriteLine("Initialising blockchain...");
            Thread.Sleep(1500);

            Console.WriteLine("Generating genesis block...");
            Thread.Sleep(1500);

            // Creates the genesis block - the first block within the chain.
            const string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";

            var transactions = new List<Transaction>{ new Transaction
            {
                SourceAccount = new Account
                {
                    SortCode = 123456.ToString(),
                    AccountNumber = 12345678.ToString()
                },
                DestinationAccount = new Account
                {
                    SortCode = 654321.ToString(),
                    AccountNumber = 87654321.ToString()
                },
                TransferedAmount = 69.69m,
                Timestamp = new DateTime(1980, 4, 14)
            }};

            var hashedTransactions = GenerateTransactionHashes(transactions);

            var block = new Block
            {
                // We need to find the next available index position within the blockchain to put the block into.
                Position = NextAvailableIndexPosition(),
                TransactionCount = transactions.Count,
                Transactions = transactions,
                Header =
                {
                    PreviousHash = previousHash,
                    Timestamp = transactions.First().Timestamp,
                    Difficulty = Difficulty,
                    Version = "0.1"
                }
            };

            block.Header.MerkleRoot = ProcessMerkleRootTree(hashedTransactions);

            Console.WriteLine("Genesis block created...");
            Thread.Sleep(1500);

            AddBlock(ValidateBlock(block));
        }

        private static void AddBlock(Block block)
        {
            Console.WriteLine("Current block count: " + BlockChain.Count);
            Console.WriteLine("Adding block to blockchain...");
            Thread.Sleep(1500);

            if (GetBlockchain().Count() != block.Position)
            {
                Console.WriteLine("Block expected position " + block.Position + " is not free within the blockchain!");
                Console.WriteLine("Press any key to retry...");
                Console.ReadLine();

                AddBlock(ValidateBlock(block));
            }

            BlockChain.Add(block);

            Console.WriteLine("Block added. Block count: " + BlockChain.Count());
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();
        }

        private static Block ValidateBlock(Block block)
        {
            Console.WriteLine("Validating block...");
            Thread.Sleep(1500);

            var hash = string.Empty;
            var nonce = 0;

            Console.WriteLine("Generating valid hash...");
            Thread.Sleep(1500);

            // If the hash does not start with the required amount of zeros then it is not valid.
            while (!IsHashValid(hash))
            {
                // The nonce goes into the input to be hashed for our valid block hash, so everytime the hash validation fails the input is rehashed with a new nonce value.
                nonce += 1;

                var input = string.Concat(block.Header.MerkleRoot, nonce, block.Header.PreviousHash, block.Header.Timestamp);

                hash = GenerateHash(input);
            }

            block.Header.Nonce = nonce;
            block.Header.ValidHash = hash;

            Console.WriteLine("[VALID] | Nonce: " + nonce + " | Hash: " + hash);
            Console.WriteLine("Press any key to continue...");
            Console.ReadLine();

            return block;
        }

        private static string GetLastHash()
        {
            return BlockChain.Last().Header.ValidHash;
        }

        private static int NextAvailableIndexPosition()
        {
            return BlockChain.Count;
        }

        private static bool IsHashValid(string hash)
        {
            // Difficulty is the amount of numbers this has to satisfy.
            var hashValid = hash.StartsWith(Difficulty);

            return hashValid;
        }

        private static List<Transaction> GenerateTransactionsList()
        {
            Console.WriteLine("Generating transactions...");
            Thread.Sleep(1500);

            var rnd = new Random();

            var transactionsList = new List<Transaction>();

            for (var i = 0; i < rnd.Next(1, 10); i++)
            {
                transactionsList.Add(new Transaction
                {
                    SourceAccount = new Account
                    {
                        SortCode = rnd.Next(100000, 999999).ToString(),
                        AccountNumber = rnd.Next(10000000, 99999999).ToString()
                    },
                    DestinationAccount = new Account
                    {
                        SortCode = rnd.Next(100000, 999999).ToString(),
                        AccountNumber = rnd.Next(10000000, 99999999).ToString()
                    },
                    TransferedAmount = rnd.Next(1, 9999) / 100.00m,
                    Timestamp = new DateTime(rnd.Next(2017, 2017), rnd.Next(1, 12), rnd.Next(1, 28), rnd.Next(0, 23), rnd.Next(0, 59), rnd.Next(0, 59), rnd.Next(0, 999))
                });
            }

            return transactionsList;
        }
    }
}
