using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using DygBot.Preconditions;
using DygBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using static DygBot.Services.GitHubService.ConfigClass.ServerConfigClass.CountChannelClass;

namespace DygBot.Modules
{
    [Summary("Moderation commands")]
    [RequireUser(312223735505747968, Group = "Permission")]
    [RequireOwner(Group = "Permission")]
    [RequireUserPermission(GuildPermission.Administrator, Group = "Permission")]
    [RequireManagementRole(Group = "Permission")]
    [Group("mod")]
    public class ModerationModule : InteractiveBase<SocketCommandContext>
    {
        private readonly GitHubService _git;
        private readonly InteractiveService _interactive;

        public ModerationModule(GitHubService git, InteractiveService interactive)
        {
            _git = git;
            _interactive = interactive;
        }

        [Command("prefix")]
        [Summary("Changes bot prefix")]
        public async Task PrefixAsync([Summary("New prefix")] string prefix)
        {
            using (Context.Channel.EnterTypingState())  // Show the "typing" notification
            {
                if (prefix == _git.Config.Servers[Context.Guild.Id.ToString()].Prefix)  // Compare new prefix with old prefix
                    await ReplyAsync($"Prefix is already set to **{prefix}**");
                else
                {
                    _git.Config.Servers[Context.Guild.Id.ToString()].Prefix = prefix;   // Set the prefix
                    await _git.UploadConfig();  // Upload config
                    await ReplyAsync($"Prefix set to {prefix}");    // Reply
                }
            }
        }

        [Group("managementRole")]
        public class ManagementRoleModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public ManagementRoleModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }


            [Command("add")]
            [Summary("Adds role to list of roles that can use this bot")]
            public async Task AddManagementRoleAsync([Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())  // Show the "typing" notification
                {
                    var roleIdString = role.Id.ToString();  // Get role ID
                    if (_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Contains(roleIdString))    // Check for information about role in config
                    {
                        await ReplyAsync("This role is already in the list");
                    }
                    else
                    {
                        _git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Add(roleIdString); // Add role to config
                        await _git.UploadConfig();
                        await ReplyAsync("Role added to list");
                    }
                }
            }

            [Command("remove")]
            [Summary("Removes role from list of roles that can use this bot")]
            public async Task RemoveManagementRoleAsync([Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var roleIdString = role.Id.ToString();
                    if (_git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Contains(roleIdString))
                    {
                        _git.Config.Servers[Context.Guild.Id.ToString()].ManagementRoles.Remove(roleIdString);
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

        [Group("countChannel")]
        public class CountChannelModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public CountChannelModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
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
                        var guildId = Context.Guild.Id.ToString();  // Get server ID
                        var channelId = channel.Id.ToString();  // Get channel ID
                        _git.Config.Servers[guildId].CountChannels[channelId] = new GitHubService.ConfigClass.ServerConfigClass.CountChannelClass   // Create new/Update config
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
                    var guildId = Context.Guild.Id.ToString();
                    var channelId = channel.Id.ToString();
                    _git.Config.Servers[guildId].CountChannels.Remove(channelId);
                    await _git.UploadConfig();
                    await ReplyAsync($"Channel **{channel}** cleared successfully");
                }
            }
        }

        [Group("vcTextRole")]
        public class VcTextRoleModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public VcTextRoleModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }

            [Command("set")]
            [Summary("Sets a role to give on entering a VC")]
            public async Task SetVcTextRole([Summary("Channel")] IVoiceChannel channel, [Summary("Role")] IRole role)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    _git.Config.Servers[guildId].VcTextRole[channel.Id.ToString()] = role.Id.ToString();    // Set role ID for channel
                    await _git.UploadConfig();
                    await ReplyAsync("Role successfully bound to voice chat");
                }
            }

            [Command("clear")]
            [Summary("Clears a role given on entering a VC")]
            public async Task ClearVcTextRole([Summary("Channel")] IVoiceChannel channel)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    _git.Config.Servers[guildId].VcTextRole.Remove(channel.Id.ToString());  // Clear channel
                    await _git.UploadConfig();
                    await ReplyAsync("Role successfully unbound from voice chat");
                }
            }
        }

        [Group("autoreact")]
        public class AutoreactModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;

            public AutoreactModule(GitHubService git, InteractiveService interactive)
            {
                _git = git;
                _interactive = interactive;
            }

            [Command("set")]
            [Summary("Sets roles to react with and channel to react on")]
            public async Task SetAutoreact([Summary("Channel")] ITextChannel channel, [Summary("Space separated Emotes/Emojis")] params string[] emotes)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    foreach (var text in emotes)
                    {
                        IEmote emote;
                        try
                        {
                            emote = Emote.Parse(text);  // Try parse emote (<:dyg:708782038521741352>)
                        }
                        catch (Exception)   // If emote is not a custom server emote
                        {
                            if (new Regex("[A-z0-9]").IsMatch(text))    // Check if text is a unicode emoji
                            {
                                await ReplyAsync($"**{text}** is not a valid emote");
                                return;
                            }
                            _ = new Emoji(text);    // Create emoji (👍)
                        }
                    }
                    _git.Config.Servers[guildId].AutoReact[channel.Id.ToString()] = emotes.ToList();    // Add emotes to config
                    await _git.UploadConfig();
                    await ReplyAsync($"AutoReply on channel **{channel}** set up successfully");
                }
            }

            [Command("clear")]
            [Summary("Clears autoreact from channel")]
            public async Task ClearAutoreact([Summary("Channel")] ITextChannel channel)
            {
                using (Context.Channel.EnterTypingState())
                {
                    var guildId = Context.Guild.Id.ToString();
                    _git.Config.Servers[guildId].AutoReact.Remove(channel.Id.ToString());   // Clear emotes
                    await _git.UploadConfig();
                    await ReplyAsync($"AutoReply on channel **{channel}** cleared successfully");
                }
            }
        }

        [Group("commandLimit")]
        public class CommandLimitModule : InteractiveBase<SocketCommandContext>
        {
            private readonly GitHubService _git;
            private readonly InteractiveService _interactive;
            private readonly CommandService _service;

            public CommandLimitModule(GitHubService git, CommandService service, InteractiveService interactive)
            {
                _git = git;
                _service = service;
                _interactive = interactive;
            }

            [Command("add")]
            [Summary("Adds a channel where a command can be used in")]
            public async Task AddCommandLimitAsync([Summary("Channel")]ITextChannel channel, [Summary("Command")][Remainder]string command)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                    {
                        await ReplyAsync($"No command has alias of {command}");
                    }
                    else
                    {
                        command = _service.Commands.First(x => x.Aliases.Contains(command)).Name;
                        if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.ContainsKey(command))
                        {
                            if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command].Contains(channel.Id.ToString()))
                            {
                                await ReplyAsync("Command already limited to that channel");
                            }
                            else
                            {
                                _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command].Add(channel.Id.ToString());
                                await _git.UploadConfig();
                                await ReplyAsync($"Command **{command}** limited to channel {channel.Mention}");
                            }
                        }
                        else
                        {
                            _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command] = new List<string> { channel.Id.ToString() };
                            await _git.UploadConfig();
                            await ReplyAsync($"Command **{command}** limited to channel {channel.Mention}");
                        }
                    }
                }
            }

            [Command("remove")]
            [Summary("Removes a channel where a command can be used in")]
            public async Task RemoveCommandLimitAsync([Summary("Channel")] ITextChannel channel, [Summary("Command")][Remainder] string command)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                    {
                        await ReplyAsync($"No command has alias of {command}");
                    }
                    else
                    {
                        command = _service.Commands.First(x => x.Aliases.Contains(command)).Name;
                        if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.ContainsKey(command))
                        {
                            _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.Remove(command);
                            await _git.UploadConfig();
                            await ReplyAsync($"Command **{command}** removed from channel {channel.Mention}");
                        }
                        else
                        {
                            await ReplyAsync("Command was not limited to that channel");
                        }
                    }
                }
            }

            [Command("show")]
            [Summary("Shows what channels command can be used in")]
            public async Task ShowCommandLimitAsync([Summary("Command")][Remainder] string command)
            {
                using (Context.Channel.EnterTypingState())
                {
                    if (!_service.Commands.Any(x => x.Aliases.Contains(command)))
                    {
                        await ReplyAsync($"No command has alias of {command}");
                    }
                    else
                    {
                        string message = "Channels where command can be used:\n";
                        if (_git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit.ContainsKey(command))
                        {
                            foreach (var channelId in _git.Config.Servers[Context.Guild.Id.ToString()].CommandLimit[command])
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
        }
    }
}
