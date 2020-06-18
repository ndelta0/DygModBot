using Discord.Commands;
using System;
using System.Threading.Tasks;

namespace DygBot.TypeReaders
{
    internal class UriTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            try
            {
                var result = new Uri(input);
                return Task.FromResult(TypeReaderResult.FromSuccess(result));   // Return input
            }
            catch (UriFormatException ex)
            {
                return Task.FromResult(TypeReaderResult.FromError(ex));
            }
        }
    }
}
