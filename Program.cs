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
        // DEBUG SETTINGS ////////////////////////////
        private static readonly bool readLine = false;
        private static int sleep = 500;
        //////////////////////////////////////////////

        // TODO: Make version, size etc const.
        private const string Difficulty = "00";
        private const string Version = "0.1";
        private static readonly IList<Block> BlockChain = new List<Block>();
        private static readonly IList<Wallet> Wallets = new List<Wallet>();
        private static readonly IList<User> Users = new List<User>();
        private static readonly IList<Transaction> MemPool = new List<Transaction>();

        public static void Main(string[] args)
        {
            // Generate a genesis user, and their wallet.
            GenerateGenesisUserWithWalletId();

            // If the blockchain has nothing in it then initialise it, and add the genesis block to it.
            if (BlockChain.Count == 0)
            {
                InitiliseBlockChain();
            }

            // Generate some dummy users to use for our transactions demo.
            GenerateUsersWithWalletIds();

            TransferGenesisFundsToUsers();

            // Set up some random transactions from within the user group.
            GenerateTransactionsList();

            var memPool = GetMemPool().ToList();

            while (memPool.Any())
            {
                // TODO: Collect transactions based on whether their size fits within a block size.
                var transactionsToProcess = memPool.Take(5).ToList();

                foreach (var transaction in transactionsToProcess)
                {
                    memPool.Remove(transaction);
                }

                // Hash all the transactional data and return a list of the hashes created. 
                var hashedTransactions = GenerateTransactionHashes(transactionsToProcess);

                // Process the merkle tree, to return the merkle root, using the hashed values from above.
                var merkleRoot = ProcessMerkleRootTree(hashedTransactions);

                // Stick the transactions list, previous hash value, merkle root, and a load of other data into a block.
                var block = new Block
                {
                    Position = NextAvailableIndexPosition(),
                    TransactionCount = transactionsToProcess.Count(),
                    Transactions = transactionsToProcess,
                    Header =
                    {
                        PreviousHash = GetLastHash(),
                        Timestamp = DateTime.Now,
                        Difficulty = Difficulty,
                        MerkleRoot = merkleRoot,
                        Version = Version
                    }
                };

                // Process this block to generate a valid hash, checking against our difficulty level.
                var validBlock = ValidateBlock(block);

                // Once valid, add to the blockchain.
                AddBlock(validBlock);
            }

            // Profit!

            Console.WriteLine("OxCoin balances...");
            Console.WriteLine("Name    |    Verified    |    Unverified");

            foreach (var user in GetUsers())
            {
                var walletId = GetWallet(GetUser(user.EmailAddress).Id).Id;
                var verifiedBalance = GetBalance(walletId, out decimal unVerifiedBalance);

                Console.WriteLine(user.GivenName + " " + user.FamilyName + "  |  " + verifiedBalance + "  |  " + (verifiedBalance + unVerifiedBalance) + "(" + unVerifiedBalance + " unverified)");
            }

            Console.ReadLine();
        }

        private static decimal GetBalance(Guid walletId, out decimal unVerifiedBalance)
        {
            unVerifiedBalance = 0.00m;
            var balance = 0.00m;
            var verifiedTransactions = new List<Transaction>();

            // Get all the verified transactions from the blockchain together.
            foreach (var block in GetBlockchain())
            {
                verifiedTransactions.AddRange(block.Transactions);
            }

            balance = verifiedTransactions.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            balance -= verifiedTransactions.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferedAmount);

            // TODO: Populate unVerifiedBalance from from the MemPool. This won't work yet as we always clear the MemPool out. 

            return balance;
        }

        private static void TransferGenesisFundsToUsers()
        {
            Console.WriteLine("Generating transactions...");
            Thread.Sleep(sleep);

            var transactionsList = new List<Transaction>();

            foreach (var wallet in GetWallets().Where(x => x.UserId != GetGenesisUser().Id))
            {
                var sourceWalletId = GetGenesisUser().Id;
                var destinationWalletId = wallet.Id;

                transactionsList.Add(new Transaction
                {
                    SourceWalletId = sourceWalletId,
                    DestinationWalletId = destinationWalletId,
                    TransferedAmount = 50m,
                    Timestamp = DateTime.Now
                });
            }

            var hashedTransactions = GenerateTransactionHashes(transactionsList);

            // Process the merkle tree, to return the merkle root, using the hashed values from above.
            var merkleRoot = ProcessMerkleRootTree(hashedTransactions);

            // Stick the transactions list, previous hash value, merkle root, and a load of other data into a block.
            var block = new Block
            {
                Position = NextAvailableIndexPosition(),
                TransactionCount = transactionsList.Count,
                Transactions = transactionsList,
                Header =
                {
                    PreviousHash = GetLastHash(),
                    Timestamp = DateTime.Now,
                    Difficulty = Difficulty,
                    MerkleRoot = merkleRoot,
                    Version = Version
                }
            };

            // Process this block to generate a valid hash, checking against our difficulty level.
            var validBlock = ValidateBlock(block);

            // Once valid, add to the blockchain.
            AddBlock(validBlock);
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

                        Thread.Sleep(sleep);
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

                        if (readLine)
                        {
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadLine();
                        }
                    }
                }

                Console.WriteLine("Merkle root: " + workingHashKvpList.First().Item2);
                if (readLine)
                {
                    Console.WriteLine("Press any key to continue...");
                    Console.ReadLine();
                }

                return workingHashKvpList.First().Item2;
            }

            // Only one transaction was passed into the method - probably for creation of the genesis block.
            // Therefore we put the value in twice, like we did above.
            var root = GenerateHash(hashKvpsToProcess.First().Item2, hashKvpsToProcess.First().Item2);

            Console.WriteLine("Merkle root: " + root);
            if (readLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

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

            Thread.Sleep(sleep);
            Console.WriteLine("[Idx] | [Hash]");

            foreach (var kvp in transactionHashKvpList.OrderBy(x => x.Item1))
            {
                Console.WriteLine(kvp.Item1 + " | " + kvp.Item2);
            }

            if (readLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

            return transactionHashKvpList;
        }

        private static string ConcatenateTransactionData(Transaction transaction)
        {
            return string.Format(transaction.SourceWalletId.ToString() +
                                 transaction.DestinationWalletId.ToString() +
                                 transaction.Timestamp.ToString());
        }

        private static string GenerateHash(string firstInput, string secondInput)
        {
            return GenerateHash(string.Concat(firstInput, secondInput));
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
            Thread.Sleep(sleep);

            Console.WriteLine("Generating genesis block...");
            Thread.Sleep(sleep);

            // Creates the genesis block - the first block within the chain.
            const string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";

            var transactions = new List<Transaction>{ new Transaction
            {
                SourceWalletId = new Guid("00000000-0000-0000-0000-000000000000"),
                DestinationWalletId = GetWallet(GetGenesisUser().Id).Id,
                TransferedAmount = 500.00m,
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
                    Timestamp = DateTime.Now,
                    Difficulty = Difficulty,
                    Version = Version
                }
            };

            block.Header.MerkleRoot = ProcessMerkleRootTree(hashedTransactions);

            Console.WriteLine("Genesis block created...");
            Thread.Sleep(sleep);

            AddBlock(ValidateBlock(block));
        }

        private static void AddBlock(Block block)
        {
            Console.WriteLine("Current block count: " + GetBlockchain().Count());
            Console.WriteLine("Adding block to blockchain...");
            Thread.Sleep(sleep);

            if (GetBlockchain().Count() != block.Position)
            {
                Console.WriteLine("Block expected position " + block.Position + " is not free within the blockchain!");
                Console.WriteLine("Press any key to retry...");
                Console.ReadLine();

                AddBlock(ValidateBlock(block));
            }

            BlockChain.Add(block);

            Console.WriteLine("Block added. Block count: " + GetBlockchain().Count());
            if (readLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }
        }

        private static Block ValidateBlock(Block block)
        {
            Console.WriteLine("Validating block...");
            Thread.Sleep(sleep);

            var hash = string.Empty;
            var nonce = 0;

            Console.WriteLine("Generating valid hash...");
            Thread.Sleep(sleep);

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
            if (readLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

            return block;
        }

        private static void GenerateTransactionsList()
        {
            Console.WriteLine("Generating transactions...");
            Thread.Sleep(sleep);

            var random = new Random();
            var idx = random.Next(10, 30);

            var transactionsList = new List<Transaction>();

            for (var i = 0; i < idx; i++)
            {
                var sourceWalletId = GetRandomWalletId();
                var destinationWalletId = GetRandomWalletId(sourceWalletId);

                MemPool.Add(new Transaction
                {
                    SourceWalletId = sourceWalletId,
                    DestinationWalletId = destinationWalletId,
                    TransferedAmount = random.Next(1, 99) / 100.00m,
                    Timestamp = new DateTime(random.Next(2017, 2017), random.Next(1, 12), random.Next(1, 28), random.Next(0, 23), random.Next(0, 59), random.Next(0, 59), random.Next(0, 999))
                });
            }

            Console.WriteLine("Transactions created: " + idx);
            if (readLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }
        }

        private static void GenerateUsersWithWalletIds()
        {
            var users = new List<User>
            {
                new User{ GivenName = "Alistair", FamilyName = "Evans", EmailAddress = "alistair.evans@7layer.net" },
                new User{ GivenName = "Owain", FamilyName = "Richardson", EmailAddress = "owain.richardson@7layer.net" },
                new User{ GivenName = "Matt", FamilyName = "Stahl-Coote", EmailAddress = "matt.stahl-coote@7layer.net" },
                new User{ GivenName = "Chris", FamilyName = "Bedwell", EmailAddress = "Chris.Bedwell@7layer.net" },
                new User{ GivenName = "John", FamilyName = "Rudden", EmailAddress = "john.rudden@7layer.net" }
            };

            foreach (var user in users)
            {
                // TODO: Create wrappers for these Add operations.
                Users.Add(user);
                Wallets.Add(new Wallet { UserId = user.Id });
            }
        }

        private static void GenerateGenesisUserWithWalletId()
        {
            var genesisUser = new User { GivenName = "Cris", FamilyName = "Oxley", EmailAddress = "cris.oxley@7layer.net" };

            // TODO: Create wrappers for these Add operations.
            Users.Add(genesisUser);
            Wallets.Add(new Wallet { UserId = GetGenesisUser().Id });
        }

        private static bool IsHashValid(string hash)
        {
            // Difficulty is the amount of numbers this has to satisfy.
            var hashValid = hash.StartsWith(Difficulty);

            return hashValid;
        }

        private static Wallet GetWallet(Guid userId)
        {
            return Wallets.First(x => x.UserId == userId);
        }

        private static Guid GetRandomWalletId(Guid? idToExclude = null)
        {
            return Wallets.Where(x => x.Id != idToExclude).ToList()[new Random().Next(0, Wallets.Count - 1)].Id;
        }

        private static User GetUser(string emailAddress)
        {
            return Users.FirstOrDefault(x => x.EmailAddress.ToLower() == emailAddress.ToLower());
        }

        private static User GetGenesisUser()
        {
            return Users.First(x => x.EmailAddress == "cris.oxley@7layer.net");
        }

        private static IEnumerable<Block> GetBlockchain()
        {
            return BlockChain.OrderBy(x => x.Position).ToList();
        }

        private static IEnumerable<Transaction> GetMemPool()
        {
            return MemPool;
        }

        private static IEnumerable<User> GetUsers()
        {
            return Users;
        }

        private static IEnumerable<Wallet> GetWallets()
        {
            return Wallets;
        }

        private static string GetLastHash()
        {
            return BlockChain.Last().Header.ValidHash;
        }

        private static int NextAvailableIndexPosition()
        {
            return BlockChain.Count;
        }
    }
}
