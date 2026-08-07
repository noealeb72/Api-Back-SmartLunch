using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    // ⚠️ Si ya tenés PagedResultDto<T> NO lo vuelvas a definir acá.

    // ===== Listado (para grilla) =====
    public class ProyectoListadoDto
    {
        public int Id { get; set; }

        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public bool Activo { get; set; }
        public bool IsDefault { get; set; }
    }

    // ===== Detalle =====
    public class ProyectoDetalleDto
    {
        public int Id { get; set; }

        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public bool Activo { get; set; }
        public bool IsDefault { get; set; }

        public DateTime? CreateDate { get; set; }
        public string CreateUser { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string UpdateUser { get; set; }
    }

    // ===== Crear =====
    public class ProyectoCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }

        [Required(ErrorMessage = "El ID de planta es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de planta debe ser mayor a 0")]
        public int PlantaId { get; set; }

        [Required(ErrorMessage = "El ID de centro de costo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de centro de costo debe ser mayor a 0")]
        public int CentroCostoId { get; set; }
    }

    // ===== Actualizar =====
    public class ProyectoUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }

        [Required(ErrorMessage = "El ID de planta es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de planta debe ser mayor a 0")]
        public int PlantaId { get; set; }

        [Required(ErrorMessage = "El ID de centro de costo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de centro de costo debe ser mayor a 0")]
        public int CentroCostoId { get; set; }
    }

    // ===== Acciones (eliminar / activar) =====
    public class ProyectoAccionDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    // ===== Para combos =====
    public class ProyectoComboDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class ProyectoImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirNombre { get; set; }
        public bool IncluirDescripcion { get; set; }
        public bool IncluirPlanta { get; set; }
        public bool IncluirCentroCosto { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public int? PlantaId { get; set; }
        public int? CentroCostoId { get; set; }
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class ProyectoImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Planta { get; set; }
        public string CentroCosto { get; set; }
        public string Estado { get; set; } // "Activo" o "Inactivo" basado en deletemark
    }

    /// <summary>Resultado de validación de cantidad de usuarios asociados (solo Admin y Gerencia).</summary>
    public class ProyectoValidacionDto
    {
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }
        public int CantidadUsuarios { get; set; }
        public bool PuedeDarDeBaja { get; set; }
        public string Mensaje { get; set; }
    }
}
