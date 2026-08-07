using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    [Authorize]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/plannutricional")]
    public class PlannutricionalController : BaseApiController
    {
        private readonly IServicioPlanNutricional _servicio;

        public PlannutricionalController(IServicioPlanNutricional servicio)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        }

        public PlannutricionalController() : this(new ServicioPlanNutricional())
        {
        }

        // GET api/plannutricional/lista
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage Listar(int page = 1, int pageSize = 10, string search = null, bool activo = true)
        {
            try
            {
                var result = _servicio.ObtenerLista(page, pageSize, search, activo);
                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        // GET api/plannutricional/{id}
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage Obtener(int id)
        {
            try
            {
                var dto = _servicio.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Plan nutricional no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.InternalServerError);
            }
        }

        // GET api/plannutricional/{id}/validar-usuarios (solo Admin y Gerencia)
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("{id:int}/validar-usuarios")]
        public HttpResponseMessage ValidarCantidadUsuarios(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var resultado = _servicio.ValidarCantidadUsuarios(id);
                return JsonOk(resultado);
            }
            catch (Exception ex) when (ex.Message.Contains("Plan nutricional no encontrado"))
            {
                return JsonError(ex.Message, HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/plannutricional/crear
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage Crear([FromBody] PlanNutricionalCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var creado = _servicio.Crear(dto, username);
                return JsonOk(creado, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // PUT api/plannutricional/actualizar
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage Actualizar([FromBody] PlanNutricionalUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicio.Actualizar(dto, username);
                return JsonOk(new { message = "Plan nutricional actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/plannutricional/baja
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage Baja(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicio.Eliminar(id, username);
                return JsonOk(new { message = "Plan nutricional dado de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/plannutricional/activar
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage Activar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicio.Activar(id, username);
                return JsonOk(new { message = "Plan nutricional activado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== helper para usuario logueado =====
        private string ObtenerNombreUsuario()
        {
            try
            {
                var identity = User?.Identity as ClaimsIdentity;
                if (identity == null || !identity.IsAuthenticated)
                    return "Sistema";

                var name = identity.FindFirst(ClaimTypes.Name)?.Value
                           ?? identity.FindFirst("username")?.Value;

                return string.IsNullOrEmpty(name) ? "Sistema" : name;
            }
            catch
            {
                return "Sistema";
            }
        }
    }
}
