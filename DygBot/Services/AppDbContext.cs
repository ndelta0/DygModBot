using DygBot.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace DygBot.Services
{
    public class AppDbContext : DbContext
    {
        public DbSet<DetailStat> DetailStat { get; set; }
        public DbSet<GeneralStat> GeneralStats { get; set; }

        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
        }
    }
}
