using DygBot.Models;
using Microsoft.EntityFrameworkCore;

namespace DygBot.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<DetailStat> DetailStat { get; set; }
        public DbSet<GeneralStat> GeneralStats { get; set; }
        public DbSet<Ban> Bans { get; set; }
        public DbSet<Warn> Warns { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
