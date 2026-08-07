using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_jerarquia")]
    public class sl_jerarquia
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(150)]
        public string nombre { get; set; }

  
        [StringLength(150)]
        public string descripcion { get; set; }

        [Required]
        public decimal bonificacion { get; set; }

        public DateTime? createdate { get; set; }

        [StringLength(50)]
        public string createuser { get; set; }

        public DateTime? updatedate { get; set; }

        [StringLength(50)]
        public string updateuser { get; set; }

        [Required]
        public bool deletemark { get; set; }

        /// <summary>
        /// Si es true, este registro se usa como valor por defecto (evita depender de IDs fijos en config si el default est� dado de baja).
        /// </summary>
        public bool is_default { get; set; }

        // ===== Navegaci�n inversa a usuarios (1 Jerarquia -> N usuarios) =====
        public virtual ICollection<sl_usuario> usuarios { get; set; }
        public virtual ICollection<sl_menudd> menuesDelDia { get; set; }
        public sl_jerarquia()
        {
            usuarios = new HashSet<sl_usuario>();
            menuesDelDia = new HashSet<sl_menudd>();
            deletemark = false;
        }
    }
}
