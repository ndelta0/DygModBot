using System;
using System.ComponentModel.DataAnnotations;

namespace DygBot.Models
{
    public class Ban
    {
        [Key]
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public DateTime BanEnd { get; set; }
        public bool Finished { get; set; } = false;
        public string Reason { get; set; }
        public ulong WhoBanned { get; set; }
    }
}
