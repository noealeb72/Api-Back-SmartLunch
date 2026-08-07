using System;

namespace smartlunch_api.Dtos
{
    // Para alta/actualización
    public class ConfigTotemDto
    {
        public string device_id { get; set; }
        public bool biostar_modo { get; set; }
        public int biostar_interval_segundos { get; set; }
        public bool smarttime_modo { get; set; }
    }

    // Para devolver detalle/listado
    public class ConfigTotemDetalleDto
    {
        public int Id { get; set; }
        public string DeviceId { get; set; }
        public bool BiostarModo { get; set; }
        public int BiostarIntervalSegundos { get; set; }
        public bool SmarttimeModo { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool DeleteMark { get; set; }
    }
}
