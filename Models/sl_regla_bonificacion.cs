using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    /// <summary>
    /// Regla de bonificación configurable: define condiciones opcionales (todas en AND,
    /// una condición nula significa "no filtra por esto") y un efecto a aplicar sobre el
    /// costo del plato cuando se crea una comanda. Administrada por Admin/Gerencia desde
    /// el front (pantalla "Reglas de Bonificación"). Si ninguna regla activa matchea un
    /// pedido, se cobra el 100% (ver ServicioComanda.CrearConDescuento).
    /// </summary>
    [Table("sl_regla_bonificacion")]
    public class sl_regla_bonificacion
    {
        [Key]
        public int id { get; set; }

        [Required]
        [StringLength(150)]
        public string nombre { get; set; }

        [Required]
        public int prioridad { get; set; }

        // ===== Condiciones (todas opcionales; null/vacío = "todos") =====
        /// <summary>Turnos a los que aplica la regla. Colección vacía = "todos" (no filtra por turno).</summary>
        public virtual ICollection<sl_turno> Turnos { get; set; }

        public int? jerarquia_id { get; set; }
        [ForeignKey("jerarquia_id")]
        public virtual sl_jerarquia Jerarquia { get; set; }

        public int? planta_id { get; set; }
        [ForeignKey("planta_id")]
        public virtual sl_planta Planta { get; set; }

        public int? plannutricional_id { get; set; }
        [ForeignKey("plannutricional_id")]
        public virtual sl_plannutricional Plannutricional { get; set; }

        /// <summary>Productos a los que aplica la regla. Colección vacía = "cualquiera" (no filtra por producto).</summary>
        public virtual ICollection<sl_plato> Platos { get; set; }

        /// <summary>Posición del pedido en el día: null=cualquiera, 1=primero, 2=segundo, 3=tercero en adelante.</summary>
        public int? posicion_pedido { get; set; }

        /// <summary>null=cualquiera, true=solo invitados, false=solo empleados.</summary>
        public bool? es_invitado { get; set; }

        public DateTime? fecha_desde { get; set; }
        public DateTime? fecha_hasta { get; set; }

        // ===== Efecto =====
        /// <summary>"Porcentaje" | "MontoFijo" | "CostoCero"</summary>
        [Required]
        [StringLength(20)]
        public string tipo_efecto { get; set; }

        /// <summary>Requerido salvo cuando tipo_efecto = "CostoCero".</summary>
        [Column(TypeName = "decimal")]
        public decimal? valor_efecto { get; set; }

        public DateTime? createdate { get; set; }

        [StringLength(50)]
        public string createuser { get; set; }

        public DateTime? updatedate { get; set; }

        [StringLength(50)]
        public string updateuser { get; set; }

        [Required]
        public bool deletemark { get; set; }

        public sl_regla_bonificacion()
        {
            deletemark = false;
            Turnos = new HashSet<sl_turno>();
            Platos = new HashSet<sl_plato>();
        }
    }
}
