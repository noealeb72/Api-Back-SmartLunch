using Newtonsoft.Json;

namespace smartlunch_api.Dtos
{
    public class SmartTimeLoginRequestDto
    {
        [JsonProperty("login")]
        public SmartTimeLoginBodyDto Login { get; set; }
    }

    public class SmartTimeLoginBodyDto
    {
        [JsonProperty("nombreAplicacion")]
        public string NombreAplicacion { get; set; }

        [JsonProperty("key")]
        public string Key { get; set; }
    }

    public class SmartTimeLoginResponseDto
    {
        [JsonProperty("token")]
        public string Token { get; set; }

        [JsonProperty("zonaHoraria")]
        public string ZonaHoraria { get; set; }

        [JsonProperty("licenciaValida")]
        public bool LicenciaValida { get; set; }
    }
}
