using Newtonsoft.Json;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace smartlunch_api.Service
{
    /// <summary>
    /// Maneja la sesión contra Biostar2.
    /// Guarda en memoria el HttpClient + bs-session-id y solo reloguea cuando hace falta.
    /// </summary>
    public class ServicioBiostar
    {
        private readonly string _baseUrl;
        private readonly string _loginPath;
        private readonly string _user;
        private readonly string _password;

        // ====== CACHE EN MEMORIA (compartida para toda la app) ======
        private static HttpClient _clienteCompartido;
        private static bool _tieneSesionValida;
        private static readonly object _lockSesion = new object();

        public ServicioBiostar(string baseUrl, string loginPath, string user, string password)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _loginPath = loginPath;
            _user = user;
            _password = password;
        }

        /// <summary>
        /// Devuelve un HttpClient autenticado.
        /// Si ya hay sesión en memoria, NO vuelve a loguear.
        /// Solo reloguea si se le pide forzarLogin (por ejemplo después de un 401/403).
        /// </summary>
        public async Task<HttpClient> ObtenerClienteAutenticadoAsync(bool forzarLogin = false)
        {
            // 1) Crear cliente compartido una sola vez
            if (_clienteCompartido == null)
            {
                var cookieContainer = new CookieContainer();

                var handler = new HttpClientHandler
                {
                    CookieContainer = cookieContainer,
                    UseCookies = true,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };

                // Si el certificado es self-signed:
                // handler.ServerCertificateCustomValidationCallback =
                //     (sender, cert, chain, sslPolicyErrors) => true;

                _clienteCompartido = new HttpClient(handler)
                {
                    BaseAddress = new Uri(_baseUrl)
                };
            }

            // 2) Si ya tenemos sesión válida y no nos pidieron forzar login, devolvemos tal cual
            if (_tieneSesionValida && !forzarLogin)
            {
                return _clienteCompartido;
            }

            // 3) Si no tenemos sesión, o nos pidieron forzar login, logueamos UNA vez
            //    con lock para que varios threads no logueen al mismo tiempo
            lock (_lockSesion)
            {
                // doble chequeo dentro del lock
                if (_tieneSesionValida && !forzarLogin)
                {
                    return _clienteCompartido;
                }

                // hacemos login sincrónico desde el lock llamando a método async
                // (esperamos afuera del lock en realidad)
            }

            // hacemos el login fuera del lock para no bloquear mientras esperamos la red
            await RealizarLoginAsync(_clienteCompartido);

            lock (_lockSesion)
            {
                _tieneSesionValida = true;
            }

            return _clienteCompartido;
        }

        /// <summary>
        /// Fuerza a invalidar la sesión en memoria (se usa después de un 401/403).
        /// </summary>
        public void InvalidarSesion()
        {
            lock (_lockSesion)
            {
                _tieneSesionValida = false;
            }
        }

        /// <summary>
        /// Hace POST /api/login y actualiza el header bs-session-id del cliente.
        /// </summary>
        private async Task RealizarLoginAsync(HttpClient client)
        {
            // Limpio header viejo si hubiera
            client.DefaultRequestHeaders.Remove("bs-session-id");

            var loginBody = new
            {
                // IMPORTANTE: "User" con mayúscula, como en RestClient/Insomnia
                User = new
                {
                    login_id = _user,
                    password = _password
                }
            };

            var loginJson = JsonConvert.SerializeObject(loginBody);
            var loginContent = new StringContent(loginJson, Encoding.UTF8, "application/json");

            var loginResponse = await client.PostAsync(_loginPath, loginContent);
            var loginResponseBody = await loginResponse.Content.ReadAsStringAsync();

            if (!loginResponse.IsSuccessStatusCode)
            {
                throw new Exception(
                    $"Error login Biostar: {(int)loginResponse.StatusCode} - {loginResponseBody}");
            }

            if (!loginResponse.Headers.TryGetValues("bs-session-id", out var values))
            {
                throw new Exception("Login Biostar OK pero no devolvió header 'bs-session-id'.");
            }

            var bsSessionId = values.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(bsSessionId))
            {
                throw new Exception("Header 'bs-session-id' vacío.");
            }

            client.DefaultRequestHeaders.Add("bs-session-id", bsSessionId);

            // La cookie JSESSIONID se guarda sola en el CookieContainer del handler,
            // y como el HttpClient es estático, la seguimos reutilizando en todas las llamadas.
        }
    }
}
