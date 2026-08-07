using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_comanda")]
    public class sl_comanda
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int menudd_id { get; set; }

        [ForeignKey("menudd_id")]
        public virtual sl_menudd Menudd { get; set; }
        
        public int npedido { get; set; }

        [Required]
        public int usuario_id { get; set; }

        [Required]
        public int plato_id { get; set; }

        [Required]
        public int turno_id { get; set; }

        [Required]
        public DateTime fecha { get; set; }

        [Required]
        public decimal monto { get; set; }

        /// <summary>
        /// Snapshot del costo de proveedor del plato al momento del pedido
        /// (copiado de sl_plato.costo_proveedor al crear la comanda, para que
        /// no cambie retroactivamente si el costo del plato se actualiza después).
        /// </summary>
        [Required]
        public decimal costo_proveedor { get; set; }

        [Required]
        public bool bonificado { get; set; }

        [Required]
        public bool invitado { get; set; }

        public int? calificacion { get; set; }

        [Required]
        [StringLength(20)]
        public string estado { get; set; }

        [StringLength(200)]
        public string comentario { get; set; }

        [Required]
        public int planta_id { get; set; }

        [Required]
        public int centrodecosto_id { get; set; }

        [Required]
        public int proyecto_id { get; set; }

        [Required]
        public int jerarquia_id { get; set; }

        [Required]
        public bool deletemark { get; set; }

        // ===== Navegaci�n (FK) =====
        [ForeignKey("usuario_id")]
        public virtual sl_usuario Usuario { get; set; }

        [ForeignKey("plato_id")]
        public virtual sl_plato Plato { get; set; }

        [ForeignKey("turno_id")]
        public virtual sl_turno Turno { get; set; }

        [ForeignKey("planta_id")]
        public virtual sl_planta Planta { get; set; }

        [ForeignKey("centrodecosto_id")]
        public virtual sl_centrodecosto CentroDeCosto { get; set; }

        [ForeignKey("proyecto_id")]
        public virtual sl_proyecto Proyecto { get; set; }

        [ForeignKey("jerarquia_id")]
        public virtual sl_jerarquia Jerarquia { get; set; }
    }
}
