using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_plannutricional")]
    public class sl_plannutricional
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(50)]
        public string nombre { get; set; }

        [StringLength(150)]
        public string descripcion { get; set; }

        [Required]
        public bool deletemark { get; set; }

        /// <summary>
        /// Si es true, este registro se usa como valor por defecto (evita depender de IDs fijos en config si el default est� dado de baja).
        /// </summary>
        public bool is_default { get; set; }

        public DateTime? createdate { get; set; }

        [StringLength(50)]
        public string createuser { get; set; }

        public DateTime? updatedate { get; set; }

        [StringLength(50)]
        public string updateuser { get; set; }

        // Navegaci�n inversa (un plan puede estar en muchos platos)
        public virtual ICollection<sl_plato> platos { get; set; }

        public sl_plannutricional()
        {
            deletemark = false;
            platos = new HashSet<sl_plato>();
        }
    }
}
