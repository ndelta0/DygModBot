using Discord;
using Discord.API;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.Models;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace DygBot.Services
{
    public class StartupService
    {
        private readonly IServiceProvider _provider;
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;
        private readonly GitHubService _gitHub;
        private readonly LoggingService _logging;
        private readonly IScheduler _scheduler;

        public StartupService(
            IServiceProvider provider,
            DiscordSocketClient discord,
            CommandService commands,
            GitHubService gitHub,
            LoggingService logging,
            IScheduler scheduler)
        {
            _provider = provider;
            _discord = discord;
            _commands = commands;
            _gitHub = gitHub;
            _logging = logging;
            _scheduler = scheduler;
        }

        public async Task StartAsync()
        {
            await _gitHub.DownloadConfig();
            var discordToken = _gitHub.Config.DiscordToken; // Get Discord token
            if (string.IsNullOrWhiteSpace(discordToken)) // Check if token is valid
                throw new ArgumentNullException("Discord token not provided");  // Throw exception if token is invalid

            try
            {
                await _discord.LoginAsync(TokenType.Bot, discordToken); // Login to Discord
                await _discord.StartAsync();    // Connect to the websocket
            }
            catch (Exception e)
            {
                await _logging.OnLogAsync(new LogMessage(LogSeverity.Critical, "Discord", e.Message, e));   // Log exception
            }

            // Create datamap with required objects
            var defaultJobDataMap = new JobDataMap()
            {
                {"Client", _discord },
                {"GitHub", _gitHub },
                {"Logging", _logging }
            };

            // Create job for updating counters
            IJobDetail countersJob = JobBuilder.Create<UpdateCountersJob>()
                .WithIdentity("updateCountersJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger countersTrigger = TriggerBuilder.Create()
                .WithIdentity("updateCountersTrigger", "discordGroup")
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
                .WithCronSchedule("0 0/5 * 1/1 * ? *")
                .StartNow()
                .Build();

            // Create job for clearing messages
            IJobDetail clearJob = JobBuilder.Create<ClearVcChatJob>()
                .WithIdentity("clearVcChatJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger clearTrigger = TriggerBuilder.Create()
                .WithIdentity("cleatVcChatTrigger", "discordGroup")
                .WithCronSchedule("0 0 4 1/1 * ? *")
                .StartNow()
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(5))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(1).WithRepeatCount(0))
                .Build();

            IJobDetail lockdownBeginJob = JobBuilder.Create<LockdownBeginJob>()
                .WithIdentity("lockdownBeginJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger lockdownBeginTrigger = TriggerBuilder.Create()
                .WithIdentity("lockdownBeginTrigger", "discordGroup")
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(5))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(5).WithRepeatCount(0))
                .WithCronSchedule("0 0 1 1/1 * ? *")
                .StartNow()
                .Build();

            IJobDetail lockdownEndJob = JobBuilder.Create<LockdownEndJob>()
                .WithIdentity("lockdownEndJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger lockdownEndTrigger = TriggerBuilder.Create()
                .WithIdentity("lockdownEndTrigger", "discordGroup")
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(5).WithRepeatCount(0))
                .WithCronSchedule("0 0 5 1/1 * ? *")
                .StartNow()
                .Build();

            // Schedule jobs
            await _scheduler.ScheduleJob(countersJob, countersTrigger);
            await _scheduler.ScheduleJob(clearJob, clearTrigger);
            await _scheduler.ScheduleJob(lockdownBeginJob, lockdownBeginTrigger);
            await _scheduler.ScheduleJob(lockdownEndJob, lockdownEndTrigger);

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider); // Load commands and modules into the command service
        }

        public class UpdateCountersJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                // Retrieve objects
                var dataMap = context.JobDetail.JobDataMap;
                var client = (DiscordSocketClient)dataMap["Client"];
                var git = (GitHubService)dataMap["GitHub"];
                var logging = (LoggingService)dataMap["Logging"];

                await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", "Updating counters"));  // Log

                foreach (var kvp in git.Config.Servers)
                {
                    if (!client.Guilds.Any(x => x.Id == kvp.Key))    // If bot is not in server
                    {
                        continue;
                    }
                    foreach (var countChannel in kvp.Value.CountChannels)
                    {
                        var guild = client.Guilds.First(x => x.Id == kvp.Key);   // Get guild object
                        var channel = guild.Channels.First(x => x.Id == countChannel.Key);   // Get channel object
                        int value = 0;
                        switch (countChannel.Value.Property)
                        {
                            case GitHubService.ConfigClass.ServerConfigClass.CountChannelClass.CountPropertyEnum.Members:
                                value = guild.MemberCount;  // Get member count
                                break;
                            case GitHubService.ConfigClass.ServerConfigClass.CountChannelClass.CountPropertyEnum.Bans:
                                value = (await guild.GetBansAsync()).Count; // Get amount of bans
                                break;
                            default:
                                break;
                        }
                        string template = countChannel.Value.Template;  // Get the name template
                        string changedName = template.Replace("%num%", value.ToString());   // Replace the placeholder with number
                        await channel.ModifyAsync(x => x.Name = changedName);   // Change the channel name
                    }
                }
            }
        }
        public class ClearVcChatJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var dataMap = context.JobDetail.JobDataMap;
                var client = (DiscordSocketClient)dataMap["Client"];
                var git = (GitHubService)dataMap["GitHub"];
                var logging = (LoggingService)dataMap["Logging"];

                await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", "Clearing VC chat"));

                var channelIds = new ulong[] { 720790650982891651, 721114067355435058, 721113555813924885, 721113291677499495 };    // Channels to be cleared
                //var channelIds = new ulong[] { 722187075176235061 };
                var guild = client.Guilds.First(x => x.Id == 683084560451633212);   // Server with those channels
                //var guild = client.Guilds.First(x => x.Id == 685477359213608960);

                foreach (var channelId in channelIds)
                {
                    int messagesCleared = 0;
                    var channel = guild.GetTextChannel(channelId);  // Get channel object
                    bool hasMessages = false;   // Flag for setting if there are more messages to clear
                    do
                    {
                        var messages = (await channel.GetMessagesAsync().FlattenAsync()).ToList();  // Get a list of messages
                        hasMessages = messages.Count > 0;   // Set the flag
                        if (hasMessages)
                        {
                            if (messages.Last().CreatedAt.UtcDateTime.AddDays(14).CompareTo(DateTime.UtcNow) == 1)  // Check if messages are older than 14 days (Discord doesn't allow bulk deletion of messages older than 14 days)
                            {
                                messagesCleared += messages.Count;  // Increase deleted messages count
                                await channel.DeleteMessagesAsync(messages);    // Bulk delete messages
                            }
                            else
                                hasMessages = false;
                        }
                    } while (hasMessages);  // Delete until all messages are deleted or are older than 14 days
                    await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", $"Cleared {messagesCleared} messages in {channel.Name}"));  // Log number of deleted messages
                }
            }
        }
        public class LockdownBeginJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var dataMap = context.JobDetail.JobDataMap;
                var client = (DiscordSocketClient)dataMap["Client"];
                var git = (GitHubService)dataMap["GitHub"];
                var logging = (LoggingService)dataMap["Logging"];

                await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", "Beggining lockdown"));

                try
                {
                    var guild = client.GetGuild(683084560451633212);

                    var role = guild.GetRole(725026970697728083);
                    var newPerm = role.Permissions.Modify(sendMessages: false);
                    await role.ModifyAsync(x => x.Permissions = newPerm, new RequestOptions { AuditLogReason = "Lockdown begin" });
                }
                catch (Exception ex)
                {
                    await logging.OnLogAsync(new LogMessage(LogSeverity.Error, "Quartz", "Lockdown begin error", ex));
                }
            }
        }
        public class LockdownEndJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var dataMap = context.JobDetail.JobDataMap;
                var client = (DiscordSocketClient)dataMap["Client"];
                var git = (GitHubService)dataMap["GitHub"];
                var logging = (LoggingService)dataMap["Logging"];

                await logging.OnLogAsync(new LogMessage(LogSeverity.Info, "Quartz", "Ending lockdown"));

                try
                {
                    var guild = client.GetGuild(683084560451633212);

                    var role = guild.GetRole(725026970697728083);
                    var newPerm = role.Permissions.Modify(sendMessages: true);
                    await role.ModifyAsync(x => x.Permissions = newPerm, new RequestOptions { AuditLogReason = "Lockdown end" });
                }
                catch (Exception ex)
                {
                    await logging.OnLogAsync(new LogMessage(LogSeverity.Error, "Quartz", "Lockdown end error", ex));
                }
            }
        }
    }
}
