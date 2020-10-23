using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.Models;
using DygBot.Preconditions;
using DygBot.Services;
using DygBot.TypeReaders;

using Reddit;
using Reddit.Controllers;

using SixLabors.ImageSharp.ColorSpaces;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DygBot.Services.GitHubService.ConfigClass.ServerConfigClass;
using static DygBot.Services.GitHubService.ConfigClass.ServerConfigClass.CountChannelClass;

namespace DygBot.Modules
{
    [Summary("Moderation commands")]
    [RequireUser(312223735505747968, Group = "Permission")]
    [RequireOwner(Group = "Permission")]
    [RequireManagementRole(Group = "Permission")]
    public class ModerationModule : InteractiveBase<SocketCommandContext>
    {

        private readonly GitHubService _git;

        public ModerationModule(GitHubService git)
        {
            _git = git;
        }

        [Command("prefix")]
        [Summary("Changes bot prefix")]
        public async Task PrefixAsync([Summary("New prefix")] string prefix)
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                if (prefix == _git.Config.Servers[Context.Guild.Id].Prefix)  // Compare new prefix with old prefix
                    await ReplyAsync($"Prefix is already set to **{prefix}**");
                else
                {
                    _git.Config.Servers[Context.Guild.Id].Prefix = prefix;   // Set the prefix
                    await _git.UploadConfig();  // Upload config
                    await ReplyAsync($"Prefix set to {prefix}");    // Reply
                }
            }
        }

        [Command("config-reload", true)]
        [Summary("Zmusza bota do przeładowania konfiguracji")]
        public async Task ConfigReloadAsync()
        {
            await _git.DownloadConfig();
            await _git.UploadConfig();
            await _git.DownloadConfig();
            await ReplyAsync("Konfiguracja przeładowana");
        }

        [Command("test")]
        [Summary("test")]
        [RequireUser(312223735505747968)]
        public async Task TestAsync()
        {
            await Task.Delay(0);
            var config = _git.Config.Servers[Context.Guild.Id];
            Console.WriteLine(config.LogChannel);
            foreach (var kvp in config.OcChannels)
            {
                Console.WriteLine($"{kvp.Key} - {kvp.Value}");
            }
            foreach (var kvp in config.AdditionalConfig)
            {
                Console.WriteLine($"{kvp.Key} - {kvp.Value}");
            }
        }

        [Group("mr")]
        public class ManagementRoleModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public ManagementRoleModule(GitHubService git)
            {
                _git = git;
            }


            [Command("add")]
            [Summary("Adds role to list of roles that can use this bot")]
            public async Task AddManagementRoleAsync([Summary("Role")] IRole role)
            {
                var roleId = role.Id;  // Get role ID
                if (_git.Config.Servers[Context.Guild.Id].ManagementRoles.Contains(roleId))    // Check for information about role in config
                {
                    await ReplyAsync("This role is already in the list");
                }
                else
                {
                    _git.Config.Servers[Context.Guild.Id].ManagementRoles.Add(roleId); // Add role to config
                    await _git.UploadConfig();
                    await ReplyAsync("Role added to list");
                }
            }

            [Command("remove")]
            [Summary("Removes role from list of roles that can use this bot")]
            public async Task RemoveManagementRoleAsync([Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var roleId = role.Id;
                    if (_git.Config.Servers[Context.Guild.Id].ManagementRoles.Contains(roleId))
                    {
                        _git.Config.Servers[Context.Guild.Id].ManagementRoles.Remove(roleId);
                        await _git.UploadConfig();
                        await ReplyAsync("Role removed from list");
                    }
                    else
                    {
                        await ReplyAsync("Role not in list");
                    }
                }
            }

        }

        [Group("cc")]
        public class CountChannelModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public CountChannelModule(GitHubService git)
            {
                _git = git;
            }

            [Command("set")]
            [Summary("Sets specified channel as a count channel")]
            public async Task SetCountChannel([Summary("Channel")] IChannel channel, [Summary("Property to display")] CountPropertyEnum property, [Summary("Channel naming format (use %num% as a placeholder for the number)")][Remainder] string template)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!template.Contains("%num%"))    // Check if template contains placeholder
                    {
                        await ReplyAsync("Template doesn't contain **%num%** placeholder");
                    }
                    else
                    {
                        var guildId = Context.Guild.Id;  // Get server ID
                        var channelId = channel.Id;  // Get channel ID
                        _git.Config.Servers[guildId].CountChannels[channelId] = new CountChannelClass   // Create new/Update config
                        {
                            Property = property,
                            Template = template
                        };
                        await _git.UploadConfig();
                        await ReplyAsync($"Channel **{channel}** set successfully");
                    }
                }
            }

            [Command("clear")]
            [Summary("Removes counting functionality from channel")]
            public async Task ClearCountChannel([Summary("Channel")] IChannel channel)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id;
                    var channelId = channel.Id;
                    _git.Config.Servers[guildId].CountChannels.Remove(channelId);
                    await _git.UploadConfig();
                    await ReplyAsync($"Channel **{channel}** cleared successfully");
                }
            }
        }

        [Group("vtr")]
        public class VcTextRoleModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public VcTextRoleModule(GitHubService git)
            {
                _git = git;
            }

            [Command("set")]
            [Summary("Sets a role to give on entering a VC")]
            public async Task SetVcTextRole([Summary("Channel")] IVoiceChannel channel, [Summary("Role")] IRole role)
            {
                var guildId = Context.Guild.Id;
                _git.Config.Servers[guildId].VcTextRole[channel.Id] = role.Id;    // Set role ID for channel
                await _git.UploadConfig();
                await ReplyAsync("Role successfully bound to voice chat");
            }

            [Command("clear")]
            [Summary("Clears a role given on entering a VC")]
            public async Task ClearVcTextRole([Summary("Channel")] IVoiceChannel channel)
            {
                var guildId = Context.Guild.Id;
                _git.Config.Servers[guildId].VcTextRole.Remove(channel.Id);  // Clear channel
                await _git.UploadConfig();
                await ReplyAsync("Role successfully unbound from voice chat");
            }
        }

        [Group("ar")]
        public class AutoreactModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public AutoreactModule(GitHubService git)
            {
                _git = git;
            }

            [Command("set")]
            [Summary("Sets roles to react with and channel to react on")]
            public async Task SetAutoreact([Summary("Channel")] ITextChannel channel, [Summary("Czy opis jest wymagany")] bool contentRequired, [Summary("Space separated Emotes/Emojis")] params string[] emotes)
            {
                var guildId = Context.Guild.Id;
                var emotesList = new List<IEmote>(emotes.Length);
                foreach (var text in emotes)
                {
                    IEmote emote;
                    if (Emote.TryParse(text, out Emote emoteTmp))
                        emote = emoteTmp;
                    else
                        emote = new Emoji(text);
                    if (emote != null)
                        emotesList.Add(emote);
                }
                _git.Config.Servers[guildId].AutoReact[channel.Id] = new AutoReactClass
                {
                    Emotes = emotesList.Select(x => x.ToString()).ToList(),
                    RequireContent = contentRequired
                };
                await _git.UploadConfig();
                await ReplyAsync($"AutoReply on channel **{channel}** set up successfully");
            }

            [Command("clear")]
            [Summary("Clears autoreact from channel")]
            public async Task ClearAutoreact([Summary("Channel")] ITextChannel channel)
            {
                var guildId = Context.Guild.Id;
                _git.Config.Servers[guildId].AutoReact.Remove(channel.Id);   // Clear emotes
                await _git.UploadConfig();
                await ReplyAsync($"AutoReply on channel **{channel}** cleared successfully");
            }
        }

        [Group("cl")]
        public class CommandLimitModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly CommandService _service;

            public CommandLimitModule(GitHubService git, CommandService service)
            {
                _git = git;
                _service = service;
            }

            [Command("add")]
            [Summary("Adds a channel where a command can be used in")]
            public async Task AddCommandLimitAsync([Summary("Channel")] ITextChannel channel, [Summary("Command")][Remainder] string command)
            {
                if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                {
                    await ReplyAsync($"No command has alias of {command}");
                }
                else
                {
                    command = _service.Commands.First(x => x.Aliases.Contains(command)).Name;
                    if (_git.Config.Servers[Context.Guild.Id].CommandLimit.ContainsKey(command))
                    {
                        if (_git.Config.Servers[Context.Guild.Id].CommandLimit[command].Contains(channel.Id))
                        {
                            await ReplyAsync("Command already limited to that channel");
                        }
                        else
                        {
                            _git.Config.Servers[Context.Guild.Id].CommandLimit[command].Add(channel.Id);
                            await _git.UploadConfig();
                            await ReplyAsync($"Command **{command}** limited to channel {channel.Mention}");
                        }
                    }
                    else
                    {
                        _git.Config.Servers[Context.Guild.Id].CommandLimit[command] = new List<ulong> { channel.Id };
                        await _git.UploadConfig();
                        await ReplyAsync($"Command **{command}** limited to channel {channel.Mention}");
                    }
                }
            }

            [Command("remove")]
            [Summary("Removes a channel where a command can be used in")]
            public async Task RemoveCommandLimitAsync([Summary("Channel")] ITextChannel channel, [Summary("Command")][Remainder] string command)
            {
                if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                {
                    await ReplyAsync($"No command has alias of {command}");
                }
                else
                {
                    command = _service.Commands.First(x => x.Aliases.Contains(command)).Name;
                    if (_git.Config.Servers[Context.Guild.Id].CommandLimit.ContainsKey(command))
                    {
                        _git.Config.Servers[Context.Guild.Id].CommandLimit.Remove(command);
                        await _git.UploadConfig();
                        await ReplyAsync($"Command **{command}** removed from channel {channel.Mention}");
                    }
                    else
                    {
                        await ReplyAsync("Command was not limited to that channel");
                    }
                }
            }

            [Command("show")]
            [Summary("Shows what channels command can be used in")]
            public async Task ShowCommandLimitAsync([Summary("Command")][Remainder] string command)
            {
                if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                {
                    await ReplyAsync($"No command has alias of {command}");
                }
                else
                {
                    string message = "Channels where command can be used:\n";
                    if (_git.Config.Servers[Context.Guild.Id].CommandLimit.ContainsKey(command))
                    {
                        foreach (var channelId in _git.Config.Servers[Context.Guild.Id].CommandLimit[command])
                        {
                            message += $"<#{channelId}>\n";
                        }
                    }
                    else
                    {
                        message += "All channels (no limits)";
                    }
                    await ReplyAsync(message);
                }
            }
        }

        [Group("rr")]
        public class ReactionRoleClass : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public ReactionRoleClass(GitHubService git)
            {
                _git = git;
            }

            public enum ReactionAction
            {
                GiveRemove,
                Give,
                Remove,
                OneOfMany
            }

            private void AddRoleResult<T>(Dictionary<ulong, TypeReaderValue> results, T role, float score) where T : IRole
            {
                if (role != null && !results.ContainsKey(role.Id))
                    results.Add(role.Id, new TypeReaderValue(role, score));
            }

            [Command()]
            public async Task SetUpReactionRoleAsync()
            {
                var embed = new EmbedBuilder
                {
                    Description = $"Podaj link do wiadomości, która ma mieć reakcję",
                    Footer = new EmbedFooterBuilder
                    {
                        Text = "Krok 1/3 | napisz 'cancel' aby anulować"
                    }
                }.Build();
                var msg = await ReplyAsync(embed: embed);

                var response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                if (response == null)
                {
                    await msg.DeleteAsync();
                    await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                    return;
                }
                else if (response.Content.ToLower() == "cancel")
                {
                    await msg.DeleteAsync();
                    await ReplyAsync("Tworzenie roli anulowane");
                    return;
                }

                var messageUrl = response.Content;
                var messageSplit = messageUrl.Split('/');

                if (ulong.TryParse(messageSplit[4], out ulong guildId)
                    && ulong.TryParse(messageSplit[5], out ulong channelId)
                    && ulong.TryParse(messageSplit[6], out ulong messageId))

                {
                    if (guildId != Context.Guild.Id)
                    {
                        await ReplyAsync("Wiadomość nie jest z tego serwera");
                        return;
                    }
                    var channel = Context.Guild.GetTextChannel(channelId);
                    if (channel == null)
                    {
                        await ReplyAsync("Nieprawidłowy kanał");
                        return;
                    }
                    var message = await channel.GetMessageAsync(messageId);
                    if (message == null)
                    {
                        await ReplyAsync("Niaprawidłowa wiadomość");
                        return;
                    }

                    embed = new EmbedBuilder
                    {
                        Description = "Wybierz w jaki sposób reakcja ma działać:\n0 - daje i zabiera\n1 - tylko daje po zareagowaniu\n2 - tylko zabiera po zareagowaniu\n3 - daje jedną i zabiera inną",
                        Footer = new EmbedFooterBuilder
                        {
                            Text = "Krok 2/3 | napisz 'cancel' aby anulować"
                        }
                    }.Build();

                    await response.DeleteAsync();

                    await msg.ModifyAsync((x) =>
                    {
                        x.Embed = embed;
                    });

                    response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                    if (response == null)
                    {
                        await msg.DeleteAsync();
                        await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                        return;
                    }
                    else if (response.Content.ToLower() == "cancel")
                    {
                        await msg.DeleteAsync();
                        await ReplyAsync("Tworzenie roli anulowane");
                        return;
                    }

                    if (!int.TryParse(response.Content, out int actionNum))
                    {
                        await msg.DeleteAsync();
                        await ReplyAsync("Czy na pewno wiadomość zawierała odpowiednią zawartość?");
                        return;
                    }

                    var action = (ReactionAction)actionNum;

                    if (action == ReactionAction.GiveRemove || action == ReactionAction.Give || action == ReactionAction.Remove)
                    {
                        embed = new EmbedBuilder
                        {
                            Description = "Podaj reakcję i rolę w formacie: emoji - id roli/wzmianka/nazwa\nPrzykład: 🎶 - 722411980635504647\nNie używaj customowych emoji z innych serwerów!",
                            Footer = new EmbedFooterBuilder
                            {
                                Text = "Krok 3/3 | napisz 'cancel' aby anulować"
                            }
                        }.Build();

                        await response.DeleteAsync();

                        await msg.ModifyAsync((x) =>
                        {
                            x.Embed = embed;
                        });

                        response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                        if (response == null)
                        {
                            await msg.DeleteAsync();
                            await ReplyAsync("Czas minął, spróbuj jeszcze raz");
                            return;
                        }
                        else if (response.Content.ToLower() == "cancel")
                        {
                            await msg.DeleteAsync();
                            await ReplyAsync("Tworzenie roli anulowane");
                            return;
                        }

                        IEmote emote;
                        if (Emote.TryParse(response.Content.Split('-')[0].Trim(), out Emote emoteTmp))
                            emote = emoteTmp;
                        else
                            emote = new Emoji(response.Content.Split('-')[0].Trim());
                        if (emote == null)
                        {
                            await ReplyAsync("Coś poszło nie tak, miałem problem z tą emotką");
                            return;
                        }

                        var roleString = response.Content.Split('-')[1].Trim();
                        var roleResults = new Dictionary<ulong, TypeReaderValue>();

                        //By Mention (1.0)
                        if (MentionUtils.TryParseRole(roleString, out var id))
                            AddRoleResult(roleResults, Context.Guild.GetRole(id) as IRole, 1.00f);

                        //By Id (0.9)
                        if (ulong.TryParse(roleString, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                            AddRoleResult(roleResults, Context.Guild.GetRole(id) as IRole, 0.90f);

                        //By Name (0.7-0.8)
                        foreach (var roleTmp in Context.Guild.Roles.Where(x => string.Equals(roleString, x.Name, StringComparison.OrdinalIgnoreCase)))
                            AddRoleResult(roleResults, roleTmp as IRole, roleTmp.Name == roleString ? 0.80f : 0.70f);

                        await response.DeleteAsync();

                        if (roleResults.Count == 0)
                        {
                            await msg.DeleteAsync();
                            await ReplyAsync("Coś poszło nie tak, czy rola została dobrze podana?");
                            return;
                        }

                        var role = (SocketRole)new List<TypeReaderValue>(roleResults.Values).OrderBy(x => x.Score).First().Value;

                        if (role == null)
                        {
                            await msg.DeleteAsync();
                            await ReplyAsync("Coś poszło nie tak, czy rola została dobrze podana?");
                            return;
                        }

                        if (_git.Config.Servers[guildId].ReactionRoles.ContainsKey(channelId)
                            && _git.Config.Servers[guildId].ReactionRoles[channelId].ContainsKey(messageId)
                            && _git.Config.Servers[guildId].ReactionRoles[channelId][messageId].Any(x => x.Roles.ContainsKey(emote.ToString())))
                        {
                            await msg.DeleteAsync();
                            await ReplyAsync("Ta emotka jest już wykorzystana w tej wiadomości na tym kanale");
                            return;
                        }

                        if (!_git.Config.Servers[guildId].ReactionRoles.ContainsKey(channelId))
                        {
                            _git.Config.Servers[guildId].ReactionRoles.Add(channelId, new Dictionary<ulong, List<ReactionRole>>());
                        }
                        if (!_git.Config.Servers[guildId].ReactionRoles[channelId].ContainsKey(messageId))
                        {
                            _git.Config.Servers[guildId].ReactionRoles[channelId].Add(messageId, new List<ReactionRole>());
                        }

                        _git.Config.Servers[guildId].ReactionRoles[channelId][messageId].Add(new ReactionRole
                        {
                            Action = action,
                            Roles = new Dictionary<string, ulong>()
                            {
                                { emote.ToString(), role.Id }
                            }
                        });

                        await (await Context.Guild.GetTextChannel(channelId).GetMessageAsync(messageId)).AddReactionAsync(emote);
                        await msg.ModifyAsync((x) =>
                        {
                            x.Content = "Utworzono pomyślnie";
                            x.Embed = null;
                        });
                        await _git.UploadConfig();
                    }
                    else if (action == ReactionAction.OneOfMany)
                    {
                        await response.DeleteAsync();

                        Dictionary<string, ulong> roleKvp = new Dictionary<string, ulong>();
                        List<IEmote> emotes = new List<IEmote>();

                        embed = new EmbedBuilder
                        {
                            Description = "Podaj reakcję i rolę w formacie: emoji - id roli/wzmianka/nazwa\nPrzykład: 🎶 - 722411980635504647\nPodaj co najmniej 2 reakcje\nGdy podasz wszystkie reakcje które chcesz, wyślij wiadomość 'continue'\nNie używaj customowych emoji z innych serwerów!",
                            Footer = new EmbedFooterBuilder
                            {
                                Text = "Krok 3/3 | napisz 'cancel' aby anulować"
                            }
                        }.Build();
                        await msg.ModifyAsync((x) =>
                        {
                            x.Embed = embed;
                        });

                        bool finished = false;

                        do
                        {
                            response = await NextMessageAsync(timeout: TimeSpan.FromMinutes(1));
                            if (response == null)
                            {
                                await msg.DeleteAsync();
                                await ReplyAndDeleteAsync("Czas minął, spróbuj jeszcze raz", timeout: TimeSpan.FromSeconds(2));
                                continue;
                            }
                            else if (response.Content.ToLower() == "cancel")
                            {
                                await msg.DeleteAsync();
                                await ReplyAsync("Tworzenie roli anulowane");
                                return;
                            }
                            else if (response.Content.ToLower() == "continue")
                            {
                                finished = true;
                                await response.DeleteAsync();
                                continue;
                            }

                            IEmote emote;
                            if (Emote.TryParse(response.Content.Split('-')[0].Trim(), out Emote emoteTmp))
                                emote = emoteTmp;
                            else
                                emote = new Emoji(response.Content.Split('-')[0].Trim());
                            if (emote == null)
                            {
                                await ReplyAndDeleteAsync("Coś poszło nie tak, spróbuj ponownie lub anuluj", timeout: TimeSpan.FromSeconds(2));
                                continue;
                            }

                            var roleString = response.Content.Split('-')[1].Trim();
                            var roleResults = new Dictionary<ulong, TypeReaderValue>();

                            //By Mention (1.0)
                            if (MentionUtils.TryParseRole(roleString, out var id))
                                AddRoleResult(roleResults, Context.Guild.GetRole(id) as IRole, 1.00f);

                            //By Id (0.9)
                            if (ulong.TryParse(roleString, NumberStyles.None, CultureInfo.InvariantCulture, out id))
                                AddRoleResult(roleResults, Context.Guild.GetRole(id) as IRole, 0.90f);

                            //By Name (0.7-0.8)
                            foreach (var roleTmp in Context.Guild.Roles.Where(x => string.Equals(roleString, x.Name, StringComparison.OrdinalIgnoreCase)))
                                AddRoleResult(roleResults, roleTmp as IRole, roleTmp.Name == roleString ? 0.80f : 0.70f);

                            await response.DeleteAsync();

                            if (roleResults.Count == 0)
                            {
                                await ReplyAndDeleteAsync("Coś poszło nie tak, czy rola została dobrze podana?", timeout: TimeSpan.FromSeconds(2));
                                continue;
                            }

                            var role = (SocketRole)new List<TypeReaderValue>(roleResults.Values).OrderBy(x => x.Score).First().Value;

                            if (role == null)
                            {
                                await ReplyAndDeleteAsync("Coś poszło nie tak, czy rola została dobrze podana?", timeout: TimeSpan.FromSeconds(2));
                                continue;
                            }

                            if (_git.Config.Servers[guildId].ReactionRoles.ContainsKey(channelId)
                                && _git.Config.Servers[guildId].ReactionRoles[channelId].ContainsKey(messageId)
                                && _git.Config.Servers[guildId].ReactionRoles[channelId][messageId].Any(x => x.Roles.ContainsKey(emote.ToString())))
                            {
                                await ReplyAndDeleteAsync("Ta emotka jest już wykorzystana w tej wiadomości na tym kanale", timeout: TimeSpan.FromSeconds(2));
                                continue;
                            }

                            await ReplyAndDeleteAsync("Dodano rolę", timeout: TimeSpan.FromSeconds(1));

                            emotes.Add(emote);

                            roleKvp.Add(emote.ToString(), role.Id);

                        } while (!finished);

                        if (!_git.Config.Servers[guildId].ReactionRoles.ContainsKey(channelId))
                            _git.Config.Servers[guildId].ReactionRoles.Add(channelId, new Dictionary<ulong, List<ReactionRole>>());
                        if (!_git.Config.Servers[guildId].ReactionRoles[channelId].ContainsKey(messageId))
                            _git.Config.Servers[guildId].ReactionRoles[channelId].Add(messageId, new List<ReactionRole>());

                        _git.Config.Servers[guildId].ReactionRoles[channelId][messageId].Add(new ReactionRole
                        {
                            Action = action,
                            Roles = roleKvp
                        });

                        foreach (var emote in emotes)
                            await (await Context.Guild.GetTextChannel(channelId).GetMessageAsync(messageId)).AddReactionAsync(emote);

                        await msg.ModifyAsync((x) =>
                        {
                            x.Content = "Utworzono pomyślnie";
                            x.Embed = null;
                        });
                        await _git.UploadConfig();
                    }
                    else
                    {
                        await msg.DeleteAsync();
                        await ReplyAndDeleteAsync($"Zły numer - powinien być: 0, 1, 2, 3; otrzymano: {actionNum}", timeout: TimeSpan.FromSeconds(5));
                    }
                }
            }

            [Command("clear")]
            public async Task ClearReactionRoleAsync(IMessage message)
            {
                if (_git.Config.Servers[Context.Guild.Id].ReactionRoles[message.Channel.Id].Keys.Contains(message.Id))
                {
                    _git.Config.Servers[Context.Guild.Id].ReactionRoles[message.Channel.Id].Remove(message.Id);

                    await message.RemoveAllReactionsAsync();
                }

                await ReplyAsync("Usunięto reakcje i role z wiadomości");
            }
        }

        [Group("rl")]
        public class ReactionLimitClass : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public ReactionLimitClass(GitHubService git)
            {
                _git = git;
            }

            [Command()]
            public async Task SetReactionLimit(IEmote emote, ITextChannel channel)
            {
                if (_git.Config.Servers[Context.Guild.Id].AllowedReactions.ContainsKey(emote.ToString()))
                {
                    _git.Config.Servers[Context.Guild.Id].AllowedReactions[emote.ToString()].Add(channel.Id);
                }
                else
                {
                    _git.Config.Servers[Context.Guild.Id].AllowedReactions[emote.ToString()] = new HashSet<ulong> { channel.Id };
                }
                await _git.UploadConfig();
                await ReplyAsync("Kanał dodany");
            }

            [Command("clear")]
            public async Task ClearReactionLimit(IEmote emote, ITextChannel channel = null)
            {
                if (_git.Config.Servers[Context.Guild.Id].AllowedReactions.TryGetValue(emote.ToString(), out HashSet<ulong> channelIds))
                {
                    if (channel == null)
                    {
                        _git.Config.Servers[Context.Guild.Id].AllowedReactions.Remove(emote.ToString());
                    }
                    else
                    {
                        if (channelIds.Contains(channel.Id))
                        {
                            channelIds.Remove(channel.Id);
                        }
                        else
                        {
                            await ReplyAsync("Kanał nie jest ustawiony dla tej emotki");
                            return;
                        }
                        _git.Config.Servers[Context.Guild.Id].AllowedReactions[emote.ToString()] = channelIds;
                    }
                    await _git.UploadConfig();
                    await ReplyAsync("Usunięto ograniczenie kanału");
                    return;
                }
                await ReplyAsync("Ograniczenia dla tej emotki nie są ustawione");
            }
        }

        [Group("halfanhour")]
        [Alias("hah")]
        public class HalfAnHourClass : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;

            public HalfAnHourClass(GitHubService git)
            {
                _git = git;
            }

            [Command()]
            public async Task SetHalfAnHourChannel(ITextChannel channel, params Gender[] genders)
            {
                Gender resultGender = Gender.None;

                foreach (var gender in genders)
                {
                    resultGender |= gender;
                }

                if (resultGender == Gender.None)
                {
                    await ReplyAsync("No genders selected");
                    return;
                }

                if (_git.Config.Servers.TryGetValue(Context.Guild.Id, out var serverConfig))
                {
                    if (serverConfig.HalfAnHourConfig.ContainsKey(channel.Id))
                    {
                        serverConfig.HalfAnHourConfig[channel.Id] = resultGender;
                    }
                    else
                        serverConfig.HalfAnHourConfig.Add(channel.Id, resultGender);
                    await _git.UploadConfig();
                    await ReplyAsync($"Added channel {channel.Mention} to half-an-hour ({resultGender})");
                }
            }

            [Command("clear")]
            public async Task ClearHalfAnHour(ITextChannel channel)
            {
                if (_git.Config.Servers.TryGetValue(Context.Guild.Id, out var serverConfig))
                {
                    if (serverConfig.HalfAnHourConfig.ContainsKey(channel.Id))
                    {
                        serverConfig.HalfAnHourConfig.Remove(channel.Id);
                        await _git.UploadConfig();
                        await ReplyAsync($"Removed channel {channel.Mention} from half-an-hour");
                    }
                    else
                    {
                        await ReplyAsync("Channel not in half-an-hour");
                    }
                }
            }
        }
    }
}
