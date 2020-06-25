using Discord;
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

        private readonly Dictionary<ulong, List<ulong>> _guildUniqueSenders = new Dictionary<ulong, List<ulong>>();

        public CommandHandler(
            DiscordSocketClient discord,
            CommandService commands,
            IServiceProvider provider,
            GitHubService git,
            AppDbContext dbContext,
            IScheduler scheduler,
            LoggingService logging)
        {
            _discord = discord;
            _commands = commands;
            _provider = provider;
            _git = git;
            _dbContext = dbContext;
            _scheduler = scheduler;
            _logging = logging;

            _discord.MessageReceived += Discord_MessageReceived;   // Bind MessageReceived event
            _discord.JoinedGuild += Discord_JoinedGuild;   // Bind JoinedGuild event
            _discord.UserVoiceStateUpdated += Discord_UserVoiceStateUpdated;

            _commands.AddTypeReader(typeof(object), new ObjectTypeReader());    // Add object type reader
            _commands.AddTypeReader(typeof(Uri), new UriTypeReader());

            var defaultJobDataMap = new JobDataMap()
            {
                {"Client", _discord },
                {"GitHub", _git },
                {"Logging", _logging },
                {"DbContext", _dbContext },
                { "UniqueSendersDict", _guildUniqueSenders }
            };

            IJobDetail generalStatsJob = JobBuilder.Create<GeneralStatsJob>()
                .WithIdentity("generalStatsJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger generalStatsTrigger = TriggerBuilder.Create()
                .WithIdentity("generalStatsTrigger", "discordGroup")
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(5).WithRepeatCount(1))
                .WithCronSchedule("0 1 2 1/1 * ? *")
                .StartNow()
                .Build();

            _scheduler.ScheduleJob(generalStatsJob, generalStatsTrigger).Wait();
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
                    string roleId;

                    switch (state)
                    {
                        case VcChangeState.Joined:
                            _git.Config.Servers[afterState.VoiceChannel.Guild.Id.ToString()].VcTextRole.TryGetValue(afterState.VoiceChannel.Id.ToString(), out roleId); // Try get role ID for channel
                            if (!string.IsNullOrWhiteSpace(roleId))
                            {
                                var role = afterState.VoiceChannel.Guild.GetRole(ulong.Parse(roleId));  // Get role object
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Joined VC" }); // Add role
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
                                    await user.RemoveRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Left VC" });    // Remove role
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
                                    await user.RemoveRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Left VC" });    // Remove role
                                }
                            }

                            _git.Config.Servers[afterState.VoiceChannel.Guild.Id.ToString()].VcTextRole.TryGetValue(afterState.VoiceChannel.Id.ToString(), out roleId);
                            if (!string.IsNullOrWhiteSpace(roleId))
                            {
                                var role = afterState.VoiceChannel.Guild.GetRole(ulong.Parse(roleId));
                                if (role != null)
                                {
                                    await user.AddRoleAsync(role, new Discord.RequestOptions { AuditLogReason = "Joined VC" }); // Add role
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
            var serverConfig = new ConfigClass.ServerConfigClass { Prefix = "db!" };    // Create new server config object
            var guildId = arg.Id.ToString();    // Get the guild ID
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
            var guildIdString = context.Guild.Id.ToString();  // Get guild ID

            if (_guildUniqueSenders.ContainsKey(guildId))
            {
                if (!_guildUniqueSenders[guildId].Contains(context.User.Id))
                {
                    _guildUniqueSenders[guildId].Add(context.User.Id);
                }
            }
            else
                _guildUniqueSenders[guildId] = new List<ulong> { context.User.Id };

            if (_git.Config.Servers[guildIdString].AutoReact.ContainsKey(context.Channel.Id.ToString()))  // Check if channel is set to be auto reacted in
            {
                var emotesString = _git.Config.Servers[guildIdString].AutoReact[context.Channel.Id.ToString()];   // Get strings of emotes
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
            string prefix = _git.Config.Servers[guildIdString]?.Prefix ?? "db!";

            int argPos = 0;     // Check if the message has a valid command prefix
            if (msg.HasStringPrefix(prefix, ref argPos) || msg.HasMentionPrefix(_discord.CurrentUser, ref argPos))
            {
                bool executeCommand = false;
                var command = msg.Content.Split(' ')[0].Substring(argPos);
                if (command != "mod")
                {
                    command = _commands.Commands.First(x => x.Aliases.Contains(command)).Name;
                    if (_git.Config.Servers[guildIdString].CommandLimit.ContainsKey(command))
                    {
                        if (_git.Config.Servers[guildIdString].CommandLimit[command].Contains(context.Channel.Id.ToString()))
                        {
                            executeCommand = true;
                        }
                        else
                            executeCommand = false;
                    }
                    else
                        executeCommand = true;
                }
                else
                    executeCommand = true;


                if (executeCommand)
                {
                    var result = await _commands.ExecuteAsync(context, argPos, _provider);     // Execute the command

                    if (!result.IsSuccess)
                    {
                        await context.Channel.SendMessageAsync($"Something went wrong: {result.Error} - {result.ErrorReason}");
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
                var git = (GitHubService)dataMap["GitHub"];
                var logging = (LoggingService)dataMap["Logging"];
                var dbContext = (AppDbContext)dataMap["DbContext"];
                var uniqueSenders = (Dictionary<ulong, List<ulong>>)dataMap["UniqueSendersDict"];

                await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", "Updating general statistics"));

                try
                {
                    var today = DateTime.Today.AddDays(-1);

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
