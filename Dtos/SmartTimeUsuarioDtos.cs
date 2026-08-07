using System;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// Request para crear un usuario desde smarTime (integración).
    /// Domicilio, FechaIngreso y Cuil son opcionales; el resto es obligatorio.
    /// Los IDs de catálogo (planta, plan, centro, proyecto, jerarquía) se toman por defecto (is_default) en el servidor.
    /// </summary>
    public class SmartTimeUsuarioCrearDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(50, MinimumLength = 1)]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(50, MinimumLength = 1)]
        public string Apellido { get; set; }

        [Required(ErrorMessage = "El legajo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El legajo debe ser mayor a 0")]
        public int Legajo { get; set; }

        [Required(ErrorMessage = "El DNI es obligatorio")]
        [Range(1, 99999999, ErrorMessage = "El DNI debe ser un número válido")]
        public int Dni { get; set; }

        /// <summary>Opcional. Si se envía, debe ser válido (11 dígitos y dígito verificador correcto).</summary>
        [StringLength(20)]
        public string Cuil { get; set; }

        /// <summary>Opcional. Puede venir vacío o null.</summary>
        [StringLength(100)]
        public string Domicilio { get; set; }

        /// <summary>Opcional. Puede venir vacío o null.</summary>
        public DateTime? FechaIngreso { get; set; }
    }

    /// <summary>
    /// Respuesta al crear un usuario desde smarTime (usuario + login creados).
    /// </summary>
    public class SmartTimeUsuarioCreadoDto
    {
        public int Id { get; set; }
        public int Legajo { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        /// <summary>Username creado para el login (por defecto = legajo).</summary>
        public string Username { get; set; }
        /// <summary>Indica que se generó contraseña automática; el usuario debe cambiarla en el primer acceso.</summary>
        public bool RequiereCambioClave { get; set; }
    }

    /// <summary>
    /// Item del listado de usuarios smarTime (solo usuarios con origen_datos/createuser = smarTime).
    /// </summary>
    public class SmartTimeUsuarioListadoDto
    {
        public int Id { get; set; }
        public int Legajo { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int Dni { get; set; }
        public string Cuil { get; set; }
        public string Domicilio { get; set; }
        public DateTime? FechaIngreso { get; set; }
        public string Username { get; set; }
        public bool Activo { get; set; }
    }

    /// <summary>
    /// Request para editar un usuario smarTime por legajo. Solo campos editables; el legajo va en la URL.
    /// Activo es opcional: si se envía, actualiza sl_usuario.deletemark y sl_login.deletemark (true = activo, false = inactivo).
    /// </summary>
    public class SmartTimeUsuarioActualizarDto
    {
        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(50, MinimumLength = 1)]
        public string Nombre { get; set; }

        [Required(ErrorMessage = "El apellido es obligatorio")]
        [StringLength(50, MinimumLength = 1)]
        public string Apellido { get; set; }

        [Required(ErrorMessage = "El DNI es obligatorio")]
        [Range(1, 99999999)]
        public int Dni { get; set; }

        /// <summary>Opcional. Si se envía, debe ser válido (11 dígitos y dígito verificador correcto).</summary>
        [StringLength(20)]
        public string Cuil { get; set; }

        [StringLength(100)]
        public string Domicilio { get; set; }

        public DateTime? FechaIngreso { get; set; }

        /// <summary>Opcional. Si se envía: true = usuario y login activos (deletemark=false), false = usuario y login inactivos (deletemark=true).</summary>
        public bool? Activo { get; set; }
    }
}
