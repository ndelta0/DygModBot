using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Discord.Commands;

namespace DygBot.TypeReaders
{
    public class CustomTimeSpanTypeReader : TypeReader
    {
        private static readonly Regex[] _regices =
        {
            new Regex("([0-9]+)y"),
            new Regex("([0-9]+)M"),
            new Regex("([0-9]+)w"),
            new Regex("([0-9]+)d"),
            new Regex("([0-9]+)h"),
            new Regex("([0-9]+)m"),
            new Regex("([0-9]+)s"),
        };

        public override Task<TypeReaderResult> ReadAsync(ICommandContext context, string input, IServiceProvider services)
        {
            var span = TimeSpan.Zero;

            span = span.Add(TimeSpan.FromDays(365 * ((_regices[0].Match(input).Groups.Count > 1) ? int.Parse(_regices[0].Match(input).Groups[1].Value) : 0)));
            span = span.Add(TimeSpan.FromDays(30 * ((_regices[1].Match(input).Groups.Count > 1) ? int.Parse(_regices[1].Match(input).Groups[1].Value) : 0)));
            span = span.Add(TimeSpan.FromDays(7 * ((_regices[2].Match(input).Groups.Count > 1) ? int.Parse(_regices[2].Match(input).Groups[1].Value) : 0)));
            span = span.Add(TimeSpan.FromDays((_regices[3].Match(input).Groups.Count > 1) ? int.Parse(_regices[3].Match(input).Groups[1].Value) : 0));
            span = span.Add(TimeSpan.FromHours((_regices[4].Match(input).Groups.Count > 1) ? int.Parse(_regices[4].Match(input).Groups[1].Value) : 0));
            span = span.Add(TimeSpan.FromMinutes((_regices[5].Match(input).Groups.Count > 1) ? int.Parse(_regices[5].Match(input).Groups[1].Value) : 0));
            span = span.Add(TimeSpan.FromSeconds((_regices[6].Match(input).Groups.Count > 1) ? int.Parse(_regices[6].Match(input).Groups[1].Value) : 0));

            if (span.Ticks == TimeSpan.Zero.Ticks)
                return Task.FromResult(TypeReaderResult.FromError(CommandError.Unsuccessful, "Input could not be parsed as a valid, non-zero TimeSpan"));
            return Task.FromResult(TypeReaderResult.FromSuccess(span));
        }
    }
}
