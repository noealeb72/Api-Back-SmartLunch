// Controllers/BaseApiController.cs
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Web.Http;
using Newtonsoft.Json;

namespace smartlunch_api.Controllers
{
    public abstract class BaseApiController : ApiController
    {
        

        protected HttpResponseMessage JsonOk(object data, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var resp = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(
                    JsonConvert.SerializeObject(data),
                    Encoding.UTF8,
                    "application/json")
            };

            return resp;
        }

        protected HttpResponseMessage JsonError(Exception ex, HttpStatusCode statusCode = HttpStatusCode.InternalServerError)
        {
            var msg =
                ex.InnerException?.InnerException?.Message ??
                ex.InnerException?.Message ??
                ex.Message;

            var resp = new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    success = false,
                    error = msg
                }))
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return resp;
        }

        protected HttpResponseMessage JsonError(string msg, HttpStatusCode code = HttpStatusCode.BadRequest)
        {
            var resp = new HttpResponseMessage(code)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new { error = msg }))
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return resp;
        }

        public class ReglaDeNegocioException : Exception
        {
            public ReglaDeNegocioException(string message) : base(message) { }
        }

        protected string GetUsername()
        {
            try
            {
                var identity = User?.Identity as ClaimsIdentity;

                // Si no hay usuario autenticado
                if (identity == null || !identity.IsAuthenticated)
                    return "Sistema";

                // 1) Claim estándar Name
                var name = identity.FindFirst(ClaimTypes.Name)?.Value;
                if (!string.IsNullOrEmpty(name))
                    return name;

                // 2) Claim "username" (por si lo pusiste así en el JWT)
                name = identity.FindFirst("username")?.Value;
                if (!string.IsNullOrEmpty(name))
                    return name;

                // 3) Fallback al .Name
                if (!string.IsNullOrEmpty(identity.Name))
                    return identity.Name;

                return "Sistema";
            }
            catch
            {
                return "Sistema";
            }
        }
    }
}
