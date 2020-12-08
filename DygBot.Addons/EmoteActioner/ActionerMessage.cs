using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Discord;

namespace DygBot.Addons.EmoteActioner
{
    public class ActionerMessage
    {
        /// <summary>
        /// Embed of the message. This may remain null.
        /// </summary>
        public Embed Embed { get; set; } = null;

        /// <summary>
        /// Content sets the content of the message, displayed above the embed. This may remain empty.
        /// </summary>
        public string Content { get; set; } = "";

        /// <summary>
        /// List of <see cref="EmoteAction"/> bound to the message
        /// </summary>
        public List<EmoteAction> EmoteActions = new List<EmoteAction>();

        public ActionerAppearanceOptions Options { get; set; } = ActionerAppearanceOptions.Default;
    }

    public struct EmoteAction
    {
        public IEmote Emote { get; set; }
        public ActionTuple Actions { get; set; }
    }

    public struct ActionTuple
    {
        public Func<Task<bool>> Added { get; set; }
        public Func<Task<bool>> Removed { get; set; }
    }
}
