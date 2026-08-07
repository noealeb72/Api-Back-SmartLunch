using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace smartlunch_api.Filters
{
    /// <summary>
    /// Filtro que valida automáticamente el ModelState antes de ejecutar la acción
    /// Retorna HTTP 400 con los errores de validación si el modelo es inválido
    /// </summary>
    public class ValidateModelAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            if (!actionContext.ModelState.IsValid)
            {
                // Extraer todos los errores de validación
                var errors = actionContext.ModelState
                    .Where(x => x.Value.Errors.Count > 0)
                    .SelectMany(x => x.Value.Errors.Select(e => new
                    {
                        Field = x.Key,
                        Message = e.ErrorMessage ?? (e.Exception != null ? e.Exception.Message : "Error de validación")
                    }))
                    .ToList();

                // Crear respuesta de error estructurada
                var errorResponse = new
                {
                    success = false,
                    message = "Errores de validación",
                    errors = errors.Select(e => new
                    {
                        field = e.Field,
                        message = e.Message
                    })
                };

                actionContext.Response = actionContext.Request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    errorResponse
                );
            }
        }
    }
}

