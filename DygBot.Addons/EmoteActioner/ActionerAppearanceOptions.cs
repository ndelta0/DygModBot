using System;
using System.Collections.Generic;

using Discord;

namespace DygBot.Addons.EmoteActioner
{
    public class ActionerAppearanceOptions
    {
        public static ActionerAppearanceOptions Default = new ActionerAppearanceOptions();
        
        public TimeSpan? Timeout = null;
    }
}
