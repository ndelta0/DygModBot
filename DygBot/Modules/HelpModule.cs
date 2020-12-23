using System.Linq;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using DygBot.Services;

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
        [Summary("Pokazuje wszystkie dostępne komendy")]
        [Alias("commands")]
        public async Task HelpAsync()
        {
            string prefix = _git.Config.Servers[Context.Guild.Id].Prefix;    // Get prefix for server
            var builder = new EmbedBuilder()    // Create an embed
            {
                Color = new Color(0xFF66CC),    // Set color
                Title = "POMOC", // Set title
                Description = $"Więcej informacji o komendzie: __{prefix}help **komenda**__" // Set description
            };

            foreach (var module in _service.Modules)
            {
                string description = module.IsSubmodule ? "\t" : string.Empty;
                foreach (var cmd in module.Commands)
                {
                    description += $"{prefix}{cmd.Aliases[0]}";
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
                        x.Name = name + ((module.Summary ?? module.Group) ?? module.Name);
                        x.Value = description;
                        x.IsInline = false;
                    });
                }

                var footerBuilder = new EmbedFooterBuilder()
                    .WithText("*-Kilka wartości\n^-Parametr bez końca\n[OPCJONANE]\n{WYMAGANE}");

                builder.WithFooter(footerBuilder);
                builder.WithCurrentTimestamp();
            }

            await ReplyAsync("", false, builder.Build());
        }

        [Command("")]
        [Summary("Pokazuje szczegóły komendy")]
        public async Task HelpAsync([Summary("Komenda")][Remainder] string command)
        {
            var result = _service.Search(Context, command);

            if (!result.IsSuccess)
            {
                await ReplyAsync($"Nie mam komendy **{command}**.");
                return;
            }
            var builder = new EmbedBuilder()
            {
                Color = new Color(0xFF66CC),
                Description = $"Komendy odpowiadające **{command}**"
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
                        paramDesc += $"='{param.DefaultValue}'";
                    if (param.IsOptional)
                        paramDesc += "]";
                    else
                        paramDesc += "}";
                    paramDesc += $" - {param.Summary ?? "brak opisu"}";
                    paramString += paramDesc + "\n";
                }

                string fullDesc = $"**Opis**: {cmd.Summary}";

                if (paramString == "")
                {
                    fullDesc += "\n**Brak parametrów**";
                }
                else
                {
                    fullDesc += $"\n**Parametry**:\n" + paramString;
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
                    .WithText("*-Kilka wartości\n^-Parametr bez końca\n[OPCJONANE]\n{WYMAGANE}");

            builder.WithFooter(footerBuilder);
            builder.WithCurrentTimestamp();

            await ReplyAsync("", false, builder.Build());
        }
    }
}
