using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace DygBot.Models
{
    public class DailyStat
    {
        [Key]
        public int Id { get; set; }
        public DateTime DateTime { get; set; }
        public int UniqueSenders { get; set; }
    }
}
