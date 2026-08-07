using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;

namespace smartlunch_api.Controllers
{
    [Authorize]
    [RoutePrefix("api/reglabonificacion")]
    public class ReglaBonificacionController : BaseApiController
    {
        private readonly IServicioReglaBonificacion _service;

        public ReglaBonificacionController(IServicioReglaBonificacion service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ReglaBonificacionController() : this(new ServicioReglaBonificacion())
        {
        }

        // GET api/reglabonificacion/lista
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage Lista(
            int page = 1,
            int pageSize = 10,
            string search = null,
            bool activo = true)
        {
            try
            {
                var result = _service.ObtenerLista(page, pageSize, search, activo);
                return JsonOk(result);
            }
            catch
            {
                return JsonError("Error al obtener las reglas de bonificación.", HttpStatusCode.InternalServerError);
            }
        }

        // GET api/reglabonificacion/{id}
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage Detalle(int id)
        {
            try
            {
                var dto = _service.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Regla no encontrada.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch
            {
                return JsonError("Error al obtener la regla.", HttpStatusCode.InternalServerError);
            }
        }

        // POST api/reglabonificacion/crear
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage Crear([FromBody] ReglaBonificacionCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var user = ObtenerNombreUsuario();
                var creada = _service.Crear(dto, user);
                return JsonOk(creada, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // PUT api/reglabonificacion/actualizar
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage Actualizar([FromBody] ReglaBonificacionUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var user = ObtenerNombreUsuario();
                _service.Actualizar(dto, user);
                return JsonOk(new { message = "Regla actualizada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/reglabonificacion/eliminar
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("eliminar")]
        public HttpResponseMessage Eliminar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var user = ObtenerNombreUsuario();
                _service.Eliminar(id, user);
                return JsonOk(new { message = "Regla de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/reglabonificacion/activar
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage Activar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var user = ObtenerNombreUsuario();
                _service.Activar(id, user);
                return JsonOk(new { message = "Regla activada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ========== Helper para auditoría ==========
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
