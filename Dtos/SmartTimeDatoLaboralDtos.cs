using System.Collections.Generic;
using Newtonsoft.Json;

namespace smartlunch_api.Dtos
{
    public class SmartimeDatoLaboralResponseDto
    {
        [JsonProperty("datoLaboral")]
        public List<SmartimeDatoLaboralItemDto> DatoLaboral { get; set; }
    }

    public class SmartimeDatoLaboralItemDto
    {
        [JsonProperty("legajo")]
        public int Legajo { get; set; }

        [JsonProperty("apellidoNombre")]
        public string ApellidoNombre { get; set; }

        // Cuando veas el JSON real podés agregar más campos acá.
        // Ej:
        [JsonProperty("dni")] public int Dni { get; set; }
        [JsonProperty("cuil")] public string Cuil { get; set; }
    }
}
