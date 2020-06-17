using Discord;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DygBot
{
    public class Startup
    {
        public static async Task RunAsync()
        {
            // Create a new instance of a service collection
            var services = new ServiceCollection();
            ConfigureServices(services);

            var provider = services.BuildServiceProvider(); // Build the service provider
            provider.GetRequiredService<LoggingService>();  // Start the logging service
            provider.GetRequiredService<CommandHandler>();  // Start the command handler service

            var scheduler = await provider.GetRequiredService<StartupService>().StartAsync();   // Start the startup service
            await Task.Delay(-1);   // Keep the program alive
            await scheduler.Shutdown();
        }

        private static void ConfigureServices(IServiceCollection services)
        {
            services
                .AddSingleton(new DiscordSocketClient(new DiscordSocketConfig   // Add Discord to the collection
                {
                    LogLevel = LogSeverity.Info,
                    MessageCacheSize = 1000, // Cache 1000 messages per channel
                    ExclusiveBulkDelete = true
                }))
                .AddSingleton(new CommandService(new CommandServiceConfig   // Add the command service to the collection
                {
                    LogLevel = LogSeverity.Verbose,
                    DefaultRunMode = RunMode.Async,  // Force all commands to run async by default
                    CaseSensitiveCommands = false
                }))
                .AddSingleton<HttpClient>() // Add the HttpClient to the collection
                .AddSingleton<GitHubService>()  // Add the GitHub service to the collection
                .AddSingleton<CommandHandler>() // Add the command handler to the collection
                .AddSingleton<StartupService>() // Add the startup service to the collection
                .AddSingleton<LoggingService>() // Add the logging service to the collection
                .AddSingleton<Random>();     // Add random to the collection
        }
    }
}
