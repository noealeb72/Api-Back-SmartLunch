using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace smartlunch_api.Dtos
{
    public class SmartimeLectorDto
    {
        [JsonProperty("idControladora")]
        public int IdControladora { get; set; }

        [JsonProperty("idDispositivo")]
        public int IdDispositivo { get; set; }

        [JsonProperty("descripcionDispositivo")]
        public string DescripcionDispositivo { get; set; }
    }

    public class SmartimeFichadaDto
    {
        // neventLogIdn no se envía en JSON, así que lo ignoramos

        [JsonProperty("lector")]
        public SmartimeLectorDto Lector { get; set; }

        // Igual que en Java: "yyyy-MM-dd'T'HH:mm:ss"
        [JsonProperty("fechaFichada")]
        public string FechaFichada { get; set; }

        [JsonProperty("numeroDeTarjeta")]
        public string NumeroDeTarjeta { get; set; }

        [JsonProperty("tipoOperacionEntradaSalida")]
        public string TipoOperacionEntradaSalida { get; set; }  // lo dejamos null
    }

    public class SmartimeEndpointRequestDto
    {
        [JsonProperty("fichada")]
        public List<SmartimeFichadaDto> Fichada { get; set; }

        [JsonProperty("zonaHoraria")]
        public string ZonaHoraria { get; set; }
    }

    // Respuesta simplificada (solo lo más útil)
    public class SmartimeFichadaResponseDto
    {
        [JsonProperty("procesadaOk")]
        public bool ProcesadaOk { get; set; }

        [JsonProperty("mensajeError")]
        public string MensajeError { get; set; }

        [JsonProperty("codigoError")]
        public int? CodigoError { get; set; }

        [JsonProperty("legajo")]
        public string Legajo { get; set; }
    }

    public class SmartimeEndpointResponseDto
    {
        [JsonProperty("fichada")]
        public List<SmartimeFichadaResponseDto> Fichada { get; set; }

        [JsonProperty("mensaje")]
        public string Mensaje { get; set; }
    }
}
