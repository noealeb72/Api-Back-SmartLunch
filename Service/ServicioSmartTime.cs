using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Maneja la sesión contra SmartTime/Sintaryc:
    /// - Hace login con Basic (usuario + password)
    /// - Guarda el token JWT en memoria
    /// - Reutiliza un HttpClient compartido
    /// </summary>
    public class ServicioSmartTime
    {
        private readonly string _baseUrl;
        private readonly string _loginPath;

        private readonly string _usuario;
        private readonly string _password;
        private readonly string _nombreApp;
        private readonly string _keyApp;
        private readonly string _authHeaderConfig;

        // ====== CACHE EN MEMORIA (COMPARTIDA) ======
        private static HttpClient _clienteCompartido;
        private static bool _tieneSesionValida;
        private static string _tokenActual;
        private static readonly object _lockSesion = new object();

        public ServicioSmartTime()
        {
            _baseUrl = (ConfigurationManager.AppSettings["SmartimeBaseUrl"] ?? "").TrimEnd('/');
            _loginPath = ConfigurationManager.AppSettings["SmartTimeLoginPath"] ?? "/Seguridad/LoginAplicacion";

            _usuario = ConfigurationManager.AppSettings["SmartTimeUsuario"] ?? "IT_FICHADA";
            // Sin fallback hardcodeado acá: es una contraseña real, tiene que venir de
            // appSettings.secrets.config (gitignoreado), no quedar pegada en el código fuente.
            _password = ConfigurationManager.AppSettings["SmartTimePassword"];
            _nombreApp = ConfigurationManager.AppSettings["SmartTimeNombreApp"] ?? "ITARSA_PRD";
            _keyApp = ConfigurationManager.AppSettings["SmartTimeKey"] ?? "ArsaProd";

            _authHeaderConfig = ConfigurationManager.AppSettings["SmartTimeAuthHeader"];

            if (string.IsNullOrWhiteSpace(_baseUrl))
                throw new Exception("Falta SmartimeBaseUrl en Web.config");
            if (string.IsNullOrWhiteSpace(_password))
                throw new Exception("Falta SmartTimePassword en appSettings.secrets.config");
        }

        /// <summary>
        /// Devuelve un HttpClient autenticado contra SmartTime.
        /// Si ya hay token en memoria y no se fuerza login, NO vuelve a loguear.
        /// </summary>
        public async Task<HttpClient> ObtenerClienteAutenticadoAsync(bool forzarLogin = false)
        {
            // 1) Crear HttpClient compartido una sola vez
            if (_clienteCompartido == null)
            {
                var handler = new HttpClientHandler
                {
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };

                _clienteCompartido = new HttpClient(handler)
                {
                    BaseAddress = new Uri(_baseUrl)
                };
            }

            // 2) Si ya tenemos sesión válida y no forzás login, devolvemos tal cual
            if (_tieneSesionValida && !forzarLogin)
                return _clienteCompartido;

            lock (_lockSesion)
            {
                if (_tieneSesionValida && !forzarLogin)
                    return _clienteCompartido;
            }

            // 3) Hacer login
            await RealizarLoginAsync(_clienteCompartido);

            lock (_lockSesion)
            {
                _tieneSesionValida = true;
            }

            return _clienteCompartido;
        }

        /// <summary>
        /// Invalida la sesión en memoria (para usar después de un 401).
        /// </summary>
        public void InvalidarSesion()
        {
            lock (_lockSesion)
            {
                _tieneSesionValida = false;
                _tokenActual = null;
            }
        }

        /// <summary>
        /// Hace POST /Seguridad/LoginAplicacion, obtiene el token JWT
        /// y setea Authorization: Bearer {token} en el HttpClient.
        /// Lee el "token" directo del JSON (sin DTO).
        /// </summary>
        private async Task RealizarLoginAsync(HttpClient client)
        {
            client.DefaultRequestHeaders.Remove("Authorization");

            var body = new
            {
                login = new
                {
                    nombreAplicacion = _nombreApp,
                    key = _keyApp
                }
            };

            var json = JsonConvert.SerializeObject(body);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            string authHeader;
            if (!string.IsNullOrWhiteSpace(_authHeaderConfig))
            {
                authHeader = _authHeaderConfig;
            }
            else
            {
                var basic = Convert.ToBase64String(
                    Encoding.UTF8.GetBytes($"{_usuario}:{_password}")
                );
                authHeader = $"Basic {basic}";
            }

            client.DefaultRequestHeaders.Add("Authorization", authHeader);

            var response = await client.PostAsync(_loginPath, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
                throw new Exception(
                    $"Error login SmartTime ({(int)response.StatusCode}): {responseJson}");

            string token;
            try
            {
                var jObj = JObject.Parse(responseJson);
                token = (string)jObj["token"];
            }
            catch (Exception ex)
            {
                throw new Exception("No se pudo leer el token de la respuesta SmartTime: " + ex.Message);
            }

            if (string.IsNullOrWhiteSpace(token))
                throw new Exception("Login SmartTime OK pero no devolvió 'token'.");

            _tokenActual = token;

            client.DefaultRequestHeaders.Remove("Authorization");
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _tokenActual);
        }
    }
}
