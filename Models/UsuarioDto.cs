using System;

namespace smartlunch_api.Models.DTOs
{
    /// <summary>
    /// DTO para respuesta de autenticación (login, authenticateByLegajo, etc.)
    /// Contiene información básica del usuario con nombres de entidades relacionadas
    /// </summary>
    public class UsuarioDto
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public string apellido { get; set; }
        public int legajo { get; set; }
        public int dni { get; set; }
        public string cuil { get; set; }

        public int? plannutricional_id { get; set; }
        public string plannutricional_nombre { get; set; }

        public int? planta_id { get; set; }
        public string planta_nombre { get; set; }

        public int? centrodecosto_id { get; set; }
        public string centrodecosto_nombre { get; set; }

        public int? proyecto_id { get; set; }
        public string proyecto_nombre { get; set; }

        public int? jerarquia_id { get; set; }
        public string jerarquia_nombre { get; set; }

        public int? pedido { get; set; }
        public int? bonificaciones { get; set; }
        public int bonificaciones_invitado { get; set; }
        public string llave_acceso { get; set; }
    }
}


