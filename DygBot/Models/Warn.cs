using System;
using System.ComponentModel.DataAnnotations;

namespace DygBot.Models
{
    public class Warn
    {
        [Key]
        public int Id { get; set; }
        public ulong UserId { get; set; }
        public ulong GuildId { get; set; }
        public DateTime WarnExpiration { get; set; }
        public bool Expired { get; set; } = false;
        public string Reason { get; set; }
        public ulong WhoWarned { get; set; }
    }
}
