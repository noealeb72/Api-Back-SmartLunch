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
    [RoutePrefix("api/turno")]
    public class TurnoController : BaseApiController
    {
        private readonly IServicioTurno _servicioTurno;

        public TurnoController(IServicioTurno servicioTurno)
        {
            _servicioTurno = servicioTurno ?? throw new ArgumentNullException(nameof(servicioTurno));
        }

        public TurnoController() : this(new ServicioTurno())
        {
        }

        // ===== GET api/turno/lista =====
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            string search = null,
            bool activo = true)
        {
            try
            {
                var result = _servicioTurno.ObtenerLista(page, pageSize, search, activo);
                return JsonOk(result);
            }
            catch
            {
                return JsonError("Error al obtener los turnos.", HttpStatusCode.InternalServerError);
            }
        }

        // ===== GET api/turno/{id} =====
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                var dto = _servicioTurno.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Turno no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch
            {
                return JsonError("Error al obtener el turno.", HttpStatusCode.InternalServerError);
            }
        }

        // ===== GET api/turno/activos-combo =====
        [HttpGet]
        [Route("activos-combo")]
        public HttpResponseMessage ObtenerActivosParaCombo()
        {
            try
            {
                var items = _servicioTurno.ObtenerActivosParaCombo();
                return JsonOk(items);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener turnos activos: {ex.Message}");
            }
        }

        // ===== POST api/turno/crear =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage CrearTurno([FromBody] TurnoCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var creado = _servicioTurno.CrearTurno(dto, username);
                return JsonOk(creado, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== PUT api/turno/actualizar =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage ActualizarTurno([FromBody] TurnoUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioTurno.ActualizarTurno(dto, username);

                return JsonOk(new { message = "Turno actualizado correctamente." });
            }
            catch (ReglaDeNegocioException ex)
            {
                // Errores esperados de validación → 400 + mensaje para mostrar en pantalla
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                // Errores de validación de negocio (como solapamiento de horarios) → 400 + mensaje real
                // El filtro global también manejará estos errores, pero aquí los capturamos primero
                if (ex.Message.Contains("superpone") || 
                    ex.Message.Contains("Ya existe") || 
                    ex.Message.Contains("obligatorio") ||
                    ex.Message.Contains("debe ser") ||
                    ex.Message.Contains("no encontrado") ||
                    ex.Message.Contains("no puede") ||
                    ex.Message.Contains("inválido") ||
                    ex.Message.Contains("Datos inválidos") ||
                    ex.Message.Contains("rango horario"))
                {
                    // Es un error de validación de negocio, devolver 400 con el mensaje real
                    return JsonError(ex.Message, HttpStatusCode.BadRequest);
                }
                // Error inesperado → 500 (el filtro global lo manejará)
                throw; // Re-lanzar para que el filtro global lo maneje
            }
        }


        // ===== POST api/turno/eliminar =====
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage EliminarTurno(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioTurno.EliminarTurno(id, username);
                return JsonOk(new { message = "Turno de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== POST api/turno/activar =====
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage ActivarTurno(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioTurno.ActivarTurno(id, username);
                return JsonOk(new { message = "Turno activado correctamente." });
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
