using System.Net;
using System.Net.Http;
using System.Web;
using System.Web.Http;

namespace smartlunch_api.Controllers
{
    /// <summary>
    /// Redirige a la UI de Swagger. GET /api/swagger/enter -> /swagger/ui/index
    /// </summary>
    [AllowAnonymous]
    [RoutePrefix("api/swagger")]
    public class SwaggerUiController : ApiController
    {
        /// <summary>
        /// Redirige a la UI de Swagger.
        /// </summary>
        [HttpGet]
        [Route("enter")]
        public HttpResponseMessage Enter()
        {
            var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            var redirectUrl = baseUrl.TrimEnd('/') + "/swagger/ui/index";
            var response = Request.CreateResponse(HttpStatusCode.Redirect);
            response.Headers.Location = new System.Uri(redirectUrl);
            return response;
        }
    }
}
