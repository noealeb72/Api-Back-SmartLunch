using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_fichada")]
    public class sl_fichada
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int identificador_usuario { get; set; }

        public int? turno_id { get; set; }

        [Required]
        public DateTime fecha_fichada { get; set; }

        [Required]
        public int id_dispositivo { get; set; }

        [Required]
        public DateTime createdate { get; set; }

        //campos del response
        public long event_id { get; set; }         
        public long event_index { get; set; }   
        public long device_ext_id { get; set; } 
        [StringLength(150)]
        public string device_name { get; set; } 
        public int event_code { get; set; }

    }
}
