using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;
using Microsoft.Owin;
using Newtonsoft.Json;
using Owin;

namespace smartlunch_api.Middleware
{
    /// <summary>
    /// Middleware OWIN para interceptar respuestas 401 y devolver mensajes descriptivos en castellano
    /// </summary>
    public class UnauthorizedResponseMiddleware
    {
        private readonly Func<IDictionary<string, object>, Task> _next;

        public UnauthorizedResponseMiddleware(Func<IDictionary<string, object>, Task> next)
        {
            _next = next;
        }

        public async Task Invoke(IDictionary<string, object> environment)
        {
            var context = new OwinContext(environment);
            var response = context.Response;

            // Interceptar la respuesta después de que se procese
            var originalBody = response.Body;
            var responseBody = new MemoryStream();
            response.Body = responseBody;

            try
            {
                await _next(environment);

                // Si la respuesta es 401, reemplazar el mensaje
                if (response.StatusCode == 401)
                {
                    // Determinar el mensaje según el contexto
                    string mensajeError = "Token de autenticación inválido o expirado. Por favor, inicie sesión nuevamente.";

                    // Verificar si hay un token en el header
                    var authHeader = context.Request.Headers.Get("Authorization");
                    if (string.IsNullOrWhiteSpace(authHeader))
                    {
                        mensajeError = "No se proporcionó token de autenticación. Por favor, inicie sesión.";
                    }
                    else if (!authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                    {
                        mensajeError = "Formato de token inválido. El token debe comenzar con 'Bearer '.";
                    }
                    else
                    {
                        // Si hay token pero fue rechazado, probablemente está expirado o es inválido
                        mensajeError = "Token de autenticación expirado o inválido. Por favor, inicie sesión nuevamente.";
                    }

                    // Crear nueva respuesta con mensaje en castellano
                    var errorResponse = new
                    {
                        Message = mensajeError,
                        error = mensajeError
                    };

                    var jsonResponse = JsonConvert.SerializeObject(errorResponse);
                    var bytes = Encoding.UTF8.GetBytes(jsonResponse);

                    // Resetear el stream de respuesta
                    responseBody.SetLength(0);
                    responseBody.Write(bytes, 0, bytes.Length);
                    responseBody.Seek(0, SeekOrigin.Begin);
                    
                    response.StatusCode = 401;
                    response.ContentType = "application/json; charset=utf-8";
                    response.ContentLength = bytes.Length;
                }

                // Copiar el contenido al stream original
                responseBody.Seek(0, SeekOrigin.Begin);
                await responseBody.CopyToAsync(originalBody);
            }
            finally
            {
                responseBody.Dispose();
                response.Body = originalBody;
            }
        }
    }

    /// <summary>
    /// Extensión para registrar el middleware fácilmente
    /// </summary>
    public static class UnauthorizedResponseMiddlewareExtensions
    {
        public static IAppBuilder UseUnauthorizedResponseHandler(this IAppBuilder app)
        {
            return app.Use<UnauthorizedResponseMiddleware>();
        }
    }
}
