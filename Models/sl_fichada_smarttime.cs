using System;

namespace smartlunch_api.Models
{
    public class sl_fichada_smarttime
    {
        public int id { get; set; }

        public int fichada_id_st { get; set; }
        public string legajo { get; set; }
        public string tipo_operacion { get; set; }
        public int? turno_id { get; set; }
        public DateTime fecha_fichada { get; set; }

        public int? id_dispositivo { get; set; }
        public int? id_controladora { get; set; }

        public string raw_json { get; set; }
        public DateTime createdate { get; set; }
    }
}
