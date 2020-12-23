using System;
using System.Threading.Tasks;

using Discord.Commands;
using Discord.WebSocket;

namespace DygBot.Preconditions
{
    public class RequireUserAttribute : PreconditionAttribute
    {
        private readonly ulong _userId;

        public RequireUserAttribute(ulong userId) => _userId = userId;  // Set allowed user's ID

        public override Task<PreconditionResult> CheckPermissionsAsync(ICommandContext context, CommandInfo command, IServiceProvider services)
        {
            if (context.User is SocketGuildUser gUser)  // Check if user is a server member
            {
                if (gUser.Id == _userId)    // Check if user is the specified user
                    return Task.FromResult(PreconditionResult.FromSuccess());
                else
                    return Task.FromResult(PreconditionResult.FromError("You can't use that command"));
            }
            else
                return Task.FromResult(PreconditionResult.FromError("You need to be in the guild to use that command"));
        }
    }
}
