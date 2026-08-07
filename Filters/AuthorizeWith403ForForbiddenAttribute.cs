using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using Newtonsoft.Json;

namespace smartlunch_api.Filters
{
    /// <summary>
    /// Igual que [Authorize(Roles = "...")] pero cuando el usuario está autenticado y no tiene el rol
    /// devuelve 403 Forbidden con mensaje "Usuario no autorizado" en lugar de 401, para que el front
    /// no cierre la sesión y muestre un cartel informativo.
    /// </summary>
    public class AuthorizeWith403ForForbiddenAttribute : AuthorizeAttribute
    {
        private const string MensajeNoAutorizado = "Usuario no autorizado para realizar esta acción. No tiene los permisos necesarios.";

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            var principal = actionContext.RequestContext.Principal;
            bool isAuthenticated = principal?.Identity?.IsAuthenticated ?? false;

            if (isAuthenticated)
            {
                // Usuario logueado pero sin el rol requerido -> 403 (no cerrar sesión en el front)
                var errorResponse = new
                {
                    Message = MensajeNoAutorizado,
                    error = MensajeNoAutorizado
                };
                var response = actionContext.Request.CreateResponse(HttpStatusCode.Forbidden, errorResponse);
                response.Content = new StringContent(
                    JsonConvert.SerializeObject(errorResponse),
                    System.Text.Encoding.UTF8,
                    "application/json");
                actionContext.Response = response;
            }
            else
            {
                // No autenticado -> 401 (token faltante o inválido, el front puede redirigir a login)
                base.HandleUnauthorizedRequest(actionContext);
            }
        }
    }
}
