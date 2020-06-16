using Discord;
using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Threading.Tasks;

namespace DygBot.Services
{
    public class LoggingService
    {
        private readonly DiscordSocketClient _discord;
        private readonly CommandService _commands;

        // DiscordSocketClient and CommandService are injected automatically from the IServiceProvider
        public LoggingService(DiscordSocketClient discord, CommandService commands)
        {
            _discord = discord;
            _commands = commands;

            _discord.Log += OnLogAsync;
            _commands.Log += OnLogAsync;
        }

        public Task OnLogAsync(LogMessage msg)
        {
            switch (msg.Severity)
            {
                case LogSeverity.Debug:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;

                case LogSeverity.Verbose:
                    Console.ForegroundColor = ConsoleColor.White;
                    break;

                case LogSeverity.Info:
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    break;

                case LogSeverity.Warning:
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    break;

                case LogSeverity.Error:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;

                case LogSeverity.Critical:
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.BackgroundColor = ConsoleColor.Red;
                    break;

                default:
                    Console.ResetColor();
                    break;
            }

            string log = $"[{DateTime.UtcNow.ToLongTimeString()} - {DateTime.UtcNow.ToShortDateString()}] "
                + $"[{msg.Source}/{msg.Severity}]\t{msg.Exception?.ToString() ?? msg.Message}";

            Console.WriteLine(log);
            Console.ResetColor();

            return Task.CompletedTask;
        }
    }
}
