using Newtonsoft.Json;
using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Cliente para llamar a Biostar (/api/events/search) usando el bs-session-id
    /// gestionado por BiostarSessionManager.
    /// </summary>
    public class BiostarClient
    {
        private readonly BiostarSessionManager _sessionManager;
        private readonly string _baseUrl;
        private readonly string _eventsPath;

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

        public BiostarClient(
            BiostarSessionManager sessionManager,
            string baseUrl,
            string eventsPath)
        {
            _sessionManager = sessionManager;
            _baseUrl = baseUrl.TrimEnd('/');
            _eventsPath = string.IsNullOrWhiteSpace(eventsPath)
                ? "/api/events/search"
                : eventsPath;
        }

        /// <summary>
        /// Llama a /api/events/search con el bs-session-id actual.
        /// Si la sesión está vencida (401/403), invalida cache, hace login de nuevo
        /// y reintenta una vez.
        /// </summary>
        public async Task<string> SearchEventsAsync(object queryBody)
        {
            // 1) Primer intento con sesión actual
            var sessionId = await _sessionManager.GetSessionIdAsync();
            var result = await CallEventsSearchAsync(queryBody, sessionId);

            // 2) Si la sesión está caída, reintenta una vez
            if (result.StatusCode == HttpStatusCode.Unauthorized ||
                result.StatusCode == HttpStatusCode.Forbidden)
            {
                _sessionManager.InvalidateSession();

                var newSessionId = await _sessionManager.GetSessionIdAsync();
                result = await CallEventsSearchAsync(queryBody, newSessionId);
            }

            var content = await result.Content.ReadAsStringAsync();

            if (!result.IsSuccessStatusCode)
            {
                throw new Exception($"Biostar events/search error: {result.StatusCode} - {content}");
            }

            return content;
        }

        /// <summary>
        /// POST directo a /api/events/search con el bs-session-id indicado.
        /// </summary>
        private Task<HttpResponseMessage> CallEventsSearchAsync(object queryBody, string sessionId)
        {
            var json = JsonConvert.SerializeObject(queryBody);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var url = _baseUrl + _eventsPath; // ej: https://IP:4433/api/events/search

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = httpContent
            };

            // Header obligatorio para Biostar
            request.Headers.Add("bs-session-id", sessionId);
            request.Headers.Accept.ParseAdd("application/json");

            return _http.SendAsync(request);
        }
    }
}
