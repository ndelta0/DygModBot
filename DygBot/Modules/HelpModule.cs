using Discord;
using Discord.Commands;
using DygBot.Services;
using System.Linq;
using System.Threading.Tasks;

namespace DygBot.Modules
{
    [Group("help")]
    [Summary("Help module")]
    public class HelpModule : ModuleBase<SocketCommandContext>
    {
        private readonly CommandService _service;
        private readonly GitHubService _git;

        public HelpModule(CommandService service, GitHubService git)
        {
            _service = service;
            _git = git;
        }

        [Command("")]
        [Summary("Shows all available commands")]
        [Alias("commands")]
        public async Task HelpAsync()
        {
            string prefix = _git.Config.Servers[Context.Guild.Id.ToString()].Prefix;    // Get prefix for server
            var builder = new EmbedBuilder()    // Create an embed
            {
                Color = Color.Green,    // Set color
                Title = "HELP", // Set title
                Description = $"For more information about a command send __{prefix}help **command**__" // Set description
            };

            foreach (var module in _service.Modules)
            {
                string description = module.IsSubmodule ? "\t" : string.Empty;
                foreach (var cmd in module.Commands)
                {
                    description += $"{prefix}{cmd.Aliases.First()}";
                    foreach (var param in cmd.Parameters)
                    {
                        string paramDesc = "";
                        if (param.IsOptional)
                            paramDesc += "[";
                        else
                            paramDesc += "{";
                        if (param.IsMultiple)
                            paramDesc += "*";
                        if (param.IsRemainder)
                            paramDesc += "^";
                        paramDesc += param.Name;
                        if (param.DefaultValue != null)
                            paramDesc += $"={param.DefaultValue}";
                        if (param.IsOptional)
                            paramDesc += "]";
                        else
                            paramDesc += "}";
                        description += " " + paramDesc;
                    }
                    description += "\n";
                }

                if (!string.IsNullOrWhiteSpace(description))
                {
                    var name = module.IsSubmodule ? "\t" : string.Empty;
                    builder.AddField(x =>
                    {
                        x.Name = name + (module.Summary ?? module.Group);
                        x.Value = description;
                        x.IsInline = false;
                    });
                }

                var footerBuilder = new EmbedFooterBuilder()
                    .WithText("*-Multiple Value Parameter\n^-Remainder Parameter\n[OPTIONAL PARAMETER]\n{REQUIRED PARAMETER}");

                builder.WithFooter(footerBuilder);
                builder.WithCurrentTimestamp();
            }

            await ReplyAsync("", false, builder.Build());
        }

        [Command("")]
        [Summary("Shows details about a command")]
        public async Task HelpAsync([Summary("Command you want to know about")][Remainder] string command)
        {
            var result = _service.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyAsync($"Sorry, I don't have a command like **{command}**.");
                return;
            }
            var builder = new EmbedBuilder()
            {
                Color = Color.Green,
                Description = $"Here are some commands like **{command}**"
            };

            foreach (var match in result.Commands)
            {
                var cmd = match.Command;

                string paramString = "";

                foreach (var param in cmd.Parameters)
                {
                    string paramDesc = "\t";
                    if (param.IsOptional)
                        paramDesc += "[";
                    else
                        paramDesc += "{";
                    if (param.IsMultiple)
                        paramDesc += "*";
                    if (param.IsRemainder)
                        paramDesc += "^";
                    paramDesc += param.Name;
                    if (param.DefaultValue != null)
                        paramDesc += $"={param.DefaultValue}";
                    if (param.IsOptional)
                        paramDesc += "]";
                    else
                        paramDesc += "}";
                    paramDesc += $" - {param.Summary ?? "No summary"}";
                    paramString += paramDesc + "\n";
                }

                string fullDesc = $"**Summary**: {cmd.Summary}";

                if (paramString == "")
                {
                    fullDesc += "\n**No parameters**";
                }
                else
                {
                    fullDesc += $"\n**Parameters**:\n" + paramString;
                }

                string names = "";

                foreach (var name in cmd.Aliases)
                {
                    names += $"{name}/";
                }

                names = names.Remove(names.Length - 1);

                builder.AddField(x =>
                {
                    x.Name = names;
                    x.Value = fullDesc;
                    x.IsInline = false;
                });
            }

            var footerBuilder = new EmbedFooterBuilder()
                    .WithText("*-Multiple Value Parameter\n^-Remainder Parameter\n[OPTIONAL PARAMETER]\n{REQUIRED PARAMETER}");

            builder.WithFooter(footerBuilder);
            builder.WithCurrentTimestamp();

            await ReplyAsync("", false, builder.Build());
        }
    }
}
