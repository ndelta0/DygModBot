using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;
using DygBot.Services;
using SixLabors.ImageSharp.ColorSpaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DygBot.Modules
{
    public class MiscModule : InteractiveBase<SocketCommandContext>
    {
        private readonly GitHubService _git;

        public MiscModule(GitHubService gitHub)
        {
            _git = gitHub;
        }

        [Command("roles")]
        [Summary("Pokazuje role użytkownika")]
        public async Task RolesAsync([Summary("Użytkownik")] SocketGuildUser user = null)
        {
            if (user == null)
            {
                if (Context.User is SocketGuildUser)
                {
                    user = Context.User as SocketGuildUser;
                }
            }
            else
            {
                if (!_git.Config.Servers[Context.Guild.Id].ManagementRoles.Any(x => (Context.User as SocketGuildUser).Roles.Select(r => r.Id).Any(y => y == x)))
                {
                    await ReplyAsync("Możesz sprawdzić tylko swoje role.");
                    return;
                }
            }

            var roles = user.Roles.SkipWhile(x => x.IsEveryone);

            var description = new StringBuilder().AppendJoin('\n', roles).ToString();

            var embed = new EmbedBuilder
            {
                Title = $"Role użytkownika {user.Username}#{user.Discriminator}",
                Description = description
            }.WithCurrentTimestamp().Build();

            await ReplyAsync(embed: embed);
        }
    }
}
