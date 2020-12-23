using System;

using Reddit.Controllers;

namespace DygBot.Models
{
    public class SubredditSource
    {
        public Gender Gender { get; set; }
        public string SubredditName { get; set; }
        public Predicate<Post> PostPredicate { get; set; }
    }

    [Flags]
    public enum Gender
    {
        None = 0,
        Female = 1 << 0,
        Male = 1 << 1,
        Other = 1 << 2
    }
}
