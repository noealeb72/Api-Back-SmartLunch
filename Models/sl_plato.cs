using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_plato")]
    public class sl_plato
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(150)]
        public string codigo { get; set; }

        [Required]
        [StringLength(150)]
        public string descripcion { get; set; }

        [Column(TypeName = "decimal")]
        public decimal costo { get; set; }

        /// <summary>
        /// Costo real que factura el proveedor por este plato (distinto de "costo",
        /// que es el precio de lista que ve el empleado antes de la bonificación).
        /// </summary>
        [Column(TypeName = "decimal")]
        public decimal costo_proveedor { get; set; }

        // --- FK �nica a plan nutricional ---
        public int plannutricional_id { get; set; }

        [ForeignKey("plannutricional_id")]
        public virtual sl_plannutricional plannutricional { get; set; }

        [StringLength(500)]
        public string presentacion { get; set; }

        public string ingredientes { get; set; }

        public bool deletemark { get; set; }

        public DateTime? createdate { get; set; }

        [StringLength(50)]
        public string createuser { get; set; }

        public DateTime? updatedate { get; set; }

        [StringLength(50)]
        public string updateuser { get; set; }

        [StringLength(500)]
        public string foto { get; set; }

        public virtual ICollection<sl_menudd> menuesDelDia { get; set; }

        public sl_plato()
        {
            menuesDelDia = new HashSet<sl_menudd>();
        }
    }
}
