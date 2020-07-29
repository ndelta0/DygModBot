using Discord.Commands;
using Discord.WebSocket;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DygBot.TypeReaders
{
    public class IMessageTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var messageUrl = input;
            var messageSplit = messageUrl.Split('/');

            ulong guildId = ulong.Parse(messageSplit[4]);
            ulong channelId = ulong.Parse(messageSplit[5]);
            ulong messageId = ulong.Parse(messageSplit[6]);

            if (guildId != context.Guild.Id)
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Message is from different guild"));
            }

            var channel = context.Guild.GetTextChannelAsync(channelId).Result;
            if (channel == null)
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Message's channel does not exist"));
            }

            var message = channel.GetMessageAsync(messageId).Result;
            if (message == null)
            {
                return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Message does not exist"));
            }

            return Task.FromResult(TypeReaderResult.FromSuccess(message));
        }
    }
}
