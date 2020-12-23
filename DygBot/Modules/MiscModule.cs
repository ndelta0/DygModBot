using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

using DygBot.Addons;
using DygBot.Addons.EmoteActioner;
using DygBot.Models;
using DygBot.Preconditions;
using DygBot.Services;

using SixLabors.ImageSharp.ColorSpaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DygBot.Modules
{
    public class MiscModule : ExtendedInteractiveBase
    {
        private readonly GitHubService _git;
        private readonly HttpClient _http;
        private readonly DiscordSocketClient _discord;

        public MiscModule(GitHubService gitHub, HttpClient httpClient, DiscordSocketClient discord)
        {
            _git = gitHub;
            _http = httpClient;
            _discord = discord;
        }

        [Command("roles")]
        [Summary("Pokazuje role użytkownika")]
        [Scope(Scope.Guild)]
        public async Task RolesAsync([Summary("Użytkownik")] SocketGuildUser user = null)
        {
            if (user == null)
            {
                user = Context.Guild.GetUser(Context.User.Id);
            }
            else
            {
                if (!_git.Config.Servers[Context.Guild.Id].ManagementRoles.Any(x => (Context.User as SocketGuildUser).Roles.Select(r => r.Id).Any(y => y == x)))
                {
                    await ReplyAsync("Możesz sprawdzić tylko swoje role.");
                    return;
                }
            }

            var roles = user.Roles.SkipWhile(x => x.IsEveryone);

            var description = new StringBuilder().AppendJoin('\n', roles).ToString();

            var embed = new EmbedBuilder
            {
                Title = $"Role użytkownika {user.Username}#{user.Discriminator}",
                Description = description
            }.WithCurrentTimestamp().Build();

            await ReplyAsync(embed: embed);
        }


        [Command("oc")]
        public async Task OcAsync()
        {
            if (Context.IsPrivate)
            {
                
                if (_git.Config.Servers[683084560451633212].OcChannels.Count == 0)
                {
                    await ReplyAndDeleteAsync("Anonimowe wysyłanie wiadomości nie jest włączone na tym serwerze", timeout: TimeSpan.FromSeconds(5));
                    return;
                }
                if (await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id) == null)
                {
                    await Context.Channel.SendMessageAsync("Musisz być na serwerze, żeby skorzystać z tej komendy");
                    return;
                }
                else if (_discord.GetGuild(683084560451633212).GetRole(ulong.Parse(_git.Config.Servers[683084560451633212].AdditionalConfig["oc.postRole"])).Members.Any())
                {
                    await Context.Channel.SendMessageAsync("Aktualnie kanał oc-anonymous jest zajęty przez innego użytkownika. Spróbuj ponownie za chwilę.");
                    return;
                }
                else
                {
                    var role = _discord.GetGuild(683084560451633212).GetRole(ulong.Parse(_git.Config.Servers[683084560451633212].AdditionalConfig["oc.postRole"]));
                    var member = _discord.GetGuild(683084560451633212).GetUser(Context.User.Id);
                    await member.AddRoleAsync(role);
                    var channel = _discord.GetGuild(683084560451633212).GetTextChannel(ulong.Parse(_git.Config.Servers[683084560451633212].AdditionalConfig["oc.postChannel"]));
                    await Context.Channel.SendMessageAsync($"Powtórz komendę na kanale {channel.Mention}");
                    var tmpMsg = await channel.SendMessageAsync("Powtórz komendę tutaj");
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5));
                        await member.RemoveRoleAsync(role);
                    });
                    _ = Task.Run(async () =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(30));
                        await tmpMsg.DeleteAsync();
                    });
                }
            }
            else
            {
                try
                {
                    await Context.Message.DeleteAsync();
                }
                catch
                { }
                var user = Context.User as SocketGuildUser;

                ulong channelId = 0;
                foreach (var role in user.Roles.Select(x => x.Id))
                {
                    if (_git.Config.Servers[Context.Guild.Id].OcChannels.TryGetValue(role, out channelId))
                        break;
                }

                if (channelId == 0)
                {
                    await ReplyAsync("Twój zestaw roli nie pozwala na wysyłanie anonimowych zdjęć na kanałach OC.");
                    await Task.Delay(10000);
                    await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                    await DeleteAllChannelMessagesAsync(Context.Channel as SocketTextChannel);
                    return;
                }
                var embed = new EmbedBuilder()
                    .WithTitle("Wyślij zdjęcie i opcjonalny opis")
                    .WithDescription($"Wyślij tylko jedno zdjęcie w załączniku. Do zdjęcia możesz dodać wiadomość, która zostanie dołączona do zdjęcia na kanale. Jeśli nie chcesz opisu, nic nie pisz.\nZdjęcie zostanie wysłane na kanał <#{channelId}>")
                    .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
                    .WithFooter(footer =>
                    {
                        footer.WithText("Wyślij 'cancel', aby anulować");
                    })
                    .Build();
                var embedMsg = await ReplyAsync(embed: embed);
                var response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                if (response == null)
                {
                    await embedMsg.DeleteAsync();
                    await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                    await Task.Delay(5000);
                    await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                    await DeleteAllChannelMessagesAsync(Context.Channel as SocketTextChannel);
                    return;
                }
                else if (response.Content.ToLower() == "cancel")
                {
                    await response.DeleteAsync();
                    await embedMsg.DeleteAsync();
                    await ReplyAsync("Wysyłanie zdjęcia anulowane");
                    await Task.Delay(5000);
                    await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                    await DeleteAllChannelMessagesAsync(Context.Channel as SocketTextChannel);
                    return;
                }
                else if (response.Attachments.Count != 1)
                {
                    await embedMsg.DeleteAsync();
                    await ReplyAsync($"Anulowano. Otrzymano zdjęć: {response.Attachments.Count}\nOczekiwano: 1");
                    await Task.Delay(5000);
                    await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                    await DeleteAllChannelMessagesAsync(Context.Channel as SocketTextChannel);
                    return;
                }
                var description = response.Content;

                var url = response.Attachments.ElementAt(0).Url;

                var extension = response.Attachments.ElementAt(0).Filename.Split('.').Last();

                var imgStream = await (await _http.GetAsync(url)).Content.ReadAsStreamAsync();

                var fileMsg = await Context.Guild.GetTextChannel(channelId).SendFileAsync(imgStream, $"anonymous-oc.{extension}", description);

                await fileMsg.AddReactionAsync(Emote.Parse("<:1dyg:708782038521741352>"));

                var discordUrl = fileMsg.Attachments.ElementAt(0).Url;

                await embedMsg.ModifyAsync(x =>
                {
                    x.Embed = null;
                    x.Content = "Zdjęcie wysłane";
                });

                var logEmbed = new EmbedBuilder()
                   .WithTitle("Anonimowe zdjęcie na OC")
                   .WithDescription("======================")
                   .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
                   .WithThumbnailUrl(Context.Message.Author.GetAvatarUrlSafe())
                   .WithImageUrl(discordUrl)
                   .AddField("Użytkownik", $"{Context.Message.Author.Mention} ({Context.Message.Author.Id})")
                   .AddField("Opis", $"{(string.IsNullOrWhiteSpace(description) ? "(brak)" : description)}")
                   .AddField("Kanał", $"{Context.Guild.GetTextChannel(channelId).Mention}")
                   .Build();

                try
                {
                    await response.DeleteAsync();
                }
                catch
                { }

                if (_git.Config.Servers[Context.Guild.Id].LogChannel != default)
                    await Context.Guild.GetTextChannel(_git.Config.Servers[Context.Guild.Id].LogChannel).SendMessageAsync(embed: logEmbed);

                await Task.Delay(5000);
                await embedMsg.DeleteAsync();
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                await DeleteAllChannelMessagesAsync(Context.Channel as SocketTextChannel);
            }
        }

        [Command("new")]
        [Scope(Scope.DM)]
        public async Task NewAsync()
        {
            //_discord.GetGuild(685477359213608960).GetTextChannel(688410550056648795)

            var embed = new EmbedBuilder()
                    .WithTitle("Pytanie 1/3")
                    .WithDescription("Jakiej jesteś płci?")
                    .WithColor(_git.Config.Servers[683084560451633212].ServerColor)
                    .WithFooter(footer =>
                    {
                        footer.WithText("Wyślij 'cancel', aby anulować (krok 1/3)");
                    })
                    .Build();
            var embedMsg = await ReplyAsync(embed: embed);
            var response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
            if (response == null)
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                await Task.Delay(5000);
                return;
            }
            else if (response.Content.ToLower() == "cancel")
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync("Wysyłanie ankiety anulowane");
                await Task.Delay(5000);
                return;
            }
            var genderStr = response.Content;

            embed = new EmbedBuilder()
                    .WithTitle("Pytanie 2/3")
                    .WithDescription("Ile masz lat?")
                    .WithColor(_git.Config.Servers[683084560451633212].ServerColor)
                    .WithFooter(footer =>
                    {
                        footer.WithText("Wyślij 'cancel', aby anulować (krok 2/3)");
                    })
                    .Build();
            embedMsg = await ReplyAsync(embed: embed);
            response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
            if (response == null)
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                return;
            }
            else if (response.Content.ToLower() == "cancel")
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync("Wysyłanie ankiety anulowane");
                return;
            }
            var ageStr = response.Content;

            embed = new EmbedBuilder()
                    .WithTitle("Pytanie 3/3")
                    .WithDescription("Czy chcesz otrzymywać wiadomości prywatne od innych użytkowników?")
                    .WithColor(_git.Config.Servers[683084560451633212].ServerColor)
                    .WithFooter(footer =>
                    {
                        footer.WithText("Wyślij 'cancel', aby anulować (krok 3/3)");
                    })
                    .Build();
            embedMsg = await ReplyAsync(embed: embed);
            response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
            if (response == null)
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                return;
            }
            else if (response.Content.ToLower() == "cancel")
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync("Wysyłanie ankiety anulowane");
                return;
            }
            var dmOpenStr = response.Content;

            var channel = _discord.GetGuild(683084560451633212).GetTextChannel(779049131028643860);

            var guildEmbed = new EmbedBuilder()
                    .WithAuthor($"{Context.User.Username}#{Context.User.Discriminator}")
                    .WithTitle("Wypełniona ankieta")
                    .WithDescription("================")
                    .WithColor(_git.Config.Servers[683084560451633212].ServerColor)
                    .WithCurrentTimestamp()
                    .WithThumbnailUrl(Context.User.GetAvatarUrlSafe())
                    .AddField("Użytkownik", $"{Context.User.Mention} ({Context.User.Id})")
                    .AddField("Płeć", genderStr)
                    .AddField("Wiek", ageStr)
                    .AddField("Wiadomości", dmOpenStr)
                    .Build();

            await ReplyAsync("Dziękujemy za wypełnienie aplikacji. Twoje role zostaną przyznane przez administrację najszybciej jak to możliwe.");

            await channel.SendMessageAsync(Context.User.Mention, embed: guildEmbed, emotes: new IEmote[]
            {
                new Emoji("🔵"),     // blue
                new Emoji("🔴"),     // red
                new Emoji("🟣"),       // purple
                new Emoji("⭕"),      // circle
                new Emoji("🚫"),     // no entry sign
                new Emoji("⚪"),     // white
                new Emoji("⛔"),     // no entry
                new Emoji("✅"),     // tick
                new Emoji("❌"),     // cross
                new Emoji("🆔")      // id
            });
        }

        [Command("ping")]
        public async Task PingAsync()
        {
            var pingFromUser = (int)Math.Round(Math.Abs((DateTimeOffset.Now - Context.Message.CreatedAt).TotalMilliseconds));
            var pingToAPI = _discord.Latency;

            await ReplyAsync($"```" +
                             $"Ping to Discord's API: {pingToAPI}ms\n" +
                             $"Ping to you:           {pingFromUser}ms```");
        }

        private static async Task DeleteAllChannelMessagesAsync(SocketTextChannel channel)
        {
            bool moreRemaining = true;
            ulong lastMsgId = 0;
            do
            {
                var tmpMsgs = lastMsgId != 0 ? channel.GetMessagesAsync(lastMsgId, Direction.Before) : channel.GetMessagesAsync();

                var msgs = await tmpMsgs.FlattenAsync();
                moreRemaining = msgs.Any();
                if (!moreRemaining)
                    continue;
                msgs.ToList().ForEach(async x =>
                {
                    await x.DeleteAsync();
                });
                lastMsgId = msgs.Last().Id;
            } while (moreRemaining);
        }
    }
}
