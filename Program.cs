using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BlockChain.Data;
using BlockChain.Models;
using BlockChain.Models.OxCoin;

namespace BlockChain
{
    public class Program
    {
        // General TODO LIST 
        // TODO: Somewhere, check a balance before allowing a transaction to occur.

        // DEBUG SETTINGS ////////////////////////////
        private const int Sleep = 500;
        //////////////////////////////////////////////

        public const string Version = "0.1";

        public const int MaximumBlockSize = 1000000;
        public const decimal OxCoinLimit = 1400m;
        public const int OxPiecesPerOxCoin = 1000;

        public const string Difficulty = "000" +
                                         ""; // TODO: Implement proper hex targeting, once I figure it out...

        public static decimal MinerReward = 50;
        public static int MinerRewardBlockchainTarget = 20;
        public const int MinerRewardBlockchainTargetIncrement = 20;

        public static IList<Block> Blockchain = new List<Block>();

        public static void Main(string[] args)
        {
            if (GetGenesisUser() == null)
            {
                GenerateGenesisUserWithWalletId();
            }

            // Initialise the blockchain, and add the genesis block to it.
            InitiliseBlockchain();
            TransferGenesisFundsToUsers();

            #region Balances and Hash List
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

                var walletId = GetUserWallet(user.Id).Id;
                var verifiedBalance = GetBalanceForUser(walletId, out var unVerifiedBalance);
                var vbSpaces = string.Empty;

                for (var i = 0; i < (10 - verifiedBalance.ToString(CultureInfo.InvariantCulture).Length); i++)
                {
                    vbSpaces += " ";
                }

                Console.WriteLine(fullName + fnSpaces + "  |  " + verifiedBalance + vbSpaces + "  |  " + (verifiedBalance + unVerifiedBalance) + " (" + unVerifiedBalance + " unverified)");
            }

            Console.WriteLine("Miner balance: ");
            foreach (var miner in GetMiners())
            {
                Console.WriteLine(miner.WalletId + "  |  " + GetBalanceForMiner(miner.WalletId, out decimal uvb) + "(" + uvb + " unverified)");
            }

            Console.WriteLine("Mining reward: " + GetMiningReward());
            Console.WriteLine("OxCoins remaining: " + GetOxCoinTotalRemaining());
            #endregion

            // Miner.
            // Eventually this will just run as a constant process.
            // Oneday, someday, somehow, somewhere...
            while (true)
            {
                var transactionsToProcess = GetTransactionsToProcess().ToList();

                if (transactionsToProcess.Count > 0)
                {
                    var block = CreateBlock(transactionsToProcess);

                    var startTime = DateTime.Now;
                    var validBlock = ValidateBlock(block);
                    var finishTime = DateTime.Now;

                    var difference = finishTime.Subtract(startTime);
                    Console.WriteLine("Time taken to find proof: (seconds): " + difference.Seconds);
                    Console.WriteLine("Time Difference (minutes): " + difference.Minutes);
                    Thread.Sleep(Sleep);

                    AddToBlockchain(validBlock);

                    // Every 10 valid blocks added to the blockchain causes the mining reward to half,
                    // and the target to be increased.
                    if (GetBlockchain().Count() == MinerRewardBlockchainTarget)
                    {
                        MinerRewardBlockchainTarget += MinerRewardBlockchainTargetIncrement;
                        MinerReward = MinerReward / 2m;
                    }

                    if (GetOxCoinTotalRemaining() >= MinerReward)
                    {
                        RewardMiner(GetRandomMiner());
                    }

                    // ???
                    // Profit!

                    #region Balances and Hash List
                    Console.WriteLine("Transactions added:" + validBlock.TransactionCount);
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

                        var walletId = GetUserWallet(user.Id).Id;
                        var verifiedBalance = GetBalanceForUser(walletId, out var unVerifiedBalance);
                        var vbSpaces = string.Empty;

                        for (var i = 0; i < (10 - verifiedBalance.ToString(CultureInfo.InvariantCulture).Length); i++)
                        {
                            vbSpaces += " ";
                        }

                        Console.WriteLine(fullName + fnSpaces + "  |  " + verifiedBalance + vbSpaces + "  |  " + (verifiedBalance + unVerifiedBalance) + " (" + unVerifiedBalance + " unverified)");
                    }

                    Console.WriteLine("Miner balance: ");

                    foreach (var miner in GetMiners())
                    {
                        Console.WriteLine(miner.WalletId + "  |  " + GetBalanceForMiner(miner.WalletId, out decimal uvb) + "(" + uvb + " unverified)");
                    }

                    Console.WriteLine("Mining reward: " + GetMiningReward());
                    Console.WriteLine("OxCoins remaining: " + GetOxCoinTotalRemaining());
                    #endregion
                }
                else
                {
                    Console.WriteLine("Waiting for the transaction pool to populate...");
                    Thread.Sleep(Sleep);
                }
            }
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
            var transactionsToProcess = new List<Transaction>();

            if (transactionPool.Count > 1)
            {
                var workingList = transactionPool.Where(x => x.Timestamp <= DateTime.Now.AddMonths(-3)).ToList();

                foreach (var transaction in workingList)
                {
                    transactionPool.Remove(transaction);
                }

                workingList.AddRange(transactionPool.OrderByDescending(x => x.Size));

                var maximumTransactionSize = MaximumBlockSize;

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

                    return transactionsToProcess;
                }
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
            Transaction trnx1;

            using (var db = new BlockChainDbContext())
            {
                trnx1 = db.Transactions.First(x => x.Id == transaction.Id);
            }

            using (var db = new BlockChainDbContext())
            {
                db.Transactions.Remove(trnx1);
                db.SaveChanges();
            }
        }

        /// <summary>
        /// Gets the verified and unverified balances for a wallet.
        /// </summary>
        /// <param name="walletId">the wallet id.</param>
        /// <param name="unVerifiedBalance">The unverified balance.</param>
        /// <returns>The verified balance.</returns>
        private static decimal GetBalanceForUser(Guid walletId, out decimal unVerifiedBalance)
        {
            var uvb = 0m;
            var balance = 0m;
            var blockchainTransactions = new List<Transaction>();
            var transactionPoolTransactions = new List<Transaction>();
            var userTransactions = new List<Transaction>();
            var userWallets = GetAllWallets().ToList();

            // Get all the verified transactions from the blockchain.
            foreach (var block in GetBlockchain())
            {
                blockchainTransactions.AddRange(block.Transactions);
            }

            transactionPoolTransactions.AddRange(GetTransactionPool());

            foreach (var wallet in userWallets)
            {
                userTransactions.AddRange(blockchainTransactions.Where(x => x.DestinationWalletId == wallet.Id));
            }

            balance = userTransactions.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            balance -= userTransactions.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferedAmount);

            userTransactions.Clear();

            foreach (var wallet in userWallets)
            {
                userTransactions.AddRange(transactionPoolTransactions.Where(x => x.DestinationWalletId == wallet.Id));
            }

            uvb = userTransactions.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            uvb -= userTransactions.Where(x => x.SourceWalletId == walletId).Sum(x => x.TransferedAmount);

            unVerifiedBalance = uvb;

            return balance;
        }

        /// <summary>
        /// Gets the verified and unverified balances for a wallet.
        /// </summary>
        /// <param name="walletId">the wallet id.</param>
        /// <param name="unVerifiedBalance">The unverified balance.</param>
        /// <returns>The verified balance.</returns>
        private static decimal GetBalanceForMiner(Guid walletId, out decimal unVerifiedBalance)
        {
            var balance = 0m;
            var blockchainTransactions = new List<Transaction>();
            var transactionPoolTransactions = new List<Transaction>();
            var minerVerifiedTransactions = new List<Transaction>();
            var minerUnverifiedTransactions = new List<Transaction>();

            // Get all the verified transactions from the blockchain.
            foreach (var block in GetBlockchain())
            {
                blockchainTransactions.AddRange(block.Transactions);
            }

            transactionPoolTransactions.AddRange(GetTransactionPool());

            foreach (var miner in GetMiners())
            {
                minerVerifiedTransactions.AddRange(blockchainTransactions.Where(x => x.DestinationWalletId == miner.WalletId));
                minerUnverifiedTransactions.AddRange(transactionPoolTransactions.Where(x => x.DestinationWalletId == miner.WalletId));
            }

            balance = minerVerifiedTransactions.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);
            unVerifiedBalance = minerUnverifiedTransactions.Where(x => x.DestinationWalletId == walletId).Sum(x => x.TransferedAmount);

            return balance;
        }

        private static void TransferGenesisFundsToUsers()
        {
            Console.WriteLine("Generating transactions...");
            Thread.Sleep(Sleep);

            var transactionsList = new List<Transaction>();
            var genesisUser = GetGenesisUser();
            var wallets = GetUserWallets().ToList();

            foreach (var wallet in wallets)
            {
                var sourceWalletId = GetUserWallet(genesisUser.Id).Id;
                var destinationWalletId = wallet.Id;
                var transaction = new Transaction
                {
                    SourceWalletId = sourceWalletId,
                    DestinationWalletId = destinationWalletId,
                    TransferedAmount = 50m,
                    Timestamp = DateTime.Now
                };

                // TODO: Figure out how to get this users OxPieces to reassign some of them to this destinationWalletId.
                transactionsList.Add(transaction);
            }

            var block = CreateBlock(transactionsList);

            var startTime = DateTime.Now;

            AddToBlockchain(ValidateBlock(block));

            var finishTime = DateTime.Now;
            var difference = finishTime.Subtract(startTime);

            Console.WriteLine("Time taken to find proof: (seconds): " + difference.Seconds);
            Console.WriteLine("Time Difference (minutes): " + difference.Minutes);
            Thread.Sleep(Sleep);
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
                    }
                }

                Console.WriteLine("Merkle root: " + workingHashKvpList.First().Item2);

                return workingHashKvpList.First().Item2;
            }

            // Only one transaction was passed into the method - probably for creation of the genesis block.
            // Therefore we put the value in twice, like we did above.
            var root = GenerateHash(hashKvpsToProcess.First().Item2, hashKvpsToProcess.First().Item2);

            Console.WriteLine("Merkle root: " + root);

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

            return transactionHashKvpList;
        }

        private static string ConcatenateTransactionData(Transaction transaction)
        {
            var oxPieces = transaction.OxPieces;
            var oxPieceCollectionDataString = new StringBuilder();

            // Take data from transaction.OxCollection first and build a string of all values, 
            // across all items within the collection.
            foreach (var oxPiece in oxPieces)
            {
                var oxPieceDataString = new StringBuilder();

                foreach (var property in oxPiece.GetType().GetProperties())
                {
                    oxPieceDataString.Append(property.GetValue(oxPiece));
                }

                oxPieceCollectionDataString.Append(oxPieceDataString);
            }

            // Then build a string of all the remaining transaction data in a seperate string.
            var transactionDataString = new StringBuilder();

            foreach (var property in transaction.GetType().GetProperties())
            {
                if (property.Name != nameof(Transaction.OxPieces))
                {
                    transactionDataString.Append(property.GetValue(transaction));
                }
            }

            // Return the combined string.
            return string.Concat(transactionDataString.Replace(" ", string.Empty), oxPieceCollectionDataString.Replace(" ", string.Empty));
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

        private static void InitiliseBlockchain()
        {
            Console.WriteLine("Initialising blockchain...");
            Thread.Sleep(Sleep);

            Console.WriteLine("Generating genesis block...");
            Thread.Sleep(Sleep);

            const string previousHash = "0000000000000000000000000000000000000000000000000000000000000000";

            var transaction = new Transaction
            {
                SourceWalletId = GetUserWallet(GetGenesisUser().Id).Id,
                DestinationWalletId = GetUserWallet(GetGenesisUser().Id).Id,
                Timestamp = DateTime.Now,
                Size = MaximumBlockSize
            };

            var oxPieces = new List<OxPiece>();

            for (var i = 0; i < OxCoinLimit * OxPiecesPerOxCoin; i++)
            {
                oxPieces.Add(new OxPiece { WalletId = transaction.DestinationWalletId });
            }

            transaction.OxPieces = oxPieces;
            transaction.TransferedAmount = (decimal)transaction.OxPieces.Count() / OxPiecesPerOxCoin;

            var transactions = new List<Transaction> { transaction };

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

            var startTime = DateTime.Now;

            AddToBlockchain(ValidateBlock(block));

            var finishTime = DateTime.Now;

            var difference = finishTime.Subtract(startTime);

            Console.WriteLine("Time taken to find proof: (seconds): " + difference.Seconds);
            Console.WriteLine("Time Difference (minutes): " + difference.Minutes);
            Thread.Sleep(Sleep);

        }

        private static void RewardMiner(Miner miner)
        {
            var genesisUserId = GetGenesisUser().Id;

            var transaction = new Transaction
            {
                SourceWalletId = GetUserWallet(genesisUserId).Id,
                DestinationWalletId = miner.WalletId,
                TransferedAmount = GetMiningReward(),
                Timestamp = DateTime.Now
            };

            AddToTransactionPool(transaction);
        }

        private static decimal GetMiningReward()
        {
            return MinerReward;
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

                var input = new StringBuilder();

                foreach (var property in block.Header.GetType().GetProperties())
                {
                    if (property.Name != nameof(BlockHeader.ValidHash))
                    {
                        input.Append(property.GetValue(block.Header));
                    }
                }

                input.Append(nonce);

                hash = GenerateHash(input.Replace(" ", string.Empty).ToString());
            }

            // Hash is valid.
            block.Header.Nonce = nonce;
            block.Header.ValidHash = hash;

            Console.WriteLine("[VALID] | Nonce: " + nonce + " | Hash: " + hash);

            return block;
        }

        private static bool IsHashValid(string hash)
        {
            // Difficulty is the amount of leading zeros this hash has to satisfy.
            var hashValid = hash.StartsWith(Difficulty);

            // TODO: Implement a target to be under?

            return hashValid;
        }

        private static Wallet GetUserWallet(Guid userId)
        {
            using (var db = new BlockChainDbContext())
            {
                // A standard user has two wallets - their user wallet, and their miner wallet.
                var userWallets = db.Wallets.Where(x => x.UserId == userId).ToList();

                // Genesis user.
                if (userWallets.Count == 1)
                {
                    return userWallets.First();
                }

                var userMiner = new Miner();

                foreach (var userWallet in userWallets)
                {
                    var miner = db.Miners.FirstOrDefault(x => x.WalletId == userWallet.Id);

                    if (miner != null)
                    {
                        userMiner = miner;
                    }
                }

                return userWallets.First(x => x.Id != userMiner.WalletId);
            }
        }

        private static User GetGenesisUser()
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Users.FirstOrDefault(x => x.GivenName == "Network" && x.FamilyName == "Admin");
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
                foreach (var user in db.Users.Where(x => x.Id != GetGenesisUser().Id))
                {
                    yield return user;
                }
            }
        }

        private static IEnumerable<Wallet> GetAllWallets()
        {
            using (var db = new BlockChainDbContext())
            {
                foreach (var wallet in db.Wallets.Where(x => x.UserId != GetGenesisUser().Id))
                {
                    yield return wallet;
                }
            }
        }

        private static IEnumerable<Wallet> GetMinerWallets()
        {
            var miners = GetMiners().ToList();
            var minersWallets = new List<Wallet>();

            foreach (var miner in miners)
            {
                var wallet = GetWallet(miner.WalletId);

                minersWallets.Add(wallet);
            }

            return minersWallets;
        }

        private static Wallet GetWallet(Guid walletId)
        {
            using (var db = new BlockChainDbContext())
            {
                return db.Wallets.First(x => x.Id == walletId);
            }
        }

        private static IEnumerable<Wallet> GetUserWallets()
        {
            var wallets = GetAllWallets().ToList();
            var minersWallets = GetMinerWallets().ToList();

            foreach (var minersWallet in minersWallets)
            {
                wallets.RemoveAll(x => x.Id == minersWallet.Id);
            }

            return wallets;
        }

        private static Miner GetRandomMiner()
        {
            var miners = new List<Miner>();

            using (var db = new BlockChainDbContext())
            {
                miners.AddRange(db.Miners);
            }

            miners.Shuffle();

            return miners[new Random().Next(0, miners.Count - 1)];
        }

        private static IEnumerable<Miner> GetMiners()
        {
            using (var db = new BlockChainDbContext())
            {
                foreach (var miner in db.Miners)
                {
                    yield return miner;
                }
            }
        }

        private static decimal GetOxCoinTotalRemaining()
        {
            var total = OxCoinLimit;

            foreach (var wallet in GetAllWallets())
            {
                // TODO: Check what happens to unVerified here.....
                total -= GetBalanceForUser(wallet.Id, out var unVerified);
                total -= GetBalanceForMiner(wallet.Id, out unVerified);
                total -= unVerified;
            }

            return total;
        }

        private static string GetLastHash()
        {
            return GetBlockchain().Last().Header.ValidHash;
        }

        private static int NextAvailableIndexPosition()
        {
            return GetBlockchain().Count();
        }

        private static void GenerateGenesisUserWithWalletId()
        {
            var genesisUser = new User
            {
                GivenName = "Network",
                FamilyName = "Admin"
            };

            AddUser(genesisUser);
            AddWallet(new Wallet { UserId = GetGenesisUser().Id });
        }

        private static void AddUser(User genesisUser)
        {
            using (var db = new BlockChainDbContext())
            {
                db.Users.Add(genesisUser);
                db.SaveChanges();
            }
        }

        private static void AddWallet(Wallet wallet)
        {
            using (var db = new BlockChainDbContext())
            {
                db.Wallets.Add(wallet);
                db.SaveChanges();
            }
        }
    }

    #region Etcetera
    public static class ThreadSafeRandom
    {
        [ThreadStatic] private static Random _local;

        public static Random ThisThreadsRandom => _local ?? (_local = new Random(unchecked(Environment.TickCount * 31 + Thread.CurrentThread.ManagedThreadId)));
    }

    internal static class ListExtensions
    {
        public static void Shuffle<T>(this IList<T> list)
        {
            var n = list.Count;
            while (n > 1)
            {
                n--;
                var k = ThreadSafeRandom.ThisThreadsRandom.Next(n + 1);
                var value = list[k];

                list[k] = list[n];
                list[n] = value;
            }
        }
    }
    #endregion
}
