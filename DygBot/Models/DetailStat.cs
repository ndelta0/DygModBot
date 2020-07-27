using System;
using System.ComponentModel.DataAnnotations;

namespace DygBot.Models
{
    public class DetailStat
    {
        [Key]
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public ulong GuildId { get; set; }
        public int Members { get; set; }
        public int Online { get; set; }
        public int Bans { get; set; }
    }
}
