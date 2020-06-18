using Discord.Commands;
using Discord.WebSocket;
using DygBot.Services;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace DygBot.Preconditions
{
    public class RequireManagementRoleAttribute : PreconditionAttribute
    {
        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            var config = services.GetRequiredService<GitHubService>().Config;   // Get config
            var managementRoles = config.Servers[context.Guild.Id.ToString()].ManagementRoles;  // Get roles set up as management
            if (context.User is SocketGuildUser gUser)  // Check if user is a guild member
            {
                var userHasRole = gUser.Roles.Any(x => managementRoles.Contains(x.Id.ToString()));  // Check if member has roles
                if (userHasRole)
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You can't use that command"));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You need to be in the guild to use that command"));
        }
    }
}
