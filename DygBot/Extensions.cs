using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using DygBot.Models;

namespace DygBot
{
    public static class Extensions
    {
        public static T Random<T>(this IEnumerable<T> enumerable)
        {
            var rand = new Random(enumerable.GetHashCode() + (int)DateTime.UtcNow.Ticks);
            int index = rand.Next(0, enumerable.Count());
            return enumerable.ElementAt(index);
        }
        public static T Random<T>(this IEnumerable<T> enumerable, int seed)
        {
            var rand = new Random(seed + enumerable.GetHashCode() + (int)DateTime.UtcNow.Ticks);
            int index = rand.Next(0, enumerable.Count());
            return enumerable.ElementAt(index);
        }

        public static string ToLocalString(this Gender gender)
            => gender switch
            {
                Gender.Female => "Kobieta",
                Gender.Male => "Mężczyzna",
                _ => "Inna",
            };

        public static string ToLocalString(this bool b)
            => b switch
            {
                false => "Nie",
                true => "Tak"
            };

        public static async Task<SocketGuildUser> GetUserSafeAsync(this SocketGuild guild, ulong userId)
        {
            var user = guild.GetUser(userId);
            if (user == null)
            {
                await guild.DownloadUsersAsync();
                user = guild.GetUser(userId);
                if (user == null)
                {
                    return null;
                }
            }
            return user;
        }
    }
}
