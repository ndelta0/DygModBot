using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.TypeReaders;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static DygBot.Services.GitHubService;

namespace DygBot.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IServiceProvider _provider;
        private readonly GitHubService _git;

        // DiscordSocketClient, CommandService, IConfigurationRoot, and IServiceProvider are
        // injected automatically from the IServiceProvider
        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            IServiceProvider provider,
            GitHubService git)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _git = git;

            _discord.MessageReceived += _discord_MessageReceived;   // Bind MessageReceived event
            _discord.JoinedGuild += _discord_JoinedGuild;   // Bind JoinedGuild event
            _discord.UserVoiceStateUpdated += _discord_UserVoiceStateUpdated;

            _commands.AddTypeReader(typeof(object), new ObjectTypeReader());    // Add object type reader
        }

        private enum VcChangeState
        {
            None,
            Left,
            Joined,
            Moved
        }

        private async Task _discord_UserVoiceStateUpdated(SocketUser socketUser, SocketVoiceState beforeState, SocketVoiceState afterState)
        {
            if (socketUser is SocketGuildUser user)
            {
                var state = VcChangeState.None;
                if (beforeState.VoiceChannel == null && afterState.VoiceChannel != null)
                    state = VcChangeState.Joined;
                if (beforeState.VoiceChannel != null && afterState.VoiceChannel == null)
                    state = VcChangeState.Left;
                if (beforeState.VoiceChannel != null && afterState.VoiceChannel != null)
                    state = VcChangeState.Moved;


                try
                {
                    string roleId;

                    switch (state)
                    {
                        case VcChangeState.Joined:
                            _git.Config.Servers[afterState.VoiceChannel.Guild.Id.ToString()].VcTextRole.TryGetValue(afterState.VoiceChannel.Id.ToString(), out roleId);
                            if (!string.IsNullOrWhiteSpace(roleId))
                            {
                                var role = afterState.VoiceChannel.Guild.GetRole(ulong.Parse(roleId));
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Joined VC" });
                                }
                            }
                            break;

                        case VcChangeState.Left:
                            _git.Config.Servers[beforeState.VoiceChannel.Guild.Id.ToString()].VcTextRole.TryGetValue(beforeState.VoiceChannel.Id.ToString(), out roleId);
                            if (!string.IsNullOrWhiteSpace(roleId))
                            {
                                var role = beforeState.VoiceChannel.Guild.GetRole(ulong.Parse(roleId));
                                if (role != null)
                                {
                                    await user.RemoveRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Left VC" });
                                }
                            }
                            break;

                        case VcChangeState.Moved:
                            _git.Config.Servers[beforeState.VoiceChannel.Guild.Id.ToString()].VcTextRole.TryGetValue(beforeState.VoiceChannel.Id.ToString(), out roleId);
                            if (!string.IsNullOrWhiteSpace(roleId))
                            {
                                var role = beforeState.VoiceChannel.Guild.GetRole(ulong.Parse(roleId));
                                if (role != null)
                                {
                                    await user.RemoveRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Left VC" });
                                }
                            }

                            _git.Config.Servers[afterState.VoiceChannel.Guild.Id.ToString()].VcTextRole.TryGetValue(afterState.VoiceChannel.Id.ToString(), out roleId);
                            if (!string.IsNullOrWhiteSpace(roleId))
                            {
                                var role = afterState.VoiceChannel.Guild.GetRole(ulong.Parse(roleId));
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Joined VC" });
                                }
                            }

                            break;
                    }
                }
                catch (Exception) {}
            }
        }

        private async Task _discord_JoinedGuild(SocketGuild arg)
        {
            var serverConfig = new ConfigClass.ServerConfigClass { Prefix = "db!" };    // Create new server config object
            var guildId = arg.Id.ToString();    // Get the guild ID
            _git.Config.Servers[guildId] = serverConfig;    // Add server config to global config
            await _git.UploadConfig();  // Upload new config to GitHub
        }

        private async Task _discord_MessageReceived(SocketMessage s)
        {
            // Ensure the message is from a user/bot
            if (!(s is SocketUserMessage msg))
                return;
            if (msg.Author.Id == _discord.CurrentUser.Id)
                return;     // Ignore self when checking commands

            var context = new SocketCommandContext(_discord, msg);     // Create the command context

            var guildId = context.Guild.Id.ToString();

            if (_git.Config.Servers[guildId].AutoReact.ContainsKey(context.Channel.Id.ToString()))
            {
                var emotesString = _git.Config.Servers[guildId].AutoReact[context.Channel.Id.ToString()];
                List<IEmote> emotes = new List<IEmote>(emotesString.Count);
                foreach (var text in emotesString)
                {
                    IEmote emote;
                    try
                    {
                        emote = Emote.Parse(text);
                    }
                    catch (Exception)
                    {
                        emote = new Emoji(text);
                    }
                    emotes.Add(emote);
                }
                await msg.AddReactionsAsync(emotes.ToArray());
            }
            else
            {
                string prefix = _git.Config.Servers[guildId]?.Prefix ?? "db!";

                int argPos = 0;     // Check if the message has a valid command prefix
                if (msg.HasStringPrefix(prefix, ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
                {
                    var result = await _commands.ExecuteAsync(context, argPos, _provider);     // Execute the command

                    if (!result.IsSuccess)
                    {
                        switch (result.Error)
                        {
                            case CommandError.UnknownCommand:
                                break;

                            case CommandError.BadArgCount:
                                await context.Channel.SendMessageAsync("Wrong argument count");
                                break;

                            case CommandError.UnmetPrecondition:
                                await context.Channel.SendMessageAsync(result.ErrorReason);
                                break;

                            default:
                                await context.Channel.SendMessageAsync("I've had trouble processing that command, try again later");
                                break;
                        }
                    }
                }
            }
        }
    }
}
