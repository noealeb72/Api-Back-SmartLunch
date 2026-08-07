using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_login")]
    public class sl_login
    {
        [Key]
        public int id { get; set; }

        [Required]
        public int usuario_id { get; set; }

        [Required]
        [StringLength(50)]
        public string username { get; set; }

        [Required]
        public byte[] password_salt { get; set; }

        [Required]
        public byte[] password_hash { get; set; }

        // Null = login viejo, anterior a esta columna (ver PasswordUtils.IteracionesLegado)
        public int? password_iteraciones { get; set; }

        [Required]
        public bool activo { get; set; }

        public DateTime? last_login { get; set; }

        public DateTime? createdate { get; set; }

        [StringLength(50)]
        public string createuser { get; set; }

        public DateTime? updatedate { get; set; }

        [StringLength(50)]
        public string updateuser { get; set; }

        [Required]
        public bool deletemark { get; set; }

        /// <summary>
        /// Si es true, el usuario debe cambiar la contraseña antes de usar endpoints sensibles (ej. clave por defecto).
        /// </summary>
        [Column("must_change_password")]
        public bool debe_cambiar_clave { get; set; }

        //Navegación hacia usuario
        [ForeignKey("usuario_id")]
        public virtual sl_usuario Usuario { get; set; }
    }
}
