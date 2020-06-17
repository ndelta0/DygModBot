using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Quartz;
using Quartz.Impl;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Reflection;
using System.Text;
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

        public StartupService(
            IServiceProvider provider,
            DiscordSocketClient discord,
            CommandService commands,
            GitHubService gitHub,
            LoggingService logging)
        {
            _provider = provider;
            _discord = discord;
            _commands = commands;
            _gitHub = gitHub;
            _logging = logging;
        }

        public async Task<IScheduler> StartAsync()
        {
            await _gitHub.DownloadConfig();
            var discordToken = _gitHub.Config.DiscordToken; // Get Discord token
            if (string.IsNullOrWhiteSpace(discordToken)) // Check if token is valid
                throw new ArgumentNullException("Discord token not provided");  // Throw exception if token is invalid

            try
            {
                await _discord.LoginAsync(Discord.TokenType.Bot, discordToken); // Login to Discord
                await _discord.StartAsync();    // Connect to the websocket
            } catch (Exception e)
            {
                await _logging.OnLogAsync(new Discord.LogMessage(Discord.LogSeverity.Critical, "Discord", e.Message, e));   // Log exception
            }
            
            var props = new NameValueCollection
            {
                {"quartz.serializer.type", "binary" }
            };
            var factory = new StdSchedulerFactory(props);
            IScheduler scheduler = await factory.GetScheduler();
            await scheduler.Start();

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
                .WithCronSchedule("0 0 6 1/1 * ? *")
                //.WithCronSchedule("0 0/1 * 1/1 * ? *")
                .StartNow()
                //.StartAt(DateTimeOffset.UtcNow.AddSeconds(5))
                //.WithSimpleSchedule(x => x.WithIntervalInMinutes(1).WithRepeatCount(0))
                .Build();

            // Schedule jobs
            await scheduler.ScheduleJob(countersJob, countersTrigger);
            await scheduler.ScheduleJob(clearJob, clearTrigger);

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider); // Load commands and modules into the command service

            return scheduler;
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

                await logging.OnLogAsync(new Discord.LogMessage(Discord.LogSeverity.Info, "Quartz", "Updating counters"));  // Log
                
                foreach (var kvp in git.Config.Servers)
                {
                    if (!client.Guilds.Any(x => x.Id.ToString() == kvp.Key))    // If bot is not in server
                    {
                        continue;
                    }
                    foreach (var countChannel in kvp.Value.CountChannels)
                    {
                        var guild = client.Guilds.First(x => x.Id.ToString() == kvp.Key);   // Get guild object
                        var channel = guild.Channels.First(x => x.Id.ToString() == countChannel.Key);   // Get channel object
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
    }
}
