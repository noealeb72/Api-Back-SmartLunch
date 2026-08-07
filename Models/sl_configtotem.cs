using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_configtotem")]
    public class sl_configtotem
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(100)]
        public string device_id { get; set; }

        // Yo asumo que ya los pasaste a BIT. Si siguen siendo nvarchar(10),
        // cambiá estos bool a string.
        [Required]
        public bool biostar_modo { get; set; }

        [Required]
        public int biostar_interval_segundos { get; set; }

        [Required]
        public bool smarttime_modo { get; set; }

        [Required]
        public DateTime created_at { get; set; }

        public DateTime? updated_at { get; set; }

        [Required]
        public bool deletemark { get; set; }

        public sl_configtotem()
        {
            created_at = DateTime.Now;
            deletemark = false;
        }
    }
}
