using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace smartlunch_api.Models
{
    [Table("sl_datosFichada")] // Nombre exacto de la tabla en la base de datos
    public class sl_datosfichada
    {
        [Key] // Clave primaria
        public int Id { get; set; }

        [Required] // Obligatorio
        public int Legajo { get; set; }


        [StringLength(50)] // Tamaño máximo 50
        public string IdLegajoFichada { get; set; }


        [StringLength(100)] // Tamaño máximo 100
        public string LlaveAcceso { get; set; }

        public DateTime? UltimaFichada { get; set; } // Campo opcional


        public int CantidadFichada { get; set; }


    }
}
