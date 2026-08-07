using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Newtonsoft.Json;

namespace smartlunch_api.Filters
{
    /// <summary>
    /// Filtro de autorización personalizado que devuelve mensajes descriptivos en castellano para errores 401
    /// </summary>
    public class CustomAuthorizationFilterAttribute : AuthorizationFilterAttribute
    {
        public override void OnAuthorization(HttpActionContext actionContext)
        {
            // Verificar si el usuario está autenticado
            var principal = actionContext.RequestContext.Principal;
            var identity = principal?.Identity as ClaimsIdentity;

            if (identity == null || !identity.IsAuthenticated)
            {
                // Determinar el mensaje según el contexto
                string mensajeError = "Token de autenticación inválido o expirado. Por favor, inicie sesión nuevamente.";

                // Verificar si hay un token en el header
                var authHeader = actionContext.Request.Headers.Authorization;
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

                // Crear respuesta con mensaje en castellano
                var errorResponse = new
                {
                    Message = mensajeError,
                    error = mensajeError
                };

                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.Unauthorized,
                    errorResponse
                );
                return;
            }

            // Si está autenticado, continuar normalmente
            base.OnAuthorization(actionContext);
        }
    }
}
