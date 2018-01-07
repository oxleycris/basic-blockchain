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
        // General TODO LIST 
        // TODO: Somewhere, check a balance before allowing a transaction to occur.

        // DEBUG SETTINGS ////////////////////////////
        private const bool ReadLine = false;
        private const int Sleep = 50;
        //////////////////////////////////////////////

        private const string Difficulty = "00";
        private const string Version = "0.1";
        private const int MaximumBlockSize = 1000000;

        private static readonly IList<Block> Blockchain = new List<Block>();

        public static void Main(string[] args)
        {
            // Initialise the blockchain, and add the genesis block to it.
            if (!Blockchain.Any())
            {
                InitiliseBlockchain();

                TransferGenesisFundsToUsers();
            }

            // Miner.
            // Eventually this will just run as a constant process.
            // Oneday, someday, somehow, somewhere...
            while (GetTransactionPool().Any())
            {
                var transactionsToProcess = GetTransactionsToProcess().ToList();

                if (transactionsToProcess.Count > 0)
                {
                    var block = CreateBlock(transactionsToProcess);
                    var validBlock = ValidateBlock(block);

                    AddToBlockchain(validBlock);

                    // ???
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
                    Console.WriteLine("Waiting for the transaction pool to populate...");
                }
            }

            #region Transaction pool empty
            Console.WriteLine("The transaction pool is now empty.");
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
            #endregion
        }

        /// <summary>
        /// Creates an unvalidated block using the transaction list to generate the merkle root hash.
        /// </summary>
        /// <param name="transactionsToProcess">The list of transactions that the block will contain.</param>
        /// <returns>The unvalidated block.</returns>
        private static Block CreateBlock(IList<Transaction> transactionsToProcess)
        {
            var hashedTransactions = GenerateTransactionHashes(transactionsToProcess);
            var merkleRootHash = GenerateMerkleRootHash(hashedTransactions);

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
                    MerkleRootHash = merkleRootHash,
                    Version = Version
                }
            };

            return block;
        }

        /// <summary>
        /// Gets transactions from the transaction pool, any that are older than our date cut-off, and then in descending order of size.
        /// </summary>
        /// <returns>A list of transactions to process.</returns>
        private static IEnumerable<Transaction> GetTransactionsToProcess()
        {
            var transactionPool = GetTransactionPool().ToList();
            var workingList = transactionPool.Where(x => x.Timestamp <= DateTime.Now.AddMonths(-3)).ToList();

            foreach (var transaction in workingList)
            {
                transactionPool.Remove(transaction);
            }

            workingList.AddRange(transactionPool.OrderByDescending(x => x.Size));

            var maximumTransactionSize = MaximumBlockSize;
            var transactionsToProcess = new List<Transaction>();

            while (maximumTransactionSize > 0)
            {
                while (workingList.Any())
                {
                    var transaction = workingList.First();

                    if (transaction.Size <= maximumTransactionSize)
                    {
                        transactionsToProcess.Add(transaction);
                        workingList.Remove(transaction);
                        DeleteFromTransactionPool(transaction);
                        maximumTransactionSize -= transaction.Size;
                    }
                    else
                    {
                        // If there is only one remaining transaction, but its size is too big to fit then return the list without it.
                        if (workingList.Count == 1 && transaction.Size <= maximumTransactionSize)
                        {
                            return transactionsToProcess;
                        }

                        workingList = workingList.Where(x => x.Size <= maximumTransactionSize).ToList();

                        // If there are no transactions left in the transaction pool,
                        // or the smallest available size transaction in the transaction pool is bigger than our remaining capacity then process this list as is.
                        if (!workingList.Any() || !(workingList.OrderBy(x => x.Size).First().Size < maximumTransactionSize))
                        {
                            return transactionsToProcess;
                        }
                    }
                }

                if (transactionsToProcess.Any())
                {
                    foreach (var transaction in transactionsToProcess)
                    {
                        AddToTransactionPool(transaction);
                    }
                }

                transactionsToProcess.Clear();

                return transactionsToProcess;
            }

            return transactionsToProcess;
        }

        private static void AddToTransactionPool(Transaction transaction)
        {
            using (var db = new BlockChainDbContext())
            {
                db.Transactions.Add(transaction);
                db.SaveChanges();
            }
        }

        private static void DeleteFromTransactionPool(Transaction transaction)
        {
            using (var db = new BlockChainDbContext())
            {
                db.Transactions.Remove(transaction);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Gets the verified and unverified balances for a wallet.
        /// </summary>
        /// <param name="walletId">the wallet id.</param>
        /// <param name="unVerifiedBalance">The unverified balance.</param>
        /// <returns>The verified balance.</returns>
        private static decimal GetBalance(Guid walletId, out decimal unVerifiedBalance)
        {
            decimal uvb;
            decimal balance;
            var transactionsList = new List<Transaction>();

            // Get all the verified transactions from the blockchain.
            foreach (var block in GetBlockchain())
            {
                transactionsList.AddRange(block.Transactions);
            }

            balance = transactionsList.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            balance -= transactionsList.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferedAmount) - transactionsList.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferFee);

            // Clear all the verified transactions out...
            transactionsList.Clear();
            // ...so that we can now query the unverified transactions from the transaction pool.
            transactionsList.AddRange(GetTransactionPool());

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

            var block = CreateBlock(transactionsList);

            AddToBlockchain(ValidateBlock(block));
        }

        /// <summary>
        /// Processes a merkle tree using a list of hashes to generate the merkle root hash.
        /// </summary>
        /// <param name="hashKvps">The list of hashes to process.</param>
        /// <returns>The merkle root hash.</returns>
        private static string GenerateMerkleRootHash(IEnumerable<Tuple<int, string>> hashKvps)
        {
            Console.WriteLine("Processing merkle tree...");

            var hashKvpsToProcess = hashKvps.OrderBy(x => x.Item1).ToList();
            var workingHashKvpList = new List<Tuple<int, string>>();

            if (hashKvpsToProcess.Count > 1)
            {
                var passCount = 0;

                // If the count is equal to one here then we have found our root value, otherwise continue processing.
                while (workingHashKvpList.Count != 1)
                {
                    if (workingHashKvpList.Count > 1)
                    {
                        hashKvpsToProcess = workingHashKvpList.OrderBy(x => x.Item1).ToList();
                        workingHashKvpList.Clear();
                    }

                    while (hashKvpsToProcess.Count > 1)
                    {
                        passCount++;

                        Thread.Sleep(Sleep);
                        Console.WriteLine("Pass: " + passCount);

                        // If there are an odd number of items to process then we add the last allowing us to pair up an even number.
                        if (hashKvpsToProcess.Count % 2 == 1)
                        {
                            hashKvpsToProcess.Add(hashKvpsToProcess.Last());
                        }

                        var idx = 0;

                        while (hashKvpsToProcess.Count > 0)
                        {
                            // Add the newly hashed pair, plus their index, to the working list.
                            workingHashKvpList.Add(new Tuple<int, string>(idx, GenerateHash(hashKvpsToProcess[0].Item2, hashKvpsToProcess[1].Item2)));

                            // Remove the pair of items we have processed for when we run through the WHILE loop again.
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

        /// <summary>
        /// Generates an indexed list of hashes based on a list of transactions.
        /// </summary>
        /// <param name="transactions">The list of transactions.</param>
        /// <returns>an >int, string> tuple containing an index and hash string.</returns>
        private static IEnumerable<Tuple<int, string>> GenerateTransactionHashes(IList<Transaction> transactions)
        {
            Console.WriteLine("Hashing transactions...");

            var transactionHashKvpList = new List<Tuple<int, string>>();

            for (var i = 0; i < transactions.Count; i++)
            {
                var input = ConcatenateTransactionData(transactions[i]);
                var hash = GenerateHash(input);

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
            var concatenatedString = string.Empty;

            foreach (var property in transaction.GetType().GetProperties())
            {
                concatenatedString += string.Concat(property.GetValue(transaction));
            }

            return concatenatedString;
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

        /// <summary>
        /// Creates the genesis block - the first block within the chain.
        /// </summary>
        public static void InitiliseBlockchain()
        {
            Console.WriteLine("Initialising blockchain...");
            Thread.Sleep(Sleep);

            Console.WriteLine("Generating genesis block...");
            Thread.Sleep(Sleep);

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

            block.Header.MerkleRootHash = GenerateMerkleRootHash(hashedTransactions);

            Console.WriteLine("Genesis block created...");
            Thread.Sleep(Sleep);

            AddToBlockchain(ValidateBlock(block));
        }

        private static void AddToBlockchain(Block block)
        {
            Console.WriteLine("Current block count: " + GetBlockchain().Count());
            Console.WriteLine("Adding block to blockchain...");
            Thread.Sleep(Sleep);

            if (GetBlockchain().Count() != block.Position)
            {
                Console.WriteLine("Error - block expected position " + block.Position + " is not free within the blockchain!");
                Console.WriteLine("Press any key to retry...");
                Console.Beep();
                Console.ReadLine();

                // Reprocess the block, and reattempt to add it to the blockchain.
                AddToBlockchain(ValidateBlock(block));
            }

            Blockchain.Add(block);

            Console.WriteLine("Block added. Block count: " + GetBlockchain().Count());
            if (ReadLine)
            {
                Console.WriteLine("Press any key to continue...");
                Console.ReadLine();
            }
        }

        /// <summary>
        /// Performs the proof of work algorithm on an unprocessed block.
        /// </summary>
        /// <param name="block">The block to be processed.</param>
        /// <returns>A processed block with the valid hash and nonce inside.</returns>
        private static Block ValidateBlock(Block block)
        {
            Console.WriteLine("Validating block...");
            Thread.Sleep(Sleep);

            var hash = string.Empty;
            var nonce = 0;

            Console.WriteLine("Generating valid hash...");
            Thread.Sleep(Sleep);

            while (!IsHashValid(hash))
            {
                nonce += 1;

                var input = string.Concat(block.Header.MerkleRootHash,
                                          nonce,
                                          block.Header.PreviousHash,
                                          block.Header.Timestamp,
                                          block.MagicNumber,
                                          block.Header);

                hash = GenerateHash(input);
            }

            // Hash is valid.
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
            // Difficulty is the amount of leading zeros this hash has to satisfy.
            var hashValid = hash.StartsWith(Difficulty);

            // TODO: Implement a target to be under?

            return hashValid;
        }

        private static Wallet GetWallet(Guid userId)
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Wallets.First(x => x.UserId == userId);
            }
        }

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
            return Blockchain.OrderBy(x => x.Position).ToList();
        }

        private static IEnumerable<Transaction> GetTransactionPool()
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
