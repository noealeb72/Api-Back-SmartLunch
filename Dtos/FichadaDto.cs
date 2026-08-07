using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    // Para listados paginados
    public class FichadaListadoDto
    {
        public int Id { get; set; }
        public int IdentificadorUsuario { get; set; }
        public int? TurnoId { get; set; }
        public DateTime FechaFichada { get; set; }
        public int IdDispositivo { get; set; }
        public DateTime Createdate { get; set; }
    }

    // Detalle (por ahora igual al listado)
    public class FichadaDetalleDto
    {
        public int Id { get; set; }
        public int IdentificadorUsuario { get; set; }
        public int? TurnoId { get; set; }
        public DateTime FechaFichada { get; set; }
        public int IdDispositivo { get; set; }
        public DateTime Createdate { get; set; }
        //
        public int plannutricional_id { get; set; }
        public int planta_id { get; set; }
        public int centrodecosto_id { get; set; }
        public int proyecto_id { get; set; }
        public int jerarquia_id { get; set; }
        public int bonificaciones_invitado { get; set; }
        public int bonificaciones { get; set; }

    }

    // Crear fichada manualmente
    public class FichadaCreateDto
    {
        [Required(ErrorMessage = "El identificador de usuario es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El identificador de usuario debe ser mayor a 0")]
        public int IdentificadorUsuario { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "El ID de turno debe ser mayor a 0")]
        public int? TurnoId { get; set; }

        public DateTime? FechaFichada { get; set; }   // si viene null uso DateTime.Now

        [Required(ErrorMessage = "El ID de dispositivo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de dispositivo debe ser mayor a 0")]
        public int IdDispositivo { get; set; }
    }
}
