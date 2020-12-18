using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Addons.Interactive;
using Discord.Commands;
using Discord.WebSocket;

using DygBot.Addons;

namespace DygBot.Addons.EmoteActioner
{
    public class ActionerMessageCallback : IExtendedReactionCallback
    {
        public SocketCommandContext Context { get; }
        public ITextChannel TextChannel { get; }
        public BaseSocketClient Client { get; }
        public ulong GuildId { get; }
        public ulong ChannelId { get; }
        public ExtendedInteractiveService ExtendedInteractive { get; private set; }
        public IUserMessage Message { get; private set; }

        public RunMode RunMode => RunMode.Async;
        public ICriterion<SocketReaction> Criterion { get; }
        public TimeSpan? Timeout => _actioner.Options.Timeout;

        private readonly ActionerMessage _actioner;


        public ActionerMessageCallback(ExtendedInteractiveService interactive,
            SocketCommandContext sourceContext,
            ActionerMessage actioner,
            ICriterion<SocketReaction> criterion = null)
        {
            ExtendedInteractive = interactive;
            Context = sourceContext;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            _actioner = actioner;
            if (_actioner.EmoteActions == null || _actioner.EmoteActions.Count == 0)
                throw new ArgumentException("List of actions cannot be empty or null");
        }

        public ActionerMessageCallback(ExtendedInteractiveService interactive,
            BaseSocketClient client,
            ulong guildId,
            ulong channelId,
            ActionerMessage actioner,
            ICriterion<SocketReaction> criterion = null)
        {
            ExtendedInteractive = interactive;
            Context = null;
            Client = client;
            GuildId = guildId;
            ChannelId = channelId;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            _actioner = actioner;
            if (_actioner.EmoteActions == null || _actioner.EmoteActions.Count == 0)
                throw new ArgumentException("List of actions cannot be empty or null");
        }

        public ActionerMessageCallback(ExtendedInteractiveService interactive,
            BaseSocketClient client,
            ITextChannel textChannel,
            ActionerMessage actioner,
            ICriterion<SocketReaction> criterion = null)
        {
            ExtendedInteractive = interactive;
            Context = null;
            Client = client;
            TextChannel = textChannel;
            Criterion = criterion ?? new EmptyCriterion<SocketReaction>();
            _actioner = actioner;
            if (_actioner.EmoteActions == null || _actioner.EmoteActions.Count == 0)
                throw new ArgumentException("List of actions cannot be empty or null");
        }

        public async Task DisplayAsync()
        {
            if (Context != null)
                Message = await Context.Channel.SendMessageAsync(_actioner.Content, embed: _actioner.Embed).ConfigureAwait(false);
            else if (TextChannel != null)
                Message = await TextChannel.SendMessageAsync(_actioner.Content, embed: _actioner.Embed).ConfigureAwait(false);
            else
                Message = await Client.GetGuild(GuildId).GetTextChannel(ChannelId).SendMessageAsync(_actioner.Content, embed: _actioner.Embed).ConfigureAwait(false);

            ExtendedInteractive.AddReactionCallback(Message, this);
            ExtendedInteractive.AddReactionRemovedCallback(Message, this);

            _ = Task.Run(async () =>
            {
                foreach (var emoteAction in _actioner.EmoteActions)
                {
                    await Message.AddReactionAsync(emoteAction.Emote);
                }
            });

            if (Timeout.HasValue && Timeout.Value != null)
            {
                _ = Task.Delay(Timeout.Value).ContinueWith(_ =>
                {
                    ExtendedInteractive.RemoveReactionCallback(Message);
                    _ = Message.DeleteAsync();
                });
            }
        }

        public async Task<bool> HandleCallbackAsync(SocketReaction reaction)
        {
            var emote = reaction.Emote;

            foreach (var emoteAction in _actioner.EmoteActions)
            {
                if (emote.Equals(emoteAction.Emote))
                {
                    return await emoteAction.Actions.Added?.Invoke(reaction.UserId);
                }
            }

            return false;
        }

        public async Task<bool> HandleRemovedCallbackAsync(SocketReaction reaction)
        {
            var emote = reaction.Emote;

            foreach (var emoteAction in _actioner.EmoteActions)
            {
                if (emote.Equals(emoteAction.Emote))
                {
                    return await emoteAction.Actions.Removed?.Invoke(reaction.UserId);
                }
            }

            return false;
        }
    }
}
