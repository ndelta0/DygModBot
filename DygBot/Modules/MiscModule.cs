using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

using DygBot.Models;
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

        public MiscModule(GitHubService gitHub, HttpClient httpClient)
        {
            _git = gitHub;
            _http = httpClient;
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
        [Summary("Anonimowo wysyła OC na kanał")]
        public async Task OcAsync()
        {
            await Context.Message.DeleteAsync();
            var user = Context.User as SocketGuildUser;
            if (_git.Config.Servers[Context.Guild.Id].OcChannels.Count == 0)
            {
                await ReplyAndDeleteAsync("Anonimowe wysyłanie wiadomości nie jest włączone na tym serwerze", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }
            var strList = new List<string>();
            foreach (var kvp in _git.Config.Servers[Context.Guild.Id].OcChannels)
            {
                strList.Add($"• {(int)kvp.Key} - <#{kvp.Value}>");
            }
            var embed = new EmbedBuilder()
                                 .WithTitle("Wybierz docelowy kanał na swoje zdjęcie")
                                 .WithDescription($"Dostępne opcje:\n{new StringBuilder().AppendJoin('\n', strList)}")
                                 .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
                                 .WithFooter(footer =>
                                 {
                                     footer.WithText("Krok 1/2 (wyślij 'cancel', aby anulować)");
                                 })
                                 .Build();
            var embedMsg = await ReplyAsync(embed: embed);

            var response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
            if (response == null)
            {
                await embedMsg.DeleteAsync();
                await ReplyAndDeleteAsync("Czas minął, spróbuj jeszcze raz", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }
            else if (response.Content == "cancel")
            {
                await response.DeleteAsync();
                await embedMsg.DeleteAsync();
                await ReplyAndDeleteAsync("Wysyłanie zdjęcia anulowane", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }

            var gender = (Gender)int.Parse(response.Content);
            if (!_git.Config.Servers[Context.Guild.Id].OcChannels.TryGetValue(gender, out ulong channel))
            {
                await embedMsg.DeleteAsync();
                await ReplyAndDeleteAsync("Zły kanał", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }

            await response.DeleteAsync();
            embed = new EmbedBuilder()
                .WithTitle("Wyślij zdjęcie i opcjonalny opis")
                .WithDescription("Wyślij tylko jedno zdjęcie w załączniku. Do zdjęcia możesz dodać wiadomość, która zostanie dołączona do zdjęcia na kanale. Jeśli nie chcesz opisu, nic nie pisz.")
                .WithColor(_git.Config.Servers[Context.Guild.Id].ServerColor)
                .WithFooter(footer =>
                {
                    footer.WithText("Krok 2/2 (wyślij 'cancel', aby anulować)");
                })
                .Build();
            await embedMsg.ModifyAsync((x) =>
            {
                x.Embed = embed;
            });
            response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
            if (response == null)
            {
                await embedMsg.DeleteAsync();
                await ReplyAndDeleteAsync("Czas minął, spróbuj jeszcze raz", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }
            else if (response.Content == "cancel")
            {
                await response.DeleteAsync();
                await embedMsg.DeleteAsync();
                await ReplyAndDeleteAsync("Wysyłanie zdjęcia anulowane", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }
            else if (response.Attachments.Count != 1)
            {
                await embedMsg.DeleteAsync();
                await ReplyAndDeleteAsync($"Anulowano. Otrzymano zdjęć: {response.Attachments.Count} - oczekiwano: 1", timeout: TimeSpan.FromSeconds(5));
                await Task.Delay(5000);
                await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
                return;
            }
            var description = response.Content;

            var url = response.Attachments.ElementAt(0).Url;

            var extension = response.Attachments.ElementAt(0).Filename.Split('.').Last();

            var imgStream = await (await _http.GetAsync(url)).Content.ReadAsStreamAsync();

            var discordUrl = (await Context.Guild.GetTextChannel(channel).SendFileAsync(imgStream, $"anonymous-oc.{extension}", description)).Attachments.ElementAt(0).Url;

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
               .AddField("Kanał", $"{Context.Guild.GetTextChannel(channel).Mention}")
               .Build();

            await response.DeleteAsync();

            if (_git.Config.Servers[Context.Guild.Id].LogChannel != default)
                await Context.Guild.GetTextChannel(_git.Config.Servers[Context.Guild.Id].LogChannel).SendMessageAsync(embed: logEmbed);

            await Task.Delay(5000);
            await embedMsg.DeleteAsync();
            await user.RemoveRoleAsync(Context.Guild.GetRole(ulong.Parse(_git.Config.Servers[Context.Guild.Id].AdditionalConfig["oc.postRole"])));
        }
    }
}
