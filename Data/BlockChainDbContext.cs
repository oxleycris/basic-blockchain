using Microsoft.EntityFrameworkCore;

namespace BlockChain.Data
{
    public class BloggingContext : DbContext
    {
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(@"Server=(localdb)\mssqllocaldb;Database=BlockChain;Trusted_Connection=True;");
        }

        //public DbSet<Blog> Blogs { get; set; }
        //public DbSet<Post> Posts { get; set; }
    }
}
