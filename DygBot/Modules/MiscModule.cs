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
                   .WithThumbnailUrl(Context.Message.Author.GetAvatarUrl())
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
                    .WithDescription($"Jakiej jesteś płci?\n\nMożliwe odpowiedzi:\n1 - Kobieta\n2 - Mężczyzna\n4 - Inna")
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
            else if (response.Content != "1" && response.Content != "2" && response.Content != "4")
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync($"Anulowano. Otrzymano: '{response.Content}'\nOczekiwano: 1, 2 lub 4");
                await Task.Delay(5000);
                return;
            }
            var gender = (Gender)int.Parse(response.Content);

            int age = 0;
            embed = new EmbedBuilder()
                    .WithTitle("Pytanie 2/3")
                    .WithDescription($"Ile masz lat (napisz cyfrą)?")
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
            else if (!int.TryParse(response.Content, out age))
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync($"Anulowano. Otrzymano: '{response.Content}'\nOczekiwano: cyfra");
                return;
            }

            bool dmOpen = false;
            embed = new EmbedBuilder()
                    .WithTitle("Pytanie 3/3")
                    .WithDescription($"Czy chcesz otrzymywać wiadomości prywatne od innych członków Dygawki?\n\nJeśli chcesz, wyślij 'Tak', 'True', 'Y' lub '1'\nJeśli nie chcesz, wyślij 'Nie', 'False', 'N' lub '0'")
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
            else if (!ExtendedBoolTryParse(response.Content, out dmOpen))
            {
                await embedMsg.DeleteAsync();
                await ReplyAsync($"Anulowano. Otrzymano: '{response.Content}'\nOczekiwano: którekolwiek z ('Tak', 'Nie', 'True', 'False', 'Y', 'N', '1', '0')");
                return;
            }

            var channel = _discord.GetGuild(683084560451633212).GetTextChannel(779049131028643860);

            var actioner = new ActionerMessage
            {
                Embed = new EmbedBuilder()
                    .WithTitle("Wypełniona ankieta")
                    .WithDescription("================")
                    .WithColor(_git.Config.Servers[683084560451633212].ServerColor)
                    .WithCurrentTimestamp()
                    .WithThumbnailUrl(Context.User.GetAvatarUrl())
                    .AddField("Użytkownik", $"{Context.User.Mention} ({Context.User.Id})")
                    .AddField("Płeć", gender.ToLocalString())
                    .AddField("Wiek", age, true)
                    .AddField("Wiadomości", dmOpen.ToLocalString(), true)
                    .Build()
            };
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("🔵"), // blue circle
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.AddRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683282889538142218));
                        }

                        return false;
                    }),
                    Removed = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.RemoveRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683282889538142218));
                        }

                        return false;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("🔴"), // red circle
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.AddRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683283001026936838));
                        }

                        return false;
                    }),
                    Removed = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.RemoveRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683283001026936838));
                        }

                        return false;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("🟣"), // purple circle
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.AddRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683300251876196385));
                        }

                        return false;
                    }),
                    Removed = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.RemoveRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683300251876196385));
                        }
                        return false;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("⭕"), // o (circle) (18+)
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.AddRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683305736532394027));
                        }

                        return false;
                    }),
                    Removed = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.RemoveRoleAsync(_discord.GetGuild(683084560451633212).GetRole(683305736532394027));
                        }

                        return false;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("🚫"), // no entry sign (18-)
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        var inviteLink = await _discord.GetGuild(683084560451633212).DefaultChannel.CreateInviteAsync(null, null, false, false);
                        await Context.User.SendMessageAsync(embed: new EmbedBuilder()
                            .WithTitle("**Szanowny użytkowniku!**")
                            .WithDescription($"Dygawka jest serwerem z zawartością nieodpowiednią dla nieletnich. Deklarując się jako osoba poniżej 18ego roku życia, Twoje konto zostało usunięte z listy dygaczy. Nie martw się, __nie zostało zbanowane__. Jeśli wybór roli *Underage* był efektem pomyłki, możesz nadal dołączyć do grona naszych użytkowników potwierdzając swoją pełnoletniość na mocy punktu nr 15 w naszym regulaminie. Jeśli zaś jesteś osobą nieletnią, zapraszamy na nasz serwer w przyszłości!\n\nMożesz dołączyć na serwer ponownie **[klikając w ten link]({inviteLink.Url})**")
                            .WithColor(_git.Config.Servers[683084560451633212].ServerColor)
                            .Build());
                        await user.KickAsync("Niepełnoletni");

                        return true;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("⚪"), // white circle
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.AddRoleAsync(_discord.GetGuild(683084560451633212).GetRole(719661984551010325));
                        }

                        return false;
                    }),
                    Removed = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.RemoveRoleAsync(_discord.GetGuild(683084560451633212).GetRole(719661984551010325));
                        }

                        return false;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("⛔"), // no entry
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.AddRoleAsync(_discord.GetGuild(683084560451633212).GetRole(719662311505264681));
                        }

                        return false;
                    }),
                    Removed = new Func<Task<bool>>(async () =>
                    {
                        var user = await _discord.GetGuild(683084560451633212).GetUserSafeAsync(Context.User.Id);
                        if (user != null)
                        {
                            await user.RemoveRoleAsync(_discord.GetGuild(683084560451633212).GetRole(719662311505264681));
                        }

                        return false;
                    })
                }
            });
            actioner.EmoteActions.Add(new EmoteAction
            {
                Emote = new Emoji("❌"), // \x (cross)
                Actions = new ActionTuple
                {
                    Added = new Func<Task<bool>>(async () =>
                    {
                        await Context.User.SendMessageAsync("Twoja ankieta została odrzucona, wyślij ją ponownie lub skontaktuj się z moderatorem lub administratorem.");

                        return true;
                    })
                }
            });

            await ExtendedInteractive.SendActionerMessageAsync(channel, actioner);

            await ReplyAsync("Ankieta została wysłana, moderacja nada ci odpowiednie role w jak najkrótszym czasie.");
        }

        private async Task DeleteAllChannelMessagesAsync(SocketTextChannel channel)
        {
            bool moreRemaining = true;
            ulong lastMsgId = 0;
            do
            {
                var tmpMsgs = lastMsgId != 0 ? channel.GetMessagesAsync(lastMsgId, Direction.Before) : channel.GetMessagesAsync();

                var msgs = await tmpMsgs.FlattenAsync();
                moreRemaining = msgs.Count() > 0;
                if (!moreRemaining)
                    continue;
                msgs.ToList().ForEach(async x =>
                {
                    await x.DeleteAsync();
                });
                lastMsgId = msgs.Last().Id;
            } while (moreRemaining);
        }

        private bool ExtendedBoolTryParse(string value, out bool result)
        {
            result = false;

            if (value == null) return false;
            if (value.Length == 0) return false;

            value = value.Trim();

            if (value.Length == 1)
            {
                if (int.TryParse(value, out int intResult))
                {
                    if (intResult == 0)
                    {
                        result = false;
                        return true;
                    }
                    else if (intResult == 1)
                    {
                        result = true;
                        return true;
                    }
                }
                else
                {
                    if (value.ToLower()[0] == 'n')
                    {
                        result = false;
                        return true;
                    }
                    else if (value.ToLower()[0] == 'y')
                    {
                        result = true;
                        return true;
                    }
                }
            }
            else
            {
                if (value.ToLower() == "nie" || value.ToLower() == "false")
                {
                    result = false;
                    return true;
                }
                else if (value.ToLower() == "tak" || value.ToLower() == "true")
                {
                    result = true;
                    return true;
                }
            }

            if (bool.TryParse(value, out result))
            {
                return true;
            }

            return false;
        }
    }
}
