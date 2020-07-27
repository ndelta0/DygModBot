using DygBot.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;
using System;
using System.Text.RegularExpressions;

namespace DygBot.Models
{
    public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext(string[] args)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
            optionsBuilder.UseMySql(GetMySqlConnectionString(), mySqlOptions => mySqlOptions
                        .ServerVersion(new Version(5, 6, 47), ServerType.MySql));

            var dbContext = new AppDbContext(optionsBuilder.Options);
            dbContext.Database.Migrate();
            return dbContext;
        }

        private static string GetMySqlConnectionString()
        {
            string rawString = Environment.GetEnvironmentVariable("CLEARDB_DATABASE_URL") ?? throw new ArgumentNullException();

            var user = new Regex("/([A-z0-9]*):").Match(rawString).Groups[1].Value;
            var password = new Regex(":([A-z0-9]*)@").Match(rawString).Groups[1].Value;
            var server = new Regex("@([-A-z0-9/_.]*)/").Match(rawString).Groups[1].Value;
            var database = new Regex("/(heroku_[A-z0-9]*)").Match(rawString).Groups[1].Value;

            return $"Server={server}; Database={database}; Uid={user}; Pwd={password}";
        }
    }
}
