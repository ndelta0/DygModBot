using Discord;
using Discord.Commands;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace DygBot.TypeReaders
{
    public class IEmoteTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            try
            {
                IEmote emote;
                if (Emote.TryParse(input, out Emote emoteTmp))
                    emote = emoteTmp;
                else
                    emote = new Emoji(input);
                if (emote != null)
                    return Task.FromResult(TypeReaderResult.FromSuccess(emote));
            }
            catch (Exception ex)
            {
                return Task.FromResult(TypeReaderResult.FromError(ex));
            }
            return Task.FromResult(TypeReaderResult.FromError(CommandError.ParseFailed, "Input could not be parsed into IEmote"));
        }
    }
}
