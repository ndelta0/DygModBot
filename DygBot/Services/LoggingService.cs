using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;
using Serilog;
using Serilog.Sinks.File;
using Serilog.Sinks.SystemConsole;
using Serilog.Sinks.Async;
using System.IO;

namespace DygBot.Services
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Async(a => a.File(Path.Combine("logs", "mainlog-.log"), rollingInterval: RollingInterval.Day))
                .WriteTo.Async(a => a.Console())
                .CreateLogger();

            _discord = discord;
            _commands = commands;

            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        public Task OnLogAsync(LogMessage msg)
        {
            var source = msg.Source;
            var message = msg.Message ?? "";
            var exception = msg.Exception;

            switch (msg.Severity)
            {
                default:
                case LogSeverity.Debug:
                    if (exception == null)
                        Log.Debug("[{Source}] {Message}", source, message);
                    else
                        Log.Debug(exception, "[{Source}] {Message}", source, message);
                    break;

                case LogSeverity.Verbose:
                    if (exception == null)
                        Log.Verbose("[{Source}] {Message}", source, message);
                    else
                        Log.Verbose(exception, "[{Source}] {Message}", source, message);
                    break;

                case LogSeverity.Info:
                    if (exception == null)
                        Log.Information("[{Source}] {Message}", source, message);
                    else
                        Log.Information(exception, "[{Source}] {Message}", source, message);
                    break;

                case LogSeverity.Warning:
                    if (exception == null)
                        Log.Warning("[{Source}] {Message}", source, message);
                    else
                        Log.Warning(exception, "[{Source}] {Message}", source, message);
                    break;

                case LogSeverity.Error:
                    if (exception == null)
                        Log.Error("[{Source}] {Message}", source, message);
                    else
                        Log.Error(exception, "[{Source}] {Message}", source, message);
                    break;

                case LogSeverity.Critical:
                    if (exception == null)
                        Log.Fatal("[{Source}] {Message}", source, message);
                    else
                        Log.Fatal(exception, "[{Source}] {Message}", source, message);
                    break;
            }

            return Task.CompletedTask;
        }
    }
}
