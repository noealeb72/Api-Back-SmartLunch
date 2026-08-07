using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace smartlunch_api.Models
{
    public class sl_tokenFichada
    {
        [Key]
        public int Id { get; set; }

        public DateTime? fecha { get; set; }
        public string token { get; set; }
    }
}