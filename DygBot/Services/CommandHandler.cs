using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.Models;
using DygBot.TypeReaders;
using Quartz;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static DygBot.Services.GitHubService;
using static DygBot.Modules.ModerationModule.ReactionRoleClass;

namespace DygBot.Services
{
    public class CommandHandler
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly IServiceProvider _provider;
        private readonly GitHubService _git;
        private readonly AppDbContext _dbContext;
        private readonly IScheduler _scheduler;
        private readonly LoggingService _logging;
        private readonly InteractiveService _interactive;

        private readonly Dictionary<ulong, HashSet<ulong>> _guildUniqueSenders = new Dictionary<ulong, HashSet<ulong>>();

        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            IServiceProvider provider,
            GitHubService git,
            AppDbContext dbContext,
            IScheduler scheduler,
            LoggingService logging,
            InteractiveService interactiveService)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _git = git;
            _dbContext = dbContext;
            _scheduler = scheduler;
            _logging = logging;
            _interactive = interactiveService;

            _discord.MessageReceived += Discord_MessageReceived;   // Bind MessageReceived event
            _discord.JoinedGuild += Discord_JoinedGuild;   // Bind JoinedGuild event
            _discord.UserVoiceStateUpdated += Discord_UserVoiceStateUpdated;
            _discord.ReactionAdded += Discord_ReactionAdded;
            _discord.ReactionRemoved += Discord_ReactionRemoved;
            _discord.Ready += async () => await _logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Discord", $"Logged in as: {_discord.CurrentUser.Username}"));

            _commands.AddTypeReader<object>(new ObjectTypeReader());
            _commands.AddTypeReader<Uri>(new UriTypeReader());
            _commands.AddTypeReader<TimeSpan>(new CustomTimeSpanTypeReader(), true);
            _commands.AddTypeReader<IEmote>(new IEmoteTypeReader());
            _commands.AddTypeReader<IMessage>(new IMessageTypeReader());

            var defaultJobDataMap = new JobDataMap()
            {
                {"Client", _discord },
                {"GitHub", _git },
                {"Logging", _logging },
                {"DbContext", _dbContext },
                {"UniqueSendersDict", _guildUniqueSenders }
            };

            IJobDetail generalStatsJob = JobBuilder.Create<GeneralStatsJob>()
                .WithIdentity("generalStatsJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger generalStatsTrigger = TriggerBuilder.Create()
                .WithIdentity("generalStatsTrigger", "discordGroup")
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(5).WithRepeatCount(1))
                .WithCronSchedule("0 1 22 1/1 * ? *")
                .StartNow()
                .Build();

            _scheduler.ScheduleJob(generalStatsJob, generalStatsTrigger).Wait();
        }

        

        private async Task Discord_ReactionAdded(Cacheable<IUserMessage, ulong> userCacheable, ISocketMessageChannel socketMessageChannel, SocketReaction socketReaction)
        {
            if (socketReaction.UserId == _discord.CurrentUser.Id)
                return;

            if (socketMessageChannel.Id != 719251462697517227)
            {
                if (socketReaction.Emote.Equals(Emote.Parse("<:rzyg:719249995064279112>")))
                {
                    if (socketReaction.User.IsSpecified)
                    {
                        if (socketReaction.User.Value is SocketGuildUser user)
                        {
                            if (!user.Roles.Any(x => x.Id == 683095642800652375 || x.Id == 683095728402596006))
                            {
                                if ((await userCacheable.GetOrDownloadAsync()) is SocketUserMessage message)
                                {
                                    await message.RemoveReactionAsync(Emote.Parse("<:rzyg:719249995064279112>"), user);
                                }
                            }
                        }
                    }
                }
            }

            var guild = (socketMessageChannel as SocketTextChannel).Guild;
            if (_git.Config.Servers[guild.Id].ReactionRoles.ContainsKey(socketMessageChannel.Id))
            {
                var message = await userCacheable.GetOrDownloadAsync();
                if (_git.Config.Servers[guild.Id].ReactionRoles[socketMessageChannel.Id].TryGetValue(message.Id, out var reactionRoles))
                {
                    foreach (var item in reactionRoles)
                    {
                        ulong roleId;
                        if (socketReaction.User.Value is SocketGuildUser member)
                        {
                            switch (item.Action)
                            {
                                case ReactionAction.GiveRemove:
                                case ReactionAction.Give:
                                    if (item.Roles.TryGetValue(socketReaction.Emote.ToString(), out roleId))
                                    {
                                        var role = guild.GetRole(roleId);
                                        if (role != null)
                                        {
                                            if (!member.Roles.Contains(role))
                                            {
                                                await member.AddRoleAsync(role);
                                            }
                                        }
                                    }
                                    break;
                                case ReactionAction.Remove:
                                    if (item.Roles.TryGetValue(socketReaction.Emote.ToString(), out roleId))
                                    {
                                        var role = guild.GetRole(roleId);
                                        if (role != null)
                                        {
                                            if (member.Roles.Contains(role))
                                            {
                                                await member.RemoveRoleAsync(role);
                                            }
                                        }
                                    }
                                    break;
                                case ReactionAction.OneOfMany:
                                    IEmote emote;
                                    foreach (var kvp in item.Roles)
                                    {
                                        var role = guild.GetRole(kvp.Value);
                                        if (role != null)
                                        {
                                            if (kvp.Key == socketReaction.Emote.ToString())
                                            {
                                                if (!member.Roles.Contains(role))
                                                {
                                                    await member.AddRoleAsync(role);
                                                }
                                            }
                                            else
                                            {
                                                if (member.Roles.Contains(role))
                                                {
                                                    await member.RemoveRoleAsync(role);
                                                }

                                                if (Emote.TryParse(kvp.Key, out Emote emoteTmp))
                                                    emote = emoteTmp;
                                                else
                                                    emote = new Emoji(kvp.Key);
                                                if (emote != null)
                                                    await message.RemoveReactionAsync(emote, member);
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private async Task Discord_ReactionRemoved(Cacheable<IUserMessage, ulong> userCacheable, ISocketMessageChannel socketMessageChannel, SocketReaction socketReaction)
        {
            if (socketReaction.UserId == _discord.CurrentUser.Id)
                return;

            var guild = (socketMessageChannel as SocketTextChannel).Guild;
            if (_git.Config.Servers[guild.Id].ReactionRoles.ContainsKey(socketMessageChannel.Id))
            {
                if (_git.Config.Servers[guild.Id].ReactionRoles[socketMessageChannel.Id].TryGetValue((await userCacheable.GetOrDownloadAsync()).Id, out var reactionRoles))
                {
                    foreach (var item in reactionRoles)
                    {
                        if (socketReaction.User.Value is SocketGuildUser member)
                        {
                            switch (item.Action)
                            {
                                case ReactionAction.GiveRemove:
                                case ReactionAction.OneOfMany:
                                    if (item.Roles.TryGetValue(socketReaction.Emote.ToString(), out ulong roleId))
                                    {
                                        var role = guild.GetRole(roleId);
                                        if (role != null)
                                        {
                                            if (member.Roles.Contains(role))
                                            {
                                                await member.RemoveRoleAsync(role);
                                            }
                                        }
                                    }
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private enum VcChangeState  // Enum with states of user being in VC
        {
            None,
            Left,
            Joined,
            Moved
        }

        private async Task Discord_UserVoiceStateUpdated(SocketUser socketUser, SocketVoiceState beforeState, SocketVoiceState afterState)
        {
            if (socketUser is SocketGuildUser user)
            {
                // Work out user action
                var state = VcChangeState.None;
                if (beforeState.VoiceChannel == null && afterState.VoiceChannel != null)
                    state = VcChangeState.Joined;
                if (beforeState.VoiceChannel != null && afterState.VoiceChannel == null)
                    state = VcChangeState.Left;
                if (beforeState.VoiceChannel != null && afterState.VoiceChannel != null)
                    state = VcChangeState.Moved;


                try
                {
                    ulong roleId;

                    switch (state)
                    {
                        case VcChangeState.Joined:
                            _git.Config.Servers[afterState.VoiceChannel.Guild.Id].VcTextRole.TryGetValue(afterState.VoiceChannel.Id, out roleId); // Try get role ID for channel
                            if (roleId != default)
                            {
                                var role = afterState.VoiceChannel.Guild.GetRole(roleId);  // Get role object
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role, new RequestOptions { AuditLogReason = "Joined VC" }); // Add role
                                }
                            }
                            break;

                        case VcChangeState.Left:
                            _git.Config.Servers[beforeState.VoiceChannel.Guild.Id].VcTextRole.TryGetValue(beforeState.VoiceChannel.Id, out roleId);
                            if (roleId != default)
                            {
                                var role = beforeState.VoiceChannel.Guild.GetRole(roleId);
                                if (role != null)
                                {
                                    await user.RemoveRoleAsync(role, new RequestOptions { AuditLogReason = "Left VC" });    // Remove role
                                }
                            }
                            break;

                        case VcChangeState.Moved:
                            _git.Config.Servers[beforeState.VoiceChannel.Guild.Id].VcTextRole.TryGetValue(beforeState.VoiceChannel.Id, out roleId);
                            if (roleId != default)
                            {
                                var role = beforeState.VoiceChannel.Guild.GetRole(roleId);
                                if (role != null)
                                {
                                    await user.RemoveRoleAsync(role, new RequestOptions { AuditLogReason = "Left VC" });    // Remove role
                                }
                            }

                            _git.Config.Servers[afterState.VoiceChannel.Guild.Id].VcTextRole.TryGetValue(afterState.VoiceChannel.Id, out roleId);
                            if (roleId != default)
                            {
                                var role = afterState.VoiceChannel.Guild.GetRole(roleId);
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role, new RequestOptions { AuditLogReason = "Joined VC" }); // Add role
                                }
                            }

                            break;
                    }
                }
                catch (Exception) { }
            }
        }

        private async Task Discord_JoinedGuild(SocketGuild arg)
        {
            var serverConfig = new ConfigClass.ServerConfigClass();    // Create new server config object
            var guildId = arg.Id;    // Get the guild ID
            _git.Config.Servers[guildId] = serverConfig;    // Add server config to global config
            await _git.UploadConfig();  // Upload new config to GitHub
        }

        private async Task Discord_MessageReceived(SocketMessage s)
        {
            // Ensure the message is from a user/bot
            if (!(s is SocketUserMessage msg))
                return;
            if (msg.Author.Id == _discord.CurrentUser.Id)
                return;     // Ignore self when checking commands

            var context = new SocketCommandContext(_discord, msg);     // Create the command context
            var guildId = context.Guild.Id;

            if (_guildUniqueSenders.ContainsKey(guildId))
            {
                 _guildUniqueSenders[guildId].Add(context.User.Id);
            }
            else
                _guildUniqueSenders[guildId] = new HashSet<ulong> { context.User.Id };

            if (_git.Config.Servers[guildId].AutoReact.ContainsKey(context.Channel.Id))
            {
                if (string.IsNullOrWhiteSpace(context.Message.Content))
                {
                    await context.Message.DeleteAsync(new RequestOptions { AuditLogReason = "Wiadomość bez podpisu" });
                    await _interactive.ReplyAndDeleteAsync(context, "Twoja wiadomość nie zawiera podpisu", timeout: TimeSpan.FromSeconds(3));
                    return;
                }
            }

            if (_git.Config.Servers[guildId].AutoReact.ContainsKey(context.Channel.Id))  // Check if channel is set to be auto reacted in
            {
                var emotesString = _git.Config.Servers[guildId].AutoReact[context.Channel.Id];   // Get strings of emotes
                List<IEmote> emotes = new List<IEmote>(emotesString.Count); // Create a list of emotes

                // Parse emotes
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
                await msg.AddReactionsAsync(emotes.ToArray());  // React with emotes
            }
            string prefix = _git.Config.Servers[guildId]?.Prefix ?? "db!";

            int argPos = 0;     // Check if the message has a valid command prefix
            if (msg.HasStringPrefix(prefix, ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                bool executeCommand = false;
                var commandStr = msg.Content.Split(' ')[0].Substring(argPos);
                var command = _commands.Commands.FirstOrDefault(x => x.Aliases.Contains(commandStr));
                if (command != null)
                {
                    commandStr = command.Name;
                }
                if (_git.Config.Servers[guildId].CommandLimit.ContainsKey(commandStr))
                {
                    if (_git.Config.Servers[guildId].CommandLimit[commandStr].Contains(context.Channel.Id))
                    {
                        executeCommand = true;
                    }
                    else
                        executeCommand = false;
                }
                else
                    executeCommand = true;


                if (executeCommand)
                {
                    var result = await _commands.ExecuteAsync(context, argPos, _provider);     // Execute the command

                    if (!result.IsSuccess)
                    {
                            switch (result.Error)
                            {
                                case CommandError.BadArgCount:
                                    await context.Channel.SendMessageAsync("Zła ilość argumentów");
                                    break;
                                case CommandError.UnmetPrecondition:
                                    if (result.ErrorReason == "Module precondition group Permission failed")
                                        await context.Channel.SendMessageAsync("Nie spełniasz wymogów polecenia - nie masz wymaganych uprawnień");
                                    else
                                        await context.Channel.SendMessageAsync("Nie spełniasz wymogów polecenia");
                                    break;
                                case CommandError.Unsuccessful:
                                case CommandError.Exception:
                                case CommandError.ParseFailed:
                                    await context.Channel.SendMessageAsync("Miałem problem z tym poleceniem");
                                    break;
                            }
                    }
                }
            }
        }

        public class GeneralStatsJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var dataMap = context.JobDetail.JobDataMap;
                var client = (DiscordSocketClient)dataMap["Client"];
                var logging = (LoggingService)dataMap["Logging"];
                var dbContext = (AppDbContext)dataMap["DbContext"];
                var uniqueSenders = (Dictionary<ulong, List<ulong>>)dataMap["UniqueSendersDict"];

                await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", "Updating general statistics"));

                try
                {
                    var today = DateTime.UtcNow.Date;

                    var additions = new List<GeneralStat>(client.Guilds.Count);

                    foreach (var guild in client.Guilds)
                    {
                        var stats = new GeneralStat
                        {
                            DateTime = today,
                            UniqueSenders = uniqueSenders.ContainsKey(guild.Id) ? uniqueSenders[guild.Id].Count : 0
                        };
                        additions.Add(stats);
                    }
                    await dbContext.GeneralStats.AddRangeAsync(additions);
                    await dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    await logging.OnLogAsync(new LogMessage(LogSeverity.Error, "Quartz", "General statistics error", ex));
                }
                finally
                {
                    uniqueSenders.Clear();
                }
            }
        }
    }
}
