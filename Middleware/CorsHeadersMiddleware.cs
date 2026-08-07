using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Owin;
using Owin;

namespace smartlunch_api.Middleware
{
    /// <summary>
    /// Añade cabeceras CORS a todas las respuestas (incluidas 400/500) para que el front pueda leer el cuerpo.
    /// Responde a OPTIONS (preflight) con 204 y cabeceras CORS.
    /// </summary>
    public class CorsHeadersMiddleware
    {
        private readonly Func<IDictionary<string, object>, Task> _next;
        private static readonly string[] AllowedOrigins = GetAllowedOrigins();
        private static readonly string AllowedMethods = ConfigurationManager.AppSettings["CorsAllowedMethods"] ?? "GET,POST,PUT,DELETE,OPTIONS";
        private static readonly string AllowedHeaders = ConfigurationManager.AppSettings["CorsAllowedHeaders"] ?? "Content-Type,Authorization,Accept,Origin,Cache-Control,Pragma,Expires,X-Requested-With,X-Request-Time";

        public CorsHeadersMiddleware(Func<IDictionary<string, object>, Task> next)
        {
            _next = next;
        }

        public async Task Invoke(IDictionary<string, object> environment)
        {
            var context = new OwinContext(environment);
            var origin = context.Request.Headers.Get("Origin");
            var isAllowedOrigin = !string.IsNullOrEmpty(origin) && AllowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);

            if (isAllowedOrigin)
            {
                context.Response.Headers["Access-Control-Allow-Origin"] = origin;
                context.Response.Headers["Access-Control-Allow-Methods"] = AllowedMethods;
                context.Response.Headers["Access-Control-Allow-Headers"] = AllowedHeaders;
                context.Response.Headers["Access-Control-Max-Age"] = "86400";
            }

            // Preflight OPTIONS: responder 204 sin pasar al pipeline
            if (context.Request.Method == "OPTIONS")
            {
                context.Response.StatusCode = 204;
                return;
            }

            await _next(environment);
        }

        private static string[] GetAllowedOrigins()
        {
            var value = ConfigurationManager.AppSettings["CorsAllowedOrigins"] ?? "";
            // Sin whitelist configurada, no permitir ningún origen (fail closed) — no hardcodear
            // orígenes de respaldo acá, menos aún IPs internas del servidor.
            return value
                .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => !string.IsNullOrEmpty(o))
                .ToArray();
        }
    }

    /// <summary>
    /// Extensión para registrar el middleware CORS.
    /// </summary>
    public static class CorsHeadersMiddlewareExtensions
    {
        public static IAppBuilder UseCorsHeaders(this IAppBuilder app)
        {
            return app.Use<CorsHeadersMiddleware>();
        }
    }
}
