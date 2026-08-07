using System.Web.Http;
using System.Web.Http.Cors;
using System.Configuration;
using System.Data.SqlClient;

namespace smartlunch_api.Controllers
{
    [RoutePrefix("api/db")]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    public class DbController : ApiController
    {
        /// <summary>
        /// Verifica que la aplicación pueda conectar a la base de datos.
        /// GET api/db/ping (no requiere autenticación).
        /// Respuesta 200 + { "db": "ok" } = conexión correcta. Error 500 = fallo de conexión.
        /// </summary>
        [AllowAnonymous]
        [HttpGet, Route("ping")]
        public IHttpActionResult Ping()
        {
            var cs = ConfigurationManager.ConnectionStrings["DataContext"].ConnectionString;
            using (var cn = new SqlConnection(cs))
            {
                cn.Open(); // lanza excepción si no conecta
                return Ok(new { db = "ok" });
            }
        }
    }
}
