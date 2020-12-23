using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.WebSocket;

using DygBot.Models;
using DygBot.Services;

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
                while (CommandHandler.FinishedInit) { await Task.Delay(100); }
                await guild.DownloadUsersAsync();
                user = guild.GetUser(userId);
                if (user == null)
                {
                    return null;
                }
            }
            return user;
        }

        public static string GetAvatarUrlSafe(this IUser user, ImageFormat format = ImageFormat.Auto, ushort size = 128)
            => user.GetAvatarUrl(format, size) ?? user.GetDefaultAvatarUrl();

        public static async Task<IUserMessage> SendMessageAsync(this IMessageChannel channel, string text = null, bool isTTS = false, Embed embed = null, RequestOptions options = null, MessageReference messageReference = null, IEmote[] emotes = null, RequestOptions emotesOptions = null)
        {
            var message = await channel.SendMessageAsync(text, isTTS, embed, options, messageReference);
            if (emotes != null)
            {
                await message.AddReactionsAsync(emotes, emotesOptions);
            }
            return message;
        }

        public static bool IsEqual(this IEmote emote, IEmote other)
        {
            return emote.Name == other.Name;
        }
    }
}
