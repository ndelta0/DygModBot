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
                await _logging.OnLogAsync(new Discord.LogMessage(Discord.LogSeverity.Critical, "Discord", e.Message, e));
            }
            
            var props = new NameValueCollection
            {
                {"quartz.serializer.type", "binary" }
            };
            var factory = new StdSchedulerFactory(props);
            IScheduler scheduler = await factory.GetScheduler();
            await scheduler.Start();

            var defaultJobDataMap = new JobDataMap()
            {
                {"Client", _discord },
                {"GitHub", _gitHub }
            };

            IJobDetail job = JobBuilder.Create<UpdateCountersJob>()
                .WithIdentity("updateCountersJob", "discordGroup")
                .UsingJobData(defaultJobDataMap)
                .Build();
            ITrigger trigger = TriggerBuilder.Create()
                .WithIdentity("updateCountersTrigger", "discordGroup")
                .StartAt(DateTimeOffset.UtcNow.AddSeconds(15))
                .WithSimpleSchedule(x => x.WithIntervalInMinutes(5).RepeatForever())
                .Build();

            await scheduler.ScheduleJob(job, trigger);

            await _commands.AddModulesAsync(Assembly.GetExecutingAssembly(), _provider); // Load commands and modules into the command service

            return scheduler;
        }



        public class UpdateCountersJob : IJob
        {
            public async Task Execute(IJobExecutionContext context)
            {
                var dataMap = context.JobDetail.JobDataMap;
                var client = (DiscordSocketClient)dataMap["Client"];
                var git = (GitHubService)dataMap["GitHub"];

                foreach (var kvp in git.Config.Servers)
                {
                    if (!client.Guilds.Any(x => x.Id.ToString() == kvp.Key))
                    {
                        continue;
                    }
                    foreach (var countChannel in kvp.Value.CountChannels)
                    {
                        var guild = client.Guilds.First(x => x.Id.ToString() == kvp.Key);
                        var channel = guild.Channels.First(x => x.Id.ToString() == countChannel.Key);
                        int value = 0;
                        switch (countChannel.Value.Property)
                        {
                            case GitHubService.ConfigClass.ServerConfigClass.CountChannelClass.CountPropertyEnum.Members:
                                value = guild.MemberCount;
                                break;
                            case GitHubService.ConfigClass.ServerConfigClass.CountChannelClass.CountPropertyEnum.Bans:
                                value = (await guild.GetBansAsync()).Count;
                                break;
                            default:
                                break;
                        }
                        string template = countChannel.Value.Template;
                        string changedName = template.Replace("%num%", value.ToString());
                        await channel.ModifyAsync(x => x.Name = changedName);
                    }
                }
            }
        }
    }
}
