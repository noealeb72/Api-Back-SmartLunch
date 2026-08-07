using System.Linq;
using Swashbuckle.Swagger;
using System.Web.Http;
using System.Web.Http.Description;

namespace smartlunch_api
{
    /// <summary>
    /// Filtro para agregar automáticamente el header de Authorization (JWT) a los endpoints con [Authorize]
    /// </summary>
    public class AddAuthorizationHeaderFilter : IOperationFilter
    {
        public void Apply(Operation operation, SchemaRegistry schemaRegistry, ApiDescription apiDescription)
        {
            // Verificar si el endpoint tiene el atributo [Authorize]
            var hasAuthorize = apiDescription.ActionDescriptor.GetCustomAttributes<AuthorizeAttribute>().Any() ||
                              apiDescription.ActionDescriptor.ControllerDescriptor.GetCustomAttributes<AuthorizeAttribute>().Any();

            // Verificar si tiene [AllowAnonymous] (que sobrescribe [Authorize])
            var hasAllowAnonymous = apiDescription.ActionDescriptor.GetCustomAttributes<AllowAnonymousAttribute>().Any();

            if (hasAuthorize && !hasAllowAnonymous)
            {
                // Agregar el parámetro de Authorization si no existe
                if (operation.parameters == null)
                {
                    operation.parameters = new System.Collections.Generic.List<Parameter>();
                }

                // Verificar si ya existe el parámetro Authorization
                var authParam = operation.parameters.FirstOrDefault(p => p.name == "Authorization");
                if (authParam == null)
                {
                    operation.parameters.Add(new Parameter
                    {
                        name = "Authorization",
                        @in = "header",
                        description = "JWT Authorization token. Ejemplo: Bearer {token}",
                        required = true,
                        type = "string"
                    });
                }

                // Agregar la referencia de seguridad
                if (operation.security == null)
                {
                    operation.security = new System.Collections.Generic.List<System.Collections.Generic.IDictionary<string, System.Collections.Generic.IEnumerable<string>>>();
                }

                var securityDict = new System.Collections.Generic.Dictionary<string, System.Collections.Generic.IEnumerable<string>>
                {
                    { "Bearer", System.Linq.Enumerable.Empty<string>() }
                };
                operation.security.Add(securityDict);
            }
        }
    }
}

