using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace smartlunch_api.Service
{
    /// <summary>
    /// Cliente para /api/events/search de Biostar2.
    /// Usa ServicioBiostar para manejar la sesión cacheada.
    /// </summary>
    public class ServicioClienteBiostar
    {
        private readonly ServicioBiostar _servicioBiostar;
        private readonly string _eventsPath;

        public ServicioClienteBiostar(ServicioBiostar servicioBiostar, string eventsPath)
        {
            _servicioBiostar = servicioBiostar;
            _eventsPath = eventsPath;
        }

        public async Task<string> BuscarEventosAsync(object queryBody)
        {
            var json = JsonConvert.SerializeObject(queryBody);

            StringContent CrearContenido() =>
                new StringContent(json, Encoding.UTF8, "application/json");

            // 1) Primer intento
            var client = await _servicioBiostar.ObtenerClienteAutenticadoAsync();
            var content = CrearContenido();
            var response = await client.PostAsync(_eventsPath, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            // 2) Si la sesión cayó, reintento una vez
            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.StatusCode == HttpStatusCode.Forbidden)
            {
                _servicioBiostar.InvalidarSesion();

                client = await _servicioBiostar.ObtenerClienteAutenticadoAsync(forzarLogin: true);
                content = CrearContenido();
                response = await client.PostAsync(_eventsPath, content);
                responseJson = await response.Content.ReadAsStringAsync();
            }

            if (!response.IsSuccessStatusCode)
            {
                // ⛔ Tiramos excepción específica con todos los datos
                throw new BiostarException(
                    "Error consultando Biostar /api/events/search",
                    (int)response.StatusCode,
                    responseJson,
                    client.BaseAddress != null
                        ? new Uri(client.BaseAddress, _eventsPath).ToString()
                        : _eventsPath
                );
            }

            return responseJson;
        }
    }
}
