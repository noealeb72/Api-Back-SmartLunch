using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_usuario")]
    public class sl_usuario
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(50)]
        public string nombre { get; set; }

        [Required]
        [StringLength(50)]
        public string apellido { get; set; }

        public int legajo { get; set; }
        public int dni { get; set; }

        [StringLength(20)]
        public string cuil { get; set; }

        [StringLength(100)]
        public string domicilio { get; set; }

        public DateTime? fechaingreso { get; set; }

        [StringLength(20)]
        public string contrato { get; set; }

        // ======================= FKs y navegación =======================

        // --- Plan nutricional ---
        public int? plannutricional_id { get; set; }

        [ForeignKey("plannutricional_id")]
        public virtual sl_plannutricional plannutricional { get; set; }

        // --- Planta ---
        public int? planta_id { get; set; }

        [ForeignKey("planta_id")]
        public virtual sl_planta planta { get; set; }

        // --- Centro de costo ---
        public int? centrodecosto_id { get; set; }

        [ForeignKey("centrodecosto_id")]
        public virtual sl_centrodecosto centrodecosto { get; set; }

        // --- Proyecto ---
        public int? proyecto_id { get; set; }

        [ForeignKey("proyecto_id")]
        public virtual sl_proyecto proyecto { get; set; }

        // --- Jerarquía ---
        public int? jerarquia_id { get; set; }

        [ForeignKey("jerarquia_id")]
        public virtual sl_jerarquia jerarquia { get; set; }

        //public virtual ICollection<sl_login> sl_login { get; set; }

        // ===============================================================

        public int? bonificaciones_invitado { get; set; }

        public DateTime? createdate { get; set; }

        [StringLength(50)]
        public string createuser { get; set; }

        public DateTime? updatedate { get; set; }

        [StringLength(50)]
        public string updateuser { get; set; }

        [Required]
        public bool deletemark { get; set; }

        public int? pedidos { get; set; }
        public int? bonificaciones { get; set; }

        public string foto { get; set; }

        [StringLength(100)]
        public string email { get; set; }

        [StringLength(20)]
        public string telefono { get; set; }

        [StringLength(50)]
        //[Column("llave_acceso")]
        public string llave_acceso { get; set; }

        [StringLength(20)]
        public string origen_datos { get; set; }

        public DateTime? fecha_ultima_sincronizacion { get; set; }

        // ===== Navegación inversa a sl_login (1 usuario -> N logins) =====
        public virtual ICollection<sl_login> Logins { get; set; }

        public sl_usuario()
        {
            Logins = new HashSet<sl_login>();
            deletemark = false;
        }
    }
}
