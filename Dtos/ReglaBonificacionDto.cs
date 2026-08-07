// Dtos/ReglaBonificacionDto.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// DTO para listar reglas de bonificación (grilla)
    /// </summary>
    public class ReglaBonificacionListadoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int Prioridad { get; set; }

        /// <summary>Vacío = "todos" (no filtra por turno).</summary>
        public List<int> TurnoIds { get; set; } = new List<int>();
        public List<string> TurnoNombres { get; set; } = new List<string>();

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int? PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int? PlannutricionalId { get; set; }
        public string PlannutricionalNombre { get; set; }

        /// <summary>Vacío = "todos" (no filtra por producto).</summary>
        public List<int> PlatoIds { get; set; } = new List<int>();
        public List<string> PlatoNombres { get; set; } = new List<string>();

        public int? PosicionPedido { get; set; }
        public bool? EsInvitado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public string TipoEfecto { get; set; }
        public decimal? ValorEfecto { get; set; }

        public bool Deletemark { get; set; }
    }

    /// <summary>
    /// DTO de detalle de una regla (ver/editar)
    /// </summary>
    public class ReglaBonificacionDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int Prioridad { get; set; }

        public List<int> TurnoIds { get; set; } = new List<int>();
        public List<string> TurnoNombres { get; set; } = new List<string>();

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int? PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int? PlannutricionalId { get; set; }
        public string PlannutricionalNombre { get; set; }

        public List<int> PlatoIds { get; set; } = new List<int>();
        public List<string> PlatoNombres { get; set; } = new List<string>();

        public int? PosicionPedido { get; set; }
        public bool? EsInvitado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        public string TipoEfecto { get; set; }
        public decimal? ValorEfecto { get; set; }
    }

    /// <summary>
    /// DTO para crear una regla de bonificación
    /// </summary>
    public class ReglaBonificacionCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres")]
        public string Nombre { get; set; }

        public int Prioridad { get; set; }

        /// <summary>Vacío o null = "todos" (no filtra por turno).</summary>
        public List<int> TurnoIds { get; set; }

        public int? JerarquiaId { get; set; }
        public int? PlantaId { get; set; }
        public int? PlannutricionalId { get; set; }

        /// <summary>Vacío o null = "todos" (no filtra por producto).</summary>
        public List<int> PlatoIds { get; set; }

        /// <summary>null=todos, 1=primero, 2=segundo, 3=tercero en adelante.</summary>
        [Range(1, 3, ErrorMessage = "La posición del pedido debe ser 1 (primero), 2 (segundo) o 3 (tercero en adelante)")]
        public int? PosicionPedido { get; set; }

        public bool? EsInvitado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        /// <summary>"Porcentaje" | "MontoFijo" | "CostoCero"</summary>
        [Required(ErrorMessage = "El tipo de efecto es obligatorio")]
        public string TipoEfecto { get; set; }

        public decimal? ValorEfecto { get; set; }
    }

    /// <summary>
    /// DTO para actualizar una regla de bonificación
    /// </summary>
    public class ReglaBonificacionUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(150, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 150 caracteres")]
        public string Nombre { get; set; }

        public int Prioridad { get; set; }

        public List<int> TurnoIds { get; set; }
        public int? JerarquiaId { get; set; }
        public int? PlantaId { get; set; }
        public int? PlannutricionalId { get; set; }
        public List<int> PlatoIds { get; set; }

        [Range(1, 3, ErrorMessage = "La posición del pedido debe ser 1 (primero), 2 (segundo) o 3 (tercero en adelante)")]
        public int? PosicionPedido { get; set; }

        public bool? EsInvitado { get; set; }
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }

        [Required(ErrorMessage = "El tipo de efecto es obligatorio")]
        public string TipoEfecto { get; set; }

        public decimal? ValorEfecto { get; set; }
    }
}
