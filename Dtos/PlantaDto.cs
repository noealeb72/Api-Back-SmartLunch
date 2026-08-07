using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Models.DTOs
{
    // Para listar / ver detalle
    public class PlantaDto
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public string descripcion { get; set; }
    }

    // Para crear
    public class PlantaCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string descripcion { get; set; }
    }

    // Para actualizar
    public class PlantaUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string nombre { get; set; }

        [StringLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
        public string descripcion { get; set; }
    }

    // Para eliminar (si querés borrar por body)
    public class PlantaDeleteDto
    {
        public int id { get; set; }
    }
}

namespace smartlunch_api.Dtos
{
    // Para listado con paginación
    public class PlantaListadoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Deletemark { get; set; }
        public bool IsDefault { get; set; }
    }

    // Para detalle completo
    public class PlantaDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public bool Activo { get; set; }
        public bool IsDefault { get; set; }
        public DateTime? CreateDate { get; set; }
        public string CreateUser { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string UpdateUser { get; set; }
    }

    // Para acciones (eliminar / activar) - usando PascalCase para consistencia
    public class PlantaAccionDto
    {
        public int Id { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class PlantaImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirNombre { get; set; }
        public bool IncluirDescripcion { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class PlantaImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Estado { get; set; } // "Activo" o "Inactivo" basado en deletemark
    }

    /// <summary>Resultado de validación de cantidad de usuarios asociados a una planta (solo Admin y Gerencia).</summary>
    public class PlantaValidacionDto
    {
        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }
        public int CantidadUsuarios { get; set; }
        /// <summary>True si la planta puede darse de baja (0 usuarios activos asociados).</summary>
        public bool PuedeDarDeBaja { get; set; }
        public string Mensaje { get; set; }
    }
}