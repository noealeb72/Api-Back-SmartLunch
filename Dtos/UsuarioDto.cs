using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using smartlunch_api.Utils;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// DTO para listado de usuarios con paginación
    /// </summary>
    public class UsuarioListadoDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int Legajo { get; set; }
        public int Dni { get; set; }
        public string Cuil { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }

        public int? PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int? CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public int? ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int? Plannutricional_id { get; set; }
        public string PlanNutricionalNombre { get; set; }

        public DateTime? FechaIngreso { get; set; }

        public int Pedidos { get; set; }
        public int Bonificaciones { get; set; }
        public int BonificacionesInvitado { get; set; }

        public bool Estado { get; set; }
        public string Username { get; set; }
        /// <summary>Fecha de creación del usuario (para ordenar: más recientes primero, admin siempre primero).</summary>
        public DateTime? Createdate { get; set; }
    }

    /// <summary>
    /// DTO para detalle completo de un usuario
    /// </summary>
    public class UsuarioDetalleDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int Legajo { get; set; }
        public int Dni { get; set; }
        public string Cuil { get; set; }

        public string Domicilio { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public string Contrato { get; set; }

        public int? Plannutricional_id { get; set; }
        public string PlanNutricionalNombre { get; set; }

        public int? PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int? CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public int? ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int BonificacionesInvitado { get; set; }
        public int Pedidos { get; set; }
        public int Bonificaciones { get; set; }

        /// <summary>Porcentaje de descuento/bonificación de la jerarquía (para web/tótem).</summary>
        public decimal Descuento { get; set; }

        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Foto { get; set; }

        //public string LlaveAccesoNum { get; set; }
        //public string OrigenDatos { get; set; }
        //public DateTime? FechaUltimaSincronizacion { get; set; }
        public string Username { get; set; }
        public bool Activo { get; set; }
    }

    public class UsuarioBaseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int Legajo { get; set; }
        public int Dni { get; set; }
        //public string Cuil { get; set; }        

        public int? Plannutricional_id { get; set; }
        public string PlanNutricionalNombre { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }

        public int JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int BonificacionesInvitado { get; set; }
        public int Pedidos { get; set; }
        public int Bonificaciones { get; set; }

        public int BonificacionesAplicadas { get; set; }
        public decimal Descuento { get; set; }        
        public bool Activo { get; set; }
    }

    /// <summary>
    /// DTO para crear un nuevo usuario
    /// </summary>
    public class UsuarioCreateDto : IValidatableObject
    {
        /// <summary>
        /// Nombre del usuario
        /// </summary>
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        /// <summary>
        /// Apellido del usuario
        /// </summary>
        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 100 caracteres")]
        public string Apellido { get; set; }

        /// <summary>
        /// Número de legajo del empleado
        /// </summary>
        [Required(ErrorMessage = "El legajo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El legajo debe ser mayor a 0")]
        public int Legajo { get; set; }

        /// <summary>
        /// Documento Nacional de Identidad
        /// </summary>
        [Required(ErrorMessage = "El DNI es obligatorio")]
        [Range(1000000, 99999999, ErrorMessage = "El DNI debe ser un número válido")]
        public int Dni { get; set; }

        /// <summary>
        /// CUIL del usuario
        /// </summary>
        [StringLength(20, ErrorMessage = "El CUIL no puede exceder 20 caracteres")]
        public string Cuil { get; set; }

        public string Domicilio { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public string Contrato { get; set; }

        public int? Plannutricional_id { get; set; }
        public int? PlantaId { get; set; }
        public int? CentroCostoId { get; set; }
        public int? ProyectoId { get; set; }
        public int? JerarquiaId { get; set; }

        public int BonificacionesInvitado { get; set; }

        public int Bonificaciones { get; set; }

        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Foto { get; set; }

        /// <summary>
        /// Username para crear el login (opcional)
        /// </summary>
        [StringLength(50, ErrorMessage = "El username no puede exceder 50 caracteres")]
        public string Username { get; set; }

        /// <summary>
        /// Password para crear el login (opcional, debe venir junto con Username)
        /// </summary>
        [StringLength(100, MinimumLength = 6, ErrorMessage = "La contraseña debe tener entre 6 y 100 caracteres")]
        public string Password { get; set; }

        //public string LlaveAccesoNum { get; set; }
        public string OrigenDatos { get; set; }
        //public DateTime? FechaUltimaSincronizacion { get; set; }

        /// <summary>
        /// Validaciones complejas que requieren lógica de negocio
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var resultados = new List<ValidationResult>();

            // Validar CUIL si se proporciona
            if (!string.IsNullOrWhiteSpace(Cuil))
            {
                if (!CuilValidator.EsValido(Cuil))
                {
                    resultados.Add(new ValidationResult(
                        "El CUIL no es válido. Debe tener 11 dígitos y un dígito verificador correcto.",
                        new[] { nameof(Cuil) }
                    ));
                }
            }

            // Validar Email si se proporciona
            if (!string.IsNullOrWhiteSpace(Email))
            {
                var emailAttr = new EmailAddressAttribute();
                if (!emailAttr.IsValid(Email))
                {
                    resultados.Add(new ValidationResult(
                        "El formato del email no es válido.",
                        new[] { nameof(Email) }
                    ));
                }
            }

            // Validar que si se proporciona Username, también se proporcione Password y viceversa
            bool tieneUsername = !string.IsNullOrWhiteSpace(Username);
            bool tienePassword = !string.IsNullOrWhiteSpace(Password);

            if (tieneUsername && !tienePassword)
            {
                resultados.Add(new ValidationResult(
                    "Si se proporciona Username, también se debe proporcionar Password.",
                    new[] { nameof(Password) }
                ));
            }

            if (tienePassword && !tieneUsername)
            {
                resultados.Add(new ValidationResult(
                    "Si se proporciona Password, también se debe proporcionar Username.",
                    new[] { nameof(Username) }
                ));
            }

            return resultados;
        }
    }

    /// <summary>
    /// DTO para actualizar un usuario existente
    /// </summary>
    public class UsuarioUpdateDto : IValidatableObject
    {
        /// <summary>
        /// ID del usuario a actualizar
        /// </summary>
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        /// <summary>
        /// Nombre del usuario
        /// </summary>
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 100 caracteres")]
        public string Nombre { get; set; }

        /// <summary>
        /// Apellido del usuario
        /// </summary>
        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "El apellido debe tener entre 2 y 100 caracteres")]
        public string Apellido { get; set; }

        /// <summary>
        /// Número de legajo del empleado
        /// </summary>
        [Required(ErrorMessage = "El legajo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El legajo debe ser mayor a 0")]
        public int Legajo { get; set; }

        /// <summary>
        /// Documento Nacional de Identidad
        /// </summary>
        [Required(ErrorMessage = "El DNI es obligatorio")]
        [Range(1000000, 99999999, ErrorMessage = "El DNI debe ser un número válido")]
        public int Dni { get; set; }

        /// <summary>
        /// CUIL del usuario
        /// </summary>
        [StringLength(20, ErrorMessage = "El CUIL no puede exceder 20 caracteres")]
        public string Cuil { get; set; }

        public string Domicilio { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public string Contrato { get; set; }

        public int? Plannutricional_id { get; set; }
        public int? PlantaId { get; set; }
        public int? CentroCostoId { get; set; }
        public int? ProyectoId { get; set; }
        public int? JerarquiaId { get; set; }

        public int BonificacionesInvitado { get; set; }

        public int Bonificaciones { get; set; }

        /// <summary>
        /// Email del usuario
        /// </summary>
        [EmailAddress(ErrorMessage = "El formato del email no es válido")]
        [StringLength(200, ErrorMessage = "El email no puede exceder 200 caracteres")]
        public string Email { get; set; }

        /// <summary>
        /// Teléfono del usuario
        /// </summary>
        [StringLength(50, ErrorMessage = "El teléfono no puede exceder 50 caracteres")]
        public string Telefono { get; set; }

        /// <summary>
        /// Foto del usuario (ruta o base64)
        /// </summary>
        public string Foto { get; set; }

        /// <summary>
        /// Llave de acceso (tarjeta/QR) del usuario
        /// </summary>
        [StringLength(100, ErrorMessage = "La llave de acceso no puede exceder 100 caracteres")]
        public string LlaveAccesoNum { get; set; }

        /// <summary>
        /// Origen de los datos: "base_datos" | "smarttime" | "biostar"
        /// </summary>
        [StringLength(50, ErrorMessage = "El origen de datos no puede exceder 50 caracteres")]
        public string OrigenDatos { get; set; }

        /// <summary>
        /// Fecha de última sincronización con sistema externo
        /// </summary>
        public DateTime? FechaUltimaSincronizacion { get; set; }

        /// <summary>
        /// Validaciones complejas que requieren lógica de negocio
        /// </summary>
        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            var resultados = new List<ValidationResult>();

            // Validar CUIL si se proporciona
            if (!string.IsNullOrWhiteSpace(Cuil))
            {
                if (!CuilValidator.EsValido(Cuil))
                {
                    resultados.Add(new ValidationResult(
                        "El CUIL no es válido. Debe tener 11 dígitos y un dígito verificador correcto.",
                        new[] { nameof(Cuil) }
                    ));
                }
            }

            return resultados;
        }
    }

    /// <summary>
    /// DTO para acciones de eliminar o activar un usuario
    /// </summary>
    public class UsuarioAccionDto
    {
        /// <summary>
        /// ID del usuario sobre el que se realizará la acción
        /// </summary>
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    /// <summary>
    /// DTO simplificado para búsqueda rápida de usuarios (solo legajo y nombre)
    /// </summary>
    public class UsuarioBusquedaSimpleDto
    {
        public int Legajo { get; set; }
        public string Nombre { get; set; }
    }

    // ===== Para impresión =====
    public class UsuarioImpresionRequestDto
    {
        public bool IncluirNombre { get; set; }
        public bool IncluirApellido { get; set; }
        public bool IncluirLegajo { get; set; }
        public bool IncluirDni { get; set; }
        public bool IncluirEmail { get; set; }
        public bool IncluirTelefono { get; set; }
        public bool IncluirPlanta { get; set; }
        public bool IncluirCentroCosto { get; set; }
        public bool IncluirProyecto { get; set; }
        public bool IncluirJerarquia { get; set; }
        public bool IncluirPlanNutricional { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
        public int? PlantaId { get; set; }
        public int? CentroCostoId { get; set; }
        public int? ProyectoId { get; set; }
    }

    public class UsuarioImpresionDto
    {
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int? Legajo { get; set; }
        public int? Dni { get; set; }
        public string Email { get; set; }
        public string Telefono { get; set; }
        public string Planta { get; set; }
        public string CentroCosto { get; set; }
        public string Proyecto { get; set; }
        public string Jerarquia { get; set; }
        public string PlanNutricional { get; set; }
        public string Estado { get; set; }
    }
}
