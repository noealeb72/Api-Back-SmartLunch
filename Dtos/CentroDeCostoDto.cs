using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    public class CentroDeCostoListadoDto
    {
        public int Id { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        public bool DeleteMark { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? Createdate { get; set; }
    }

    public class CentroDeCostoDetalleDto
    {
        public int Id { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public string Nombre { get; set; }
        public string TheDescripcion { get; set; }

        public bool DeleteMark { get; set; }
        public bool IsDefault { get; set; }

        public DateTime? Createdate { get; set; }
        public string Createuser { get; set; }
        public DateTime? Updatedate { get; set; }
        public string Updateuser { get; set; }
    }

    public class CentroDeCostoCreateDto
    {
        [Required(ErrorMessage = "El ID de planta es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de planta debe ser mayor a 0")]
        public int PlantaId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }
    }

    public class CentroDeCostoUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El ID de planta es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de planta debe ser mayor a 0")]
        public int PlantaId { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }
    }

    public class CentroDeCostoAccionDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class CentroDeCostoImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirPlanta { get; set; }
        public bool IncluirNombre { get; set; }
        public bool IncluirDescripcion { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class CentroDeCostoImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Planta { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; } // "Activo" o "Inactivo" basado en deletemark
    }

    /// <summary>Resultado de validación de cantidad de usuarios asociados (solo Admin y Gerencia).</summary>
    public class CentroDeCostoValidacionDto
    {
        public int CentroDeCostoId { get; set; }
        public string CentroDeCostoNombre { get; set; }
        public int CantidadUsuarios { get; set; }
        public bool PuedeDarDeBaja { get; set; }
        public string Mensaje { get; set; }
    }
}
