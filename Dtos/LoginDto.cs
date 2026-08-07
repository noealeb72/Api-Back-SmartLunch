using Newtonsoft.Json;
using smartlunch_api.Models.DTOs;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// Respuesta del login por usuario/contraseña.
    /// </summary>
    public class LoginResponseDto
    {
        /// <summary>
        /// Token JWT para autenticación en requests posteriores
        /// </summary>
        public string Token { get; set; }
        
        /// <summary>
        /// Información del usuario autenticado
        /// </summary>
        public UsuarioDto Usuario { get; set; }
    }

    /// <summary>
    /// Respuesta genérica para AuthenticateByLegajo, etc.
    /// </summary>
    public class AuthenticateResponse
    {
        /// <summary>
        /// Indica si la autenticación fue exitosa
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// Token JWT para autenticación en requests posteriores
        /// </summary>
        public string Token { get; set; }
        
        /// <summary>
        /// Información del usuario autenticado
        /// </summary>
        public UsuarioDto Usuario { get; set; }
        
        /// <summary>
        /// Origen de los datos: "base_datos" | "smarttime"
        /// </summary>
        public string Origen { get; set; }
        
        /// <summary>
        /// Mensaje descriptivo de la operación
        /// </summary>
        public string Mensaje { get; set; }
    }

    /// <summary>
    /// DTO para listado de logins con paginación
    /// </summary>
    public class LoginListadoDto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Username { get; set; }
        public bool Activo { get; set; }
        public bool DeleteMark { get; set; }
        public DateTime? LastLogin { get; set; }
        public string UsuarioNombreCompleto { get; set; }
    }

    /// <summary>
    /// DTO para detalle completo de un login
    /// </summary>
    public class LoginDetalleDto
    {
        public int Id { get; set; }
        public int UsuarioId { get; set; }
        public string Username { get; set; }
        public bool Estado { get; set; }
        public bool DeleteMark { get; set; }
        public DateTime? LastLogin { get; set; }
        public DateTime Createdate { get; set; }
        public string Createuser { get; set; }
        public DateTime? Updatedate { get; set; }
        public string Updateuser { get; set; }
        public string UsuarioNombreCompleto { get; set; }
        public int UsuarioLegajo { get; set; }
    }

   



    /// <summary>
    /// DTO para crear un nuevo login
    /// </summary>
    public class LoginCreateDto
    {
        /// <summary>
        /// ID del usuario al que pertenece el login
        /// </summary>
        [Required(ErrorMessage = "El ID de usuario es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de usuario debe ser mayor a 0")]
        public int UsuarioId { get; set; }

        /// <summary>
        /// Nombre de usuario (username) para el login
        /// </summary>
        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 100 caracteres")]
        public string Username { get; set; }

        /// <summary>
        /// Contraseña en texto plano (se hashea en el servicio)
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string Password { get; set; }
    }

    /// <summary>
    /// DTO para actualizar un login existente
    /// </summary>
    public class LoginUpdateDto
    {
        /// <summary>
        /// ID del login a actualizar
        /// </summary>
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        /// <summary>
        /// Nuevo nombre de usuario
        /// </summary>
        [StringLength(100, MinimumLength = 3, ErrorMessage = "El nombre de usuario debe tener entre 3 y 100 caracteres")]
        public string Username { get; set; }

        /// <summary>
        /// Estado activo/inactivo del login
        /// </summary>
        public bool Activo { get; set; }

        /// <summary>
        /// Nueva contraseña (opcional, solo si se desea cambiar)
        /// </summary>
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string NuevoPassword { get; set; }
    }

    /// <summary>
    /// DTO para acciones de eliminar o activar un login
    /// </summary>
    public class LoginAccionDto
    {
        /// <summary>
        /// ID del login sobre el que se realizará la acción
        /// </summary>
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    /// <summary>
    /// DTO para solicitud de login/autenticación.
    /// Acepta "Username"/"username" y "Password"/"password" en JSON.
    /// </summary>
    public class LoginRequestDto
    {
        /// <summary>
        /// Nombre de usuario
        /// </summary>
        [Required(ErrorMessage = "El nombre de usuario es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre de usuario no puede exceder 100 caracteres.")]
        [JsonProperty("Username")]
        public string Username { get; set; }

        /// <summary>
        /// Contraseña del usuario
        /// </summary>
        [Required(ErrorMessage = "La contraseña es obligatoria")]
        [StringLength(256, MinimumLength = 1, ErrorMessage = "La contraseña debe tener entre 1 y 256 caracteres.")]
        [JsonProperty("Password")]
        public string Password { get; set; }

    }

    /// <summary>
    /// DTO para resultado de autenticación
    /// </summary>
    public class LoginAuthResultDto
    {
        public bool Ok { get; set; }
        public string Mensaje { get; set; }
        public int UsuarioId { get; set; }
        public string Username { get; set; }

        public string Jerarquia { get; set; }
        public string NombreCompleto { get; set; }
        public bool Activo { get; set; }
        [JsonProperty("token")]
        public string Token { get; set; }

        /// <summary>
        /// Refresh token para renovar el JWT sin volver a enviar usuario/contraseña. El front lo guarda y lo envía a POST /api/login/Refresh.
        /// Serializado como "refreshToken" para compatibilidad con clientes que esperan camelCase; también acepta "RefreshToken".
        /// </summary>
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }

        /// <summary>
        /// Si es true, el usuario debe cambiar la contraseña antes de continuar (ej. clave por defecto).
        /// Valor de sl_login.must_change_password. El front debe mostrar el flujo de cambio de clave obligatorio.
        /// </summary>
        [JsonProperty(Order = 99)]
        public bool RequiereCambioClave { get; set; }
    }

    /// <summary>
    /// Request para POST /api/login/Refresh. Envía el refresh token recibido en el login.
    /// Acepta "RefreshToken" o "refreshToken" en el body.
    /// </summary>
    public class RefreshTokenRequestDto
    {
        [Required(ErrorMessage = "El RefreshToken es obligatorio")]
        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }

    /// <summary>
    /// Respuesta de POST /api/login/Refresh: nuevo JWT y opcionalmente nuevo RefreshToken (rotación).
    /// </summary>
    public class RefreshTokenResponseDto
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("refreshToken")]
        public string RefreshToken { get; set; }
    }

    /// <summary>
    /// DTO para autenticación por legajo (usado en tótem)
    /// </summary>
    public class AuthenticateByLegajoRequest
    {
        /// <summary>
        /// Número de legajo del empleado
        /// </summary>
        [Required(ErrorMessage = "El legajo es obligatorio")]
        [StringLength(20, MinimumLength = 1, ErrorMessage = "El legajo debe tener entre 1 y 20 caracteres")]
        public string Legajo { get; set; }
    }

    public class LoginCambiarClaveDto
    {
        /// <summary>Id del usuario (sl_usuario). Lo envía el front para identificar a quién se le cambia la clave.</summary>
        [Required(ErrorMessage = "El usuario_id es obligatorio")]
        public int UsuarioId { get; set; }

        /// <summary>Id del registro sl_login. Opcional si se envía UsuarioId: se resuelve por usuario_id.</summary>
        public int Id { get; set; }

        [Required(ErrorMessage = "La clave actual es obligatoria")]
        public string ClaveActual { get; set; }

        [Required(ErrorMessage = "La nueva clave es obligatoria")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La nueva contraseña debe tener entre 6 y 100 caracteres")]
        public string NuevaClave { get; set; }
    }
}
