using Newtonsoft.Json;
using smartlunch_api.Dtos;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace smartlunch_api.Services
{
    public class ServicioClienteSmartTimeDatoLaboral
    {
        private readonly ServicioSmartTime _servicioSmartTime;
        private readonly string _datoLaboralPath;
        private readonly string _empresa;
        private readonly string _traza;
        private readonly string _aplicacionOrigen;

        public ServicioClienteSmartTimeDatoLaboral()
        {
            _servicioSmartTime = new ServicioSmartTime();

            _datoLaboralPath = ConfigurationManager.AppSettings["SmartTimeDatoLaboralPath"] ?? "/DatoLaboral";
            _empresa = ConfigurationManager.AppSettings["SmartTimeEmpresa"] ?? "1";
            _traza = ConfigurationManager.AppSettings["SmartTimeTraza"] ?? "traza";
            _aplicacionOrigen = ConfigurationManager.AppSettings["SmartTimeAplicacionOrigen"] ?? "swagger";
        }

        public async Task<string> ObtenerDatoLaboralRawAsync(int legajo)
        {
            var client = await _servicioSmartTime.ObtenerClienteAutenticadoAsync();

            if (!client.DefaultRequestHeaders.Contains("traza"))
                client.DefaultRequestHeaders.Add("traza", _traza);

            if (!client.DefaultRequestHeaders.Contains("aplicacionorigen"))
                client.DefaultRequestHeaders.Add("aplicacionorigen", _aplicacionOrigen);

            if (!client.DefaultRequestHeaders.Contains("empresa"))
                client.DefaultRequestHeaders.Add("empresa", _empresa);

            var url = $"{_datoLaboralPath}?DatoLaboral.Legajos={legajo}";

            var response = await client.GetAsync(url);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _servicioSmartTime.InvalidarSesion();
                client = await _servicioSmartTime.ObtenerClienteAutenticadoAsync(forzarLogin: true);

                if (!client.DefaultRequestHeaders.Contains("traza"))
                    client.DefaultRequestHeaders.Add("traza", _traza);
                if (!client.DefaultRequestHeaders.Contains("aplicacionorigen"))
                    client.DefaultRequestHeaders.Add("aplicacionorigen", _aplicacionOrigen);
                if (!client.DefaultRequestHeaders.Contains("empresa"))
                    client.DefaultRequestHeaders.Add("empresa", _empresa);

                response = await client.GetAsync(url);
                responseJson = await response.Content.ReadAsStringAsync();
            }

            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Error llamando a SmartTime DatoLaboral ({(int)response.StatusCode}): {responseJson}");

            return responseJson;
        }

        public async Task<SmartimeDatoLaboralResponseDto> ObtenerDatoLaboralAsync(int legajo)
        {
            var json = await ObtenerDatoLaboralRawAsync(legajo);
            var dto = JsonConvert.DeserializeObject<SmartimeDatoLaboralResponseDto>(json)
                       ?? new SmartimeDatoLaboralResponseDto();
            return dto;
        }
    }
}
