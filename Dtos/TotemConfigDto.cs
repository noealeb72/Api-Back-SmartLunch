// smartlunch_api/Models/DTOs/TotemConfigDto.cs
using System;

namespace smartlunch_api.Models.DTOs
{
    public class TotemConfigDto
    {
        public int id { get; set; }
        public string device_id { get; set; }
        public string api_base_url { get; set; }

        public string biostar_modo { get; set; }
        public string biostar_address { get; set; }
        public string biostar_authorization { get; set; }
        public string biostar_session_id { get; set; }
        public string biostar_device_id { get; set; }
        public int biostar_interval_segundos { get; set; }

        public string smarttime_modo { get; set; }
        public string smarttime_url { get; set; }
        public string smarttime_autorizacion { get; set; }
        public string smarttime_usuario { get; set; }
        public string smarttime_contrasena { get; set; }
        public string smarttime_nombre_aplicacion { get; set; }
        public string smarttime_key { get; set; }
        public int smarttime_empresa { get; set; }
        public string smarttime_traza { get; set; }
        public string smarttime_aplicacion_origen { get; set; }

        public DateTime? created_at { get; set; }
        public DateTime? updated_at { get; set; }
    }

    public class TotemConfigDeleteDto
    {
        public int id { get; set; }
    }
}
