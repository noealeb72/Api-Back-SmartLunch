using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    

    // ===== Listado (para grilla) =====
    public class TurnoListadoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }

        public TimeSpan? HoraDesde { get; set; }
        public TimeSpan? HoraHasta { get; set; }

        public bool Activo { get; set; }
    }

    // ===== Detalle =====
    public class TurnoDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }

        public TimeSpan? HoraDesde { get; set; }
        public TimeSpan? HoraHasta { get; set; }

        public bool Activo { get; set; }

        public DateTime? CreateDate { get; set; }
        public string CreateUser { get; set; }
        public DateTime? UpdateDate { get; set; }
        public string UpdateUser { get; set; }
    }

    // ===== Crear =====
    public class TurnoCreateDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        public TimeSpan? HoraDesde { get; set; }
        public TimeSpan? HoraHasta { get; set; }
    }

    // ===== Actualizar =====
    public class TurnoUpdateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        public TimeSpan? HoraDesde { get; set; }
        public TimeSpan? HoraHasta { get; set; }
    }

    // ===== Acciones (eliminar / activar) =====
    public class TurnoAccionDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    // ===== Para combos simples =====
    public class TurnoComboDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
    }

    // ===== Para impresión =====
    public class TurnoImpresionRequestDto
    {
        public bool IncluirNombre { get; set; }
        public bool IncluirHoraDesde { get; set; }
        public bool IncluirHoraHasta { get; set; }
        public bool IncluirEstado { get; set; }
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class TurnoImpresionDto
    {
        public string Nombre { get; set; }
        public string HoraDesde { get; set; }
        public string HoraHasta { get; set; }
        public string Estado { get; set; }
    }
}
