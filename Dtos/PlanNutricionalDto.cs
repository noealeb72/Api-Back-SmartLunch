using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    // Para listado con paginado
    public class PlanNutricionalListadoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public DateTime? Createdate { get; set; }
        public bool Estado { get; set; }   // !deletemark
        public bool IsDefault { get; set; }
    }

    // Para detalle
    public class PlanNutricionalDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }

        public bool DeleteMark { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? Createdate { get; set; }
        public string Createuser { get; set; }
        public DateTime? Updatedate { get; set; }
        public string Updateuser { get; set; }
    }

    // Para crear
    public class PlanNutricionalCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }
    }

    // Para actualizar
    public class PlanNutricionalUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string Descripcion { get; set; }
    }

    // Para baja / activar
    public class PlanNutricionalIdDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class PlanNutricionalImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirNombre { get; set; }
        public bool IncluirDescripcion { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class PlanNutricionalImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; } // "Activo" o "Inactivo" basado en deletemark
    }

    /// <summary>Resultado de validación de cantidad de usuarios asociados (solo Admin y Gerencia).</summary>
    public class PlanNutricionalValidacionDto
    {
        public int PlanNutricionalId { get; set; }
        public string PlanNutricionalNombre { get; set; }
        public int CantidadUsuarios { get; set; }
        public bool PuedeDarDeBaja { get; set; }
        public string Mensaje { get; set; }
    }
}
