using Discord;
using Discord.Commands;
using DygBot.Preconditions;
using DygBot.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    public class ModerationModule : ModuleBase<SocketCommandContext>
    {
        private readonly GitHubService _git;

        public ModerationModule(GitHubService git)
        {
            _git = git;
        }

        [Command("prefix")]
        [Summary("Changes bot prefix")]
        public async Task PrefixAsync([Summary("New prefix")]string prefix)
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

        [Command("addManagementRole")]
        [Summary("Adds role to list of roles that can use this bot")]
        public async Task AddManagementRoleAsync([Summary("Role")]IRole role)
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

        [Command("removeManagementRole")]
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

        [Command("setCountChannel")]
        [Summary("Sets specified channel as a count channel")]
        public async Task SetCountChannel([Summary("Channel")]IChannel channel, [Summary("Property to display")]CountPropertyEnum property, [Summary("Channel naming format (use %num% as a placeholder for the number)")][Remainder]string template)
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

        [Command("clearCountChannel")]
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

        [Command("setVcTextRole")]
        [Summary("Sets a role to give on entering a VC")]
        public async Task SetVcTextRole([Summary("Channel")]IVoiceChannel channel, [Summary("Role")]IRole role)
        {
            using (Context.Channel.EnterTypingState())
            {
                var guildId = Context.Guild.Id.ToString();
                _git.Config.Servers[guildId].VcTextRole[channel.Id.ToString()] = role.Id.ToString();    // Set role ID for channel
                await _git.UploadConfig();
                await ReplyAsync("Role successfully bound to voice chat");
            }
        }

        [Command("clearVcTextRole")]
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

        [Command("setAutoreact")]
        [Summary("Sets roles to react with and channel to react on")]
        public async Task SetAutoreact([Summary("Channel")]ITextChannel channel, [Summary("Space separated Emotes/Emojis")]params string[] emotes)
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
                        emote = new Emoji(text);    // Create emoji (👍)
                    }
                }
                _git.Config.Servers[guildId].AutoReact[channel.Id.ToString()] = emotes.ToList();    // Add emotes to config
                await _git.UploadConfig();
                await ReplyAsync($"AutoReply on channel **{channel}** set up successfully");
            }
        }

        [Command("clearAutoreact")]
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
}
