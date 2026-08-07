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
    [RoutePrefix("api/proyecto")]
    public class ProyectoController : BaseApiController
    {
        private readonly IServicioProyecto _servicioProyecto;

        public ProyectoController(IServicioProyecto servicioProyecto)
        {
            _servicioProyecto = servicioProyecto ?? throw new ArgumentNullException(nameof(servicioProyecto));
        }

        public ProyectoController() : this(new ServicioProyecto())
        {
        }

        // ===== GET api/proyecto/lista =====
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            string search = null,
            int? plantaId = null,
            int? centroCostoId = null,
            bool activo = true)
        {
            try
            {
                var result = _servicioProyecto.ObtenerLista(
                    page,
                    pageSize,
                    search,
                    activo);

                return JsonOk(result);
            }
            catch
            {
                return JsonError("Error al obtener los proyectos.", HttpStatusCode.InternalServerError);
            }
        }

        // ===== GET api/proyecto/{id} =====
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                var dto = _servicioProyecto.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Proyecto no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch
            {
                return JsonError("Error al obtener el proyecto.", HttpStatusCode.InternalServerError);
            }
        }

        // ===== GET api/proyecto/{id}/validar-usuarios (solo Admin y Gerencia) =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("{id:int}/validar-usuarios")]
        public HttpResponseMessage ValidarCantidadUsuarios(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var resultado = _servicioProyecto.ValidarCantidadUsuarios(id);
                return JsonOk(resultado);
            }
            catch (Exception ex) when (ex.Message.Contains("Proyecto no encontrado"))
            {
                return JsonError(ex.Message, HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== GET api/proyecto/activos-combo =====
        [HttpGet]
        [Route("activos-combo")]
        public HttpResponseMessage ObtenerActivosParaCombo(
            int? plantaId = null,
            int? centroCostoId = null)
        {
            try
            {
                var items = _servicioProyecto.ObtenerActivosParaCombo(plantaId, centroCostoId);
                return JsonOk(items);
            }
            catch
            {
                return JsonError("Error al obtener proyectos para combo.", HttpStatusCode.InternalServerError);
            }
        }

        // ===== POST api/proyecto/crear =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage CrearProyecto([FromBody] ProyectoCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var creado = _servicioProyecto.CrearProyecto(dto, username);
                return JsonOk(creado, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== PUT api/proyecto/actualizar =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage ActualizarProyecto([FromBody] ProyectoUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioProyecto.ActualizarProyecto(dto, username);
                return JsonOk(new { message = "Proyecto actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== POST api/proyecto/eliminar =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage EliminarProyecto(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioProyecto.EliminarProyecto(id, username);
                return JsonOk(new { message = "Proyecto de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== POST api/proyecto/activar =====
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage ActivarProyecto(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioProyecto.ActivarProyecto(id, username);
                return JsonOk(new { message = "Proyecto activado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== helper usuario logueado =====
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
