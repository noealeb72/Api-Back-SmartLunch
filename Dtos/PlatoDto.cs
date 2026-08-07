using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    // Dto para listar
    public class PlatoListadoDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public int? Plannutricional_id { get; set; }
        public string Plannutricional_nombre { get; set; }
        public decimal? Costo { get; set; }
        public decimal? CostoProveedor { get; set; }
        public string Ingredientes { get; set; }
        public string Foto { get; set; }
        public bool Deletemark { get; set; }
        public DateTime? Createdate { get; set; }
    }

    /// <summary>
    /// DTO simplificado para búsqueda rápida (solo código y nombre)
    /// </summary>
    public class PlatoBusquedaSimpleDto
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
    }

    public class PlatoDetalleDto
    {
        public int Id { get; set; }
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public string Ingredientes { get; set; }
        public string Presentacion { get; set; }
        public int Plannutricional_id { get; set; }
        public string Plannutricional_nombre { get; set; }
        public decimal Costo { get; set; }
        public decimal CostoProveedor { get; set; }
        public string Foto { get; set; }
    }

    public class PlatoCreateDto
    {
        [Required(ErrorMessage = "El código es obligatorio")]
        [StringLength(50, MinimumLength = 1, ErrorMessage = "El código debe tener entre 1 y 50 caracteres")]
        public string Codigo { get; set; }

        [Required(ErrorMessage = "La descripción es obligatoria")]
        [StringLength(200, MinimumLength = 2, ErrorMessage = "La descripción debe tener entre 2 y 200 caracteres")]
        public string Descripcion { get; set; }

        [StringLength(1000, ErrorMessage = "Los ingredientes no pueden exceder 1000 caracteres")]
        public string Ingredientes { get; set; }

        [StringLength(500, ErrorMessage = "La presentación no puede exceder 500 caracteres")]
        public string Presentacion { get; set; }

        [Required(ErrorMessage = "El plan nutricional es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de plan nutricional debe ser mayor a 0")]
        public int? Plannutricional_id { get; set; }

        [Required(ErrorMessage = "El costo es obligatorio")]
        [Range(0, double.MaxValue, ErrorMessage = "El costo debe ser mayor o igual a 0")]
        public decimal Costo { get; set; }

        [Required(ErrorMessage = "El costo de proveedor es obligatorio")]
        [Range(0, double.MaxValue, ErrorMessage = "El costo de proveedor debe ser mayor o igual a 0")]
        public decimal CostoProveedor { get; set; }

        [StringLength(9000, ErrorMessage = "La ruta de la foto no puede exceder 9000 caracteres")]
        public string Foto { get; set; }
    }

    public class PlatoUpdateDto : PlatoCreateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        public bool EliminarFoto { get; set; }
    }

    public class PlatoAccionDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    public class EliminarFotoDto
    {
        [Required(ErrorMessage = "La ruta de la imagen es obligatoria")]
        public string Ruta { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class PlatoImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirCodigo { get; set; }
        public bool IncluirDescripcion { get; set; }
        public bool IncluirPlanNutricional { get; set; }
        public bool IncluirImporte { get; set; }
        public bool IncluirPresentacion { get; set; }
        public bool IncluirIngredientes { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public int? PlanNutricionalId { get; set; }
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class PlatoImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Codigo { get; set; }
        public string Descripcion { get; set; }
        public string PlanNutricional { get; set; }
        public decimal? Importe { get; set; }
        public string Presentacion { get; set; }
        public string Ingredientes { get; set; }
        public string Estado { get; set; }
    }
}
