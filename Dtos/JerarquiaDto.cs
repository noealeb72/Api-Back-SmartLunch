// Dtos/JerarquiaDto.cs
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// DTO para listar jerarquías (grilla / combos)
    /// </summary>
    public class JerarquiaListadoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public decimal Bonificacion { get; set; }
        public bool Deletemark { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// DTO de detalle de jerarquía (ver/editar)
    /// </summary>
    public class JerarquiaDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public decimal Bonificacion { get; set; }
        public bool IsDefault { get; set; }
    }

    /// <summary>
    /// DTO para crear una jerarquía
    /// </summary>
    public class JerarquiaCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }

        [Required(ErrorMessage = "La bonificación es obligatoria")]
        [Range(0, 100, ErrorMessage = "La bonificación debe estar entre 0 y 100")]
        public decimal Bonificacion { get; set; }
    }

    /// <summary>
    /// DTO para actualizar una jerarquía
    /// </summary>
    public class JerarquiaUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }

        [Required(ErrorMessage = "La bonificación es obligatoria")]
        [Range(0, 100, ErrorMessage = "La bonificación debe estar entre 0 y 100")]
        public decimal Bonificacion { get; set; }
    }

    /// <summary>
    /// DTO simple para acciones por Id (eliminar / activar)
    /// </summary>
    public class JerarquiaAccionDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class JerarquiaImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirNombre { get; set; }
        public bool IncluirDescripcion { get; set; }
        public bool IncluirBonificacion { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class JerarquiaImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public decimal? Bonificacion { get; set; }
        public string Estado { get; set; } // "Activo" o "Inactivo" basado en deletemark
    }
}
