using System.Threading.Tasks;

using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

using DygBot.Addons.EmoteActioner;

namespace DygBot.Addons
{
    public abstract class ExtendedInteractiveBase : ExtendedInteractiveBase<SocketCommandContext>
    {
    }

    public abstract class ExtendedInteractiveBase<T> : InteractiveBase<T> where T : SocketCommandContext
    {
        public ExtendedInteractiveService ExtendedInteractive { get; set; }

        public Task<IUserMessage> ActionerReplyAsync(ActionerMessage actioner)
            => ActionerReplyAsync(actioner, new EmptyCriterion<SocketReaction>());

        public Task<IUserMessage> ActionerReplyAsync(ActionerMessage actioner, ICriterion<SocketReaction> criterion)
            => ExtendedInteractive.SendActionerMessageAsync(Context, actioner, criterion);
    }
}
