using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Discord.Addons.Interactive;
using Discord.WebSocket;

namespace DygBot.Addons
{
    public interface IExtendedReactionCallback : IReactionCallback
    {
        Task<bool> HandleRemovedCallbackAsync(SocketReaction reaction);
    }
}
