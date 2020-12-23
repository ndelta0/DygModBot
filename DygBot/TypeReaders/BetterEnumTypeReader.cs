using System;
using System.Threading.Tasks;

using Discord.Commands;

namespace DygBot.TypeReaders
{
    public class BetterEnumTypeReader : TypeReader
    {
        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            throw new NotImplementedException();
        }
    }
}
