using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SosuBot.Database.Models
{
    public class OsuUser
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public long TelegramId { get; set; }

        public required long OsuUserId { get; set; }
        public required string OsuUsername { get; set; }
        public required string OsuMode { get; set; }
        public double? PPValue { get; set; }
    }
}
