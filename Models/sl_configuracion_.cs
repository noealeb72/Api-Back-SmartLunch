namespace smartlunch_api.Models
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.ComponentModel.DataAnnotations.Schema;
    using System.Data.Entity.Spatial;

    public partial class sl_configuracion_
    {
        [Key]
        [StringLength(50)]
        public string parametro { get; set; }

        [Required]
        [StringLength(10)]
        public string valor { get; set; }
    }
}
