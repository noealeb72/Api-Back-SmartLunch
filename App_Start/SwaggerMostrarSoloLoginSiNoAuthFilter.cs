using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http.Description;
using Swashbuckle.Swagger;

namespace smartlunch_api
{
    /// <summary>
    /// Muestra solo el endpoint POST /api/login/Autentificar hasta que el usuario tenga token (cookie swagger_bearer o header Authorization).
    /// Una vez autenticado (token en cookie o header), se muestra la documentación completa de todos los endpoints.
    /// </summary>
    public class SwaggerMostrarSoloLoginSiNoAuthFilter : IDocumentFilter
    {
        private const string CookieName = "swagger_bearer";
        private const string AutentificarPath = "/api/login/Autentificar";

        public void Apply(SwaggerDocument swaggerDoc, SchemaRegistry schemaRegistry, IApiExplorer apiExplorer)
        {
            if (TieneTokenEnRequest())
                return;

            // Sin token: dejar solo el endpoint de Autenticar
            var paths = swaggerDoc.paths ?? new Dictionary<string, PathItem>();
            var keysToRemove = paths.Keys.Where(k => !EsEndpointAutentificar(k)).ToList();
            foreach (var key in keysToRemove)
                paths.Remove(key);
        }

        private static bool TieneTokenEnRequest()
        {
            try
            {
                var request = HttpContext.Current?.Request;
                if (request == null) return false;

                var auth = request.Headers["Authorization"];
                if (!string.IsNullOrWhiteSpace(auth) && auth.TrimStart().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    return true;

                var cookie = request.Cookies[CookieName];
                if (cookie != null && !string.IsNullOrWhiteSpace(cookie.Value))
                    return true;

                return false;
            }
            catch
            {
                return false;
            }
        }

        private static bool EsEndpointAutentificar(string pathKey)
        {
            if (string.IsNullOrWhiteSpace(pathKey)) return false;
            var normalized = pathKey.Trim().TrimEnd('/');
            return normalized.EndsWith("/Autentificar", StringComparison.OrdinalIgnoreCase)
                   && normalized.IndexOf("/api/login", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
