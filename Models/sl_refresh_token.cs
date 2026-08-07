using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_refresh_token")]
    public class sl_refresh_token
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int login_id { get; set; }

        [Required]
        [StringLength(128)]
        public string token { get; set; }

        [Required]
        public DateTime expires_at { get; set; }

        public DateTime created_at { get; set; }

        [Required]
        public bool revoked { get; set; }

        [ForeignKey("login_id")]
        public virtual sl_login Login { get; set; }
    }
}
