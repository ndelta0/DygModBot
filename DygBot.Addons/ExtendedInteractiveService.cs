using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

using DygBot.Addons;
using DygBot.Addons.EmoteActioner;

namespace DygBot.Addons
{
    public class ExtendedInteractiveService : InteractiveService
    {
        private readonly Dictionary<ulong, IExtendedReactionCallback> _callbacksRemoved;

        public ExtendedInteractiveService(DiscordSocketClient discord, InteractiveServiceConfig config = null)
            : this((BaseSocketClient)discord, config) { }

        public ExtendedInteractiveService(DiscordShardedClient discord, InteractiveServiceConfig config = null)
            : this((BaseSocketClient)discord, config) { }

        public ExtendedInteractiveService(BaseSocketClient discord, InteractiveServiceConfig config = null) : base(discord, config)
        {
            Discord.ReactionRemoved += HandleReactionRemovedAsync;

            _callbacksRemoved = new Dictionary<ulong, IExtendedReactionCallback>();
        }

        public void AddReactionRemovedCallback(IMessage message, IExtendedReactionCallback callback)
            => _callbacksRemoved[message.Id] = callback;

        public void RemoveReactionRemovedCallback(IMessage message)
            => RemoveReactionRemovedCallback(message.Id);

        public void RemoveReactionRemovedCallback(ulong id)
            => _callbacksRemoved.Remove(id);

        public void ClearReactionRemovedCallbacks()
            => _callbacksRemoved.Clear();

        private async Task HandleReactionRemovedAsync(Cacheable<IUserMessage, ulong> message,
            ISocketMessageChannel channel,
            SocketReaction reaction)
        {
            if (reaction.UserId == Discord.CurrentUser.Id) return;
            if (!_callbacksRemoved.TryGetValue(message.Id, out var callback)) return;
            if (!await callback.Criterion.JudgeAsync(callback.Context, reaction).ConfigureAwait(false)) return;
            switch (callback.RunMode)
            {
                case RunMode.Async:
                    _ = Task.Run(async () =>
                    {
                        if (await callback.HandleRemovedCallbackAsync(reaction).ConfigureAwait(false))
                            RemoveReactionRemovedCallback(message.Id);
                    });
                    break;
                default:
                    if (await callback.HandleRemovedCallbackAsync(reaction).ConfigureAwait(false))
                        RemoveReactionRemovedCallback(message.Id);
                    break;
            }
        }

        public async Task<IUserMessage> SendActionerMessageAsync(SocketCommandContext context,
            ActionerMessage actioner,
            ICriterion<SocketReaction> criterion = null)
        {
            var callback = new ActionerMessageCallback(this, context, actioner, criterion);
            await callback.DisplayAsync().ConfigureAwait(false);
            return callback.Message;
        }

        public async Task<IUserMessage> SendActionerMessageAsync(ulong guildId, ulong channelId,
            ActionerMessage actionerMessage, ICriterion<SocketReaction> criterion = null)
        {
            var callback = new ActionerMessageCallback(this, Discord, guildId, channelId, actionerMessage, criterion);
            await callback.DisplayAsync().ConfigureAwait(false);
            return callback.Message;
        }

        public async Task<IUserMessage> SendActionerMessageAsync(ITextChannel textChannel,
            ActionerMessage actionerMessage, ICriterion<SocketReaction> criterion = null)
        {
            var callback = new ActionerMessageCallback(this, Discord, textChannel, actionerMessage, criterion);
            await callback.DisplayAsync().ConfigureAwait(false);
            return callback.Message;
        }

        public new void Dispose()
        {
            base.Dispose();
            Discord.ReactionRemoved -= HandleReactionRemovedAsync;
        }
    }
}
