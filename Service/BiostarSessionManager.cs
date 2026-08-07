using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Net.Http;
using System.Runtime.Caching;
using System.Text;
using System.Threading.Tasks;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Maneja el login a Biostar y el cache del bs-session-id.
    /// Cachea hasta que Biostar devuelva error (401/403) y ahí se invalida.
    /// </summary>
    public class BiostarSessionManager
    {
        private const string CacheKey = "BIOSTAR_SESSION_ID";
        private static readonly ObjectCache Cache = MemoryCache.Default;
        private static readonly object _lock = new object();

        private readonly string _baseUrl;
        private readonly string _loginPath;
        private readonly string _user;
        private readonly string _password;

        // HttpClient compartido (no crear uno por request)
        private static readonly HttpClient _http = CreateHttpClient();

        private static HttpClient CreateHttpClient()
        {
            var timeoutSeconds = 60; // Valor por defecto
            var timeoutStr = ConfigurationManager.AppSettings["BiostarHttpTimeoutSeconds"];
            if (!string.IsNullOrWhiteSpace(timeoutStr) && int.TryParse(timeoutStr, out var parsedTimeout))
            {
                timeoutSeconds = parsedTimeout;
            }

            return new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds)
            };
        }

        public BiostarSessionManager(
            string baseUrl,
            string loginPath,
            string user,
            string password)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _loginPath = string.IsNullOrWhiteSpace(loginPath) ? "/api/login" : loginPath;
            _user = user;
            _password = password;
        }

        /// <summary>
        /// Devuelve un bs-session-id válido.
        /// Usa cache si existe; si no, hace login a Biostar.
        /// </summary>
        public async Task<string> GetSessionIdAsync()
        {
            // 1) Intentar leer de cache
            var cached = Cache.Get(CacheKey) as string;
            if (!string.IsNullOrEmpty(cached))
                return cached;

            // 2) Double-check con lock (evitar múltiples logins en paralelo)
            lock (_lock)
            {
                cached = Cache.Get(CacheKey) as string;
                if (!string.IsNullOrEmpty(cached))
                    return cached;
            }

            // 3) No hay sesión → hacer login
            var loginBody = new
            {
                User = new
                {
                    login_id = _user,
                    password = _password
                }
            };

            var json = JsonConvert.SerializeObject(loginBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var loginUrl = _baseUrl + _loginPath; // ej: https://IP:4433/api/login

            var resp = await _http.PostAsync(loginUrl, content);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"Biostar login failed: {resp.StatusCode} - {respBody}");
            }

            // 4) Leer header bs-session-id
            if (!resp.Headers.TryGetValues("bs-session-id", out var values))
            {
                throw new Exception("Biostar login OK pero no devolvió header 'bs-session-id'.");
            }

            string sessionId = null;
            foreach (var v in values)
            {
                sessionId = v;
                break;
            }

            if (string.IsNullOrEmpty(sessionId))
            {
                throw new Exception("Biostar session id vacío.");
            }

            // 5) Guardar en cache SIN expiración fija
            lock (_lock)
            {
                Cache.Set(
                    CacheKey,
                    sessionId,
                    new CacheItemPolicy() // sin AbsoluteExpiration ni SlidingExpiration → “infinito”
                );
            }

            return sessionId;
        }

        /// <summary>
        /// Borra el session id del cache (por ejemplo si Biostar devuelve 401/403).
        /// </summary>
        public void InvalidateSession()
        {
            Cache.Remove(CacheKey);
        }
    }
}
