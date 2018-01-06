using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BlockChain.Data;
using BlockChain.Models;

namespace BlockChain
{
    public class Program
    {
        // DEBUG SETTINGS ////////////////////////////
        private static readonly bool ReadLine = false;
        private static int Sleep = 50;
        //////////////////////////////////////////////

        private const string Difficulty = "00";
        private const string Version = "0.1";
        private const int MaximumBlockSize = 1000000;

        private static readonly IList<Block> BlockChain = new List<Block>();

        public static void Main(string[] args)
        {
            // Initialise the blockchain, and add the genesis block to it.
            if (!BlockChain.Any())
            {
                InitiliseBlockChain();

                TransferGenesisFundsToUsers();
            }

            while (GetMemPool().Any())
            {
                var transactionsToProcess = GetMemPoolTransactionsToProcess().ToList();

                if (transactionsToProcess.Count > 0)
                {
                    // Hash all the transactional data and return a list of the hashes created. 
                    var hashedTransactions = GenerateTransactionHashes(transactionsToProcess);

                    // Process the merkle tree, to return the merkle root, using the hashed values from above.
                    var merkleRoot = ProcessMerkleRootTree(hashedTransactions);

                    // Stick the transactions list, previous hash value, merkle root, and a load of other data into a block.
                    var block = new Block
                    {
                        MaximumBlockSize = MaximumBlockSize,
                        BlockSize = transactionsToProcess.Sum(x => x.Size),
                        Position = NextAvailableIndexPosition(),
                        TransactionCount = transactionsToProcess.Count,
                        Transactions = transactionsToProcess,
                        Header = new BlockHeader
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

                    // Profit!

                    #region Balances and Hash List
                    Console.WriteLine();
                    Console.WriteLine("OxCoin balances...");
                    Console.WriteLine("Name                            |  Verified    |  Unverified");

                    foreach (var user in GetUsers())
                    {
                        var fnSpaces = string.Empty;
                        var fullName = string.Format(user.GivenName + " " + user.FamilyName);
                        for (var i = 0; i < (30 - fullName.Length); i++)
                        {
                            fnSpaces += " ";
                        }

                        var walletId = GetWallet(GetUser(user.EmailAddress).Id).Id;
                        var verifiedBalance = GetBalance(walletId, out var unVerifiedBalance);

                        var vbSpaces = string.Empty;
                        for (var i = 0; i < (10 - verifiedBalance.ToString(CultureInfo.InvariantCulture).Length); i++)
                        {
                            vbSpaces += " ";
                        }

                        Console.WriteLine(fullName + fnSpaces + "  |  " + verifiedBalance + vbSpaces + "  |  " + (verifiedBalance + unVerifiedBalance) + " (" + unVerifiedBalance + " unverified)");
                    }

                    Console.WriteLine("Miner balance: " + GetMinerBalance() + " OxCoins.");
                    #endregion
                }
                else
                {
                    Console.WriteLine("Waiting for the MemPool to populate...");
                }
            }

            Console.WriteLine("The MemPool is now empty.");
            Console.WriteLine("Blockchain valid hashes:");
            Console.WriteLine("Index  |  Size  |  Nonce  |  Hash");
            foreach (var b in GetBlockchain().OrderBy(x => x.Position))
            {
                Console.WriteLine(b.Position + "  |  " + b.BlockSize + "  |  " + b.Header.Nonce + "  |  " + b.Header.ValidHash);
            }
            Console.WriteLine("Miner balance: " + GetMinerBalance() + " OxCoins.");

            // Finished - beep!
            Console.Beep();
            Console.ReadLine();
        }

        private static IEnumerable<Transaction> GetMemPoolTransactionsToProcess()
        {
            var memPool = GetMemPool().ToList();
            var memPoolWorkingList = memPool.Where(x => x.Timestamp <= DateTime.Now.AddMonths(-3)).ToList();

            foreach (var transaction in memPoolWorkingList)
            {
                memPool.Remove(transaction);
            }

            // Add transactions in order of their size.
            memPoolWorkingList.AddRange(memPool.OrderByDescending(x => x.Size));

            var maximumTransactionSize = MaximumBlockSize;
            var transactionsToProcess = new List<Transaction>();

            while (maximumTransactionSize > 0)
            {
                while (memPoolWorkingList.Any())
                {
                    var transaction = memPoolWorkingList.First();

                    if (transaction.Size <= maximumTransactionSize)
                    {
                        transactionsToProcess.Add(transaction);
                        memPoolWorkingList.Remove(transaction);
                        DeleteFromMemPool(transaction);
                        maximumTransactionSize -= transaction.Size;
                    }
                    else
                    {
                        // If there is only one remaining transaction, but its size is too big to fit then return the list without it.
                        if (memPoolWorkingList.Count == 1 && transaction.Size <= maximumTransactionSize)
                        {
                            return transactionsToProcess;
                        }

                        memPoolWorkingList = memPoolWorkingList.Where(x => x.Size <= maximumTransactionSize).ToList();

                        // If there are no transactions left in the MemPool,
                        // or the smallest available size transaction in the MemPool is bigger than our remaining capacity then process this list as is.
                        if (!memPoolWorkingList.Any() || !(memPoolWorkingList.OrderBy(x => x.Size).First().Size < maximumTransactionSize))
                        {
                            return transactionsToProcess;
                        }
                    }
                }

                if (transactionsToProcess.Any())
                {
                    foreach (var transaction in transactionsToProcess)
                    {
                        AddToMemPool(transaction);
                    }
                }

                transactionsToProcess.Clear();

                return transactionsToProcess;
            }

            return transactionsToProcess;
        }

        private static void AddToMemPool(Transaction transaction)
        {
            using (var db = new BlockChainDbContext())
            {
                db.Transactions.Add(transaction);
                db.SaveChanges();
            }
        }

        private static void DeleteFromMemPool(Transaction transaction)
        {
            using (var db = new BlockChainDbContext())
            {
                db.Transactions.Remove(transaction);
                db.SaveChanges();
            }
        }

        private static decimal GetBalance(Guid walletId, out decimal unVerifiedBalance)
        {
            // TODO: Somewhere, check a balance before allowing a transaction to occur.
            decimal uvb;
            decimal balance;
            var transactionsList = new List<Transaction>();

            // Get all the verified transactions from the blockchain together.
            foreach (var block in GetBlockchain())
            {
                transactionsList.AddRange(block.Transactions);
            }

            balance = transactionsList.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            balance -= transactionsList.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferedAmount) - transactionsList.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferFee);

            transactionsList.Clear();
            transactionsList.AddRange(GetMemPool());

            uvb = transactionsList.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            uvb -= transactionsList.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferedAmount) - transactionsList.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferFee);

            unVerifiedBalance = uvb;

            return balance;
        }

        private static void TransferGenesisFundsToUsers()
        {
            Console.WriteLine("Generating transactions...");
            Thread.Sleep(Sleep);

            var transactionsList = new List<Transaction>();
            var genesisUser = GetGenesisUser();
            var wallets = GetWallets().ToList();

            foreach (var wallet in wallets.Where(x => x.UserId != genesisUser.Id))
            {
                var sourceWalletId = GetWallet(genesisUser.Id).Id;
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
                MaximumBlockSize = MaximumBlockSize,
                BlockSize = transactionsList.Sum(x => x.Size),
                Position = NextAvailableIndexPosition(),
                TransactionCount = transactionsList.Count,
                Transactions = transactionsList,
                Header = new BlockHeader
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

                        Thread.Sleep(Sleep);
                        Console.WriteLine("Pass: " + passCount);

                        // If there are an odd number of items to process then we add the last  allowing us to pair up an even number.
                        if (hashKvpsToProcess.Count % 2 == 1)
                        {
                            hashKvpsToProcess.Add(hashKvpsToProcess.Last());
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

                        Console.WriteLine("[Idx] | [Hash]");

                        foreach (var workingHashKvp in workingHashKvpList)
                        {
                            Console.WriteLine(workingHashKvp.Item1 + " | " + workingHashKvp.Item2);
                        }

                        if (ReadLine)
                        {
                            Console.WriteLine("Press any key to continue...");
                            Console.ReadLine();
                        }
                    }
                }

                Console.WriteLine("Merkle root: " + workingHashKvpList.First().Item2);
                if (ReadLine)
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
            if (ReadLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

            return root;
        }

        private static IEnumerable<Tuple<int, string>> GenerateTransactionHashes(IList<Transaction> transactions)
        {
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

            Thread.Sleep(Sleep);
            Console.WriteLine("[Idx] | [Hash]");

            foreach (var kvp in transactionHashKvpList.OrderBy(x => x.Item1))
            {
                Console.WriteLine(kvp.Item1 + " | " + kvp.Item2);
            }

            if (ReadLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

            return transactionHashKvpList;
        }

        private static string ConcatenateTransactionData(Transaction transaction)
        {
            return string.Concat(transaction.SourceWalletId,
                                 transaction.DestinationWalletId,
                                 transaction.Timestamp);
        }

        private static string GenerateHash(string firstInput, string secondInput)
        {
            return GenerateHash(string.Concat(firstInput, secondInput));
        }

        private static string GenerateHash(string input)
        {
            var sha256 = new SHA256Managed();
            var hashString = string.Empty;
            var transactionBytes = Encoding.Default.GetBytes(input);
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
            Thread.Sleep(Sleep);

            Console.WriteLine("Generating genesis block...");
            Thread.Sleep(Sleep);

            // Creates the genesis block - the first block within the chain.
            const string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";

            var transactions = new List<Transaction>{ new Transaction
            {
                SourceWalletId = new Guid("00000000-0000-0000-0000-000000000000"),
                DestinationWalletId = GetWallet(GetGenesisUser().Id).Id,
                TransferedAmount = 1000.000000m,
                Timestamp = new DateTime(1980, 4, 14),
                Size = MaximumBlockSize
            }};

            var hashedTransactions = GenerateTransactionHashes(transactions);

            var block = new Block
            {
                // We need to find the next available index position within the blockchain to put the block into.
                Position = NextAvailableIndexPosition(),
                MaximumBlockSize = MaximumBlockSize,
                BlockSize = transactions.Sum(x => x.Size),
                TransactionCount = transactions.Count,
                Transactions = transactions,
                Header = new BlockHeader
                {
                    PreviousHash = previousHash,
                    Timestamp = DateTime.Now,
                    Difficulty = Difficulty,
                    Version = Version
                }
            };

            block.Header.MerkleRoot = ProcessMerkleRootTree(hashedTransactions);

            Console.WriteLine("Genesis block created...");
            Thread.Sleep(Sleep);

            AddBlock(ValidateBlock(block));
        }

        private static void AddBlock(Block block)
        {
            Console.WriteLine("Current block count: " + GetBlockchain().Count());
            Console.WriteLine("Adding block to blockchain...");
            Thread.Sleep(Sleep);

            if (GetBlockchain().Count() != block.Position)
            {
                Console.WriteLine("Block expected position " + block.Position + " is not free within the blockchain!");
                Console.WriteLine("Press any key to retry...");
                Console.ReadLine();

                AddBlock(ValidateBlock(block));
            }

            BlockChain.Add(block);

            Console.WriteLine("Block added. Block count: " + GetBlockchain().Count());
            if (ReadLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }
        }

        private static Block ValidateBlock(Block block)
        {
            Console.WriteLine("Validating block...");
            Thread.Sleep(Sleep);

            var hash = string.Empty;
            var nonce = 0;

            Console.WriteLine("Generating valid hash...");
            Thread.Sleep(Sleep);

            // If the hash does not start with the required amount of zeros then it is not valid.
            while (!IsHashValid(hash))
            {
                // The nonce goes into the input to be hashed for our valid block hash, so everytime the hash validation fails the input is rehashed with a new nonce value.
                nonce += 1;

                var input = string.Concat(block.Header.MerkleRoot,
                                          nonce,
                                          block.Header.PreviousHash,
                                          block.Header.Timestamp,
                                          block.MagicNumber,
                                          block.Transactions.Sum(x => x.Size));

                hash = GenerateHash(input);
            }

            block.Header.Nonce = nonce;
            block.Header.ValidHash = hash;

            Console.WriteLine("[VALID] | Nonce: " + nonce + " | Hash: " + hash);
            if (ReadLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }

            return block;
        }

        private static bool IsHashValid(string hash)
        {
            // Difficulty is the amount of numbers this has to satisfy.
            var hashValid = hash.StartsWith(Difficulty);

            return hashValid;
        }

        private static Wallet GetWallet(Guid userId)
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Wallets.First(x => x.UserId == userId);
            }
        }

        //private static Guid GetRandomWalletId(Guid? idToExclude = null)
        //{
        //    return GetWallets().Where(x => x.Id != idToExclude).ToList()[new Random().Next(0, GetWallets().Count() - 1)].Id;
        //}

        private static decimal GetMinerBalance()
        {
            var transactions = new List<Transaction>();

            foreach (var block in GetBlockchain())
            {
                transactions.AddRange(block.Transactions);
            }

            return transactions.Sum(x => x.TransferFee);
        }

        private static User GetUser(string emailAddress)
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Users.FirstOrDefault(x => x.EmailAddress.ToLower() == emailAddress.ToLower());
            }
        }

        private static User GetGenesisUser()
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Users.First(x => x.EmailAddress == "cris.oxley@7layer.net");
            }
        }

        private static IEnumerable<Block> GetBlockchain()
        {
            return BlockChain.OrderBy(x => x.Position).ToList();
        }

        private static IEnumerable<Transaction> GetMemPool()
        {
            using (var db = new BlockChainDbContext())
            {
                foreach (var transaction in db.Transactions)
                {
                    yield return transaction;
                }
            }
        }

        private static IEnumerable<User> GetUsers()
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Users.ToList();
            }
        }

        private static IEnumerable<Wallet> GetWallets()
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Wallets.ToList();
            }
        }

        private static string GetLastHash()
        {
            return GetBlockchain().Last().Header.ValidHash;
        }

        private static int NextAvailableIndexPosition()
        {
            return GetBlockchain().Count();
        }
    }
}
