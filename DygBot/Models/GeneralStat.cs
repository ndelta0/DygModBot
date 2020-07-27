using System;
using System.ComponentModel.DataAnnotations;

namespace DygBot.Models
{
    public class GeneralStat
    {
        [Key]
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public int UniqueSenders { get; set; }
    }
}
