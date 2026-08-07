using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace smartlunch_api.Handlers
{
    /// <summary>
    /// MessageHandler para interceptar respuestas 401 y devolver mensajes descriptivos en castellano
    /// </summary>
    public class UnauthorizedResponseHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Si la respuesta es 401, reemplazar el mensaje
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                // Determinar el mensaje según el contexto
                string mensajeError = "Token de autenticación inválido o expirado. Por favor, inicie sesión nuevamente.";

                // Verificar si hay un token en el header
                var authHeader = request.Headers.Authorization;
                if (authHeader == null || string.IsNullOrWhiteSpace(authHeader.Scheme) || string.IsNullOrWhiteSpace(authHeader.Parameter))
                {
                    mensajeError = "No se proporcionó token de autenticación. Por favor, inicie sesión.";
                }
                else if (!authHeader.Scheme.Equals("Bearer", StringComparison.OrdinalIgnoreCase))
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
                var newResponse = request.CreateResponse(HttpStatusCode.Unauthorized, errorResponse);
                newResponse.Content = new StringContent(jsonResponse, System.Text.Encoding.UTF8, "application/json");

                return newResponse;
            }

            return response;
        }
    }
}
