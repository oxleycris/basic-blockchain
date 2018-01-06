using BlockChain.Models;
using Microsoft.EntityFrameworkCore;

namespace BlockChain.Data
{
    public class BlockChainDbContext : DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Wallet> Wallets { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=OxCoin.TransactionGenerator;Trusted_Connection=True;");
        }
    }
}
