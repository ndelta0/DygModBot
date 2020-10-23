using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

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
    public class MiscModule : InteractiveBase<SocketCommandContext>
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
        public async Task RolesAsync([Summary("Użytkownik")] SocketGuildUser user = null)
        {
            if (user == null)
            {
                if (Context.User is SocketGuildUser)
                {
                    user = Context.User as SocketGuildUser;
                }
            }
            else
            {
                if (!_git.Config.Servers[Context.Guild.Id].ManagementRoles.Any(x => (Context.User as SocketGuildUser).Roles.Select(r => r.Id).Any(y => y == x)))
                {
                    await ReplyAsync("Możesz sprawdzić tylko swoje role.");
                    return;
                }
            }

            var roles = user.Roles.TakeWhile(x => !x.IsEveryone);

            var description = new StringBuilder().AppendJoin('\n', roles).ToString();

            var embed = new EmbedBuilder
            {
                Title = $"Role użytkownika {user.Username}#{user.Discriminator}",
                Description = description
            }.WithCurrentTimestamp().Build();

            await ReplyAsync(embed: embed);
        }

        //[Command("oc")]
        //[Summary("Anonimowo wysyła OC na kanał")]
        //public async Task OcAsync()
        //{
        //    await Context.Message.DeleteAsync();
        //    var user = Context.User as SocketGuildUser;
        //    if (_git.Config.Servers[Context.Guild.Id].OcChannels.Count == 0)
        //    {
        //        await ReplyAndDeleteAsync("Anonimowe wysyłanie wiadomości nie jest włączone na tym serwerze", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }
        //    var strList = new List<string>();
        //    foreach (var kvp in _git.Config.Servers[Context.Guild.Id].OcChannels)
        //    {
        //        strList.Add($"• {(int)kvp.Key} - <#{kvp.Value}>");
        //    }
        //    var embed = new EmbedBuilder()
        //                         .WithTitle("Wybierz docelowy kanał na swoje zdjęcie")
        //                         .WithDescription($"Dostępne opcje:\n{new StringBuilder().AppendJoin('\n', strList)}")
        //                         .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
        //                         .WithFooter(footer =>
        //                         {
        //                             footer.WithText("Krok 1/2 (wyślij 'cancel', aby anulować)");
        //                         })
        //                         .Build();
        //    var embedMsg = await ReplyAsync(embed: embed);

        //    var response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
        //    if (response == null)
        //    {
        //        await embedMsg.DeleteAsync();
        //        await ReplyAndDeleteAsync("Czas minął, spróbuj jeszcze raz", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }
        //    else if (response.Content.ToLower() == "cancel")
        //    {
        //        await response.DeleteAsync();
        //        await embedMsg.DeleteAsync();
        //        await ReplyAndDeleteAsync("Wysyłanie zdjęcia anulowane", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }

        //    var gender = (Gender)int.Parse(response.Content);
        //    if (!_git.Config.Servers[Context.Guild.Id].OcChannels.TryGetValue(gender, out ulong channel))
        //    {
        //        await embedMsg.DeleteAsync();
        //        await ReplyAndDeleteAsync("Zły kanał", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }

        //    await response.DeleteAsync();
        //    embed = new EmbedBuilder()
        //        .WithTitle("Wyślij zdjęcie i opcjonalny opis")
        //        .WithDescription("Wyślij tylko jedno zdjęcie w załączniku. Do zdjęcia możesz dodać wiadomość, która zostanie dołączona do zdjęcia na kanale. Jeśli nie chcesz opisu, nic nie pisz.")
        //        .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
        //        .WithFooter(footer =>
        //        {
        //            footer.WithText("Krok 2/2 (wyślij 'cancel', aby anulować)");
        //        })
        //        .Build();
        //    await embedMsg.ModifyAsync((x) =>
        //    {
        //        x.Embed = embed;
        //    });
        //    response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
        //    if (response == null)
        //    {
        //        await embedMsg.DeleteAsync();
        //        await ReplyAndDeleteAsync("Czas minął, spróbuj jeszcze raz", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }
        //    else if (response.Content.ToLower() == "cancel")
        //    {
        //        await response.DeleteAsync();
        //        await embedMsg.DeleteAsync();
        //        await ReplyAndDeleteAsync("Wysyłanie zdjęcia anulowane", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }
        //    else if (response.Attachments.Count != 1)
        //    {
        //        await embedMsg.DeleteAsync();
        //        await ReplyAndDeleteAsync($"Anulowano. Otrzymano zdjęć: {response.Attachments.Count} - oczekiwano: 1", timeout: TimeSpan.FromSeconds(5));
        //        await Task.Delay(5000);
        //        await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //        await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //        return;
        //    }
        //    var description = response.Content;

        //    var url = response.Attachments.ElementAt(0).Url;

        //    var extension = response.Attachments.ElementAt(0).Filename.Split('.').Last();

        //    var imgStream = await (await _http.GetAsync(url)).Content.ReadAsStreamAsync();

        //    var fileMsg = await Context.Guild.GetTextChannel(channel).SendFileAsync(imgStream, $"anonymous-oc.{extension}", description);

        //    await fileMsg.AddReactionAsync(Emote.Parse("<:1dyg:708782038521741352>"));

        //    var discordUrl = fileMsg.Attachments.ElementAt(0).Url;

        //    await embedMsg.ModifyAsync(x =>
        //    {
        //        x.Embed = null;
        //        x.Content = "Zdjęcie wysłane";
        //    });

        //    var logEmbed = new EmbedBuilder()
        //       .WithTitle("Anonimowe zdjęcie na OC")
        //       .WithDescription("======================")
        //       .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
        //       .WithThumbnailUrl(Context.Message.Author.GetAvatarUrl())
        //       .WithImageUrl(discordUrl)
        //       .AddField("Użytkownik", $"{Context.Message.Author.Mention} ({Context.Message.Author.Id})")
        //       .AddField("Opis", $"{(string.IsNullOrWhiteSpace(description) ? "(brak)" : description)}")
        //       .AddField("Kanał", $"{Context.Guild.GetTextChannel(channel).Mention}")
        //       .Build();

        //    await response.DeleteAsync();

        //    if (_git.Config.Servers[Context.Guild.Id].LogChannel != default)
        //        await Context.Guild.GetTextChannel(_git.Config.Servers[Context.Guild.Id].LogChannel).SendMessageAsync(embed: logEmbed);

        //    await Task.Delay(5000);
        //    await embedMsg.DeleteAsync();
        //    await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        //    await Context.Channel.GetMessagesAsync().ForEachAsync(x => x.ToList().ForEach(async y => await y.DeleteAsync()));
        //}

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
                if (_discord.GetGuild(683084560451633212).GetUser(Context.User.Id) == null)
                {
                    await Context.Channel.SendMessageAsync("Musisz być na serwerze, żeby skorzystać z tej komendy");
                    return;
                }
                else if (_discord.GetGuild(683084560451633212).GetRole(ulong.Parse(_git.Config.Servers[683084560451633212].AdditionalConfig["oc.postRole"])).Members.Count() > 0)
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
                    await ReplyAsync($"Anulowano. Otrzymano zdjęć: {response.Attachments.Count} - oczekiwano: 1");
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
                   .WithThumbnailUrl(Context.Message.Author.GetAvatarUrl())
                   .WithImageUrl(discordUrl)
                   .AddField("Użytkownik", $"{Context.Message.Author.Mention} ({Context.Message.Author.Id})")
                   .AddField("Opis", $"{(string.IsNullOrWhiteSpace(description) ? "(brak)" : description)}")
                   .AddField("Kanał", $"{Context.Guild.GetTextChannel(channelId).Mention}")
                   .Build();

                await response.DeleteAsync();

                if (_git.Config.Servers[Context.Guild.Id].LogChannel != default)
                    await Context.Guild.GetTextChannel(768175447728062475).SendMessageAsync(embed: logEmbed);

                await Task.Delay(5000);
                await embedMsg.DeleteAsync();
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                await DeleteAllChannelMessagesAsync(Context.Channel as SocketTextChannel);
            }
        }

        private async Task DeleteAllChannelMessagesAsync(SocketTextChannel channel)
        {
            var bulkDelete = new List<SocketMessage>();
            var individualDelete = new List<SocketMessage>();
            var awaiters = new List<Task>();

            bool moreRemaining = true;
            ulong lastMsgId = 0;
            do
            {
                var tmpMsgs = lastMsgId != 0 ? channel.GetMessagesAsync(lastMsgId, Direction.Before) : channel.GetMessagesAsync();
                moreRemaining = await tmpMsgs.CountAsync() > 0;

                await foreach (var l in tmpMsgs)
                {
                    l.ToList().ForEach(x =>
                    {
                        if (x.CreatedAt.CompareTo(DateTimeOffset.UtcNow) > 0)
                            bulkDelete.Add(x as SocketMessage);
                        else
                            individualDelete.Add(x as SocketMessage);
                    });
                }
            } while (moreRemaining);

            awaiters.Add(channel.DeleteMessagesAsync(bulkDelete));

            individualDelete.ForEach(x =>
            {
                try
                {
                    awaiters.Add(x.DeleteAsync());
                }
                catch { }
            });

            Task.WaitAll(awaiters.ToArray());
        }
    }
}
