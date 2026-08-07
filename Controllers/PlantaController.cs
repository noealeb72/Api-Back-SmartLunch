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
    [RoutePrefix("api/planta")]
    public class PlantaController : BaseApiController
    {
        private readonly IServicioPlanta _servicioPlanta;

        public PlantaController(IServicioPlanta servicioPlanta)
        {
            _servicioPlanta = servicioPlanta ?? throw new ArgumentNullException(nameof(servicioPlanta));
        }

        public PlantaController() : this(new ServicioPlanta())
        {
        }

        // ======================================================
        // GET api/planta/lista?page=1&pageSize=10&search=xxx&activo=true
        // Lista con paginado + buscador
        // ======================================================
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
                var result = _servicioPlanta.ObtenerLista(page, pageSize, search, activo);
                return JsonOk(result);
            }
            catch
            {
                return JsonError("Error al obtener la lista de plantas.", HttpStatusCode.InternalServerError);
            }
        }

        // ======================================================
        // GET api/planta/{id}
        // Detalle de planta
        // ======================================================
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                var planta = _servicioPlanta.ObtenerPorId(id);
                if (planta == null)
                    return JsonError("Planta no encontrada.", HttpStatusCode.NotFound);

                return JsonOk(planta);
            }
            catch
            {
                return JsonError("Error al obtener la planta.", HttpStatusCode.InternalServerError);
            }
        }

        // ======================================================
        // GET api/planta/{id}/validar-usuarios
        // Valida cantidad de usuarios asociados a la planta (solo Admin y Gerencia)
        // ======================================================
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("{id:int}/validar-usuarios")]
        public HttpResponseMessage ValidarCantidadUsuarios(int id)
        {
            if (id <= 0)
                return JsonError("Id de planta inválido.", HttpStatusCode.BadRequest);

            try
            {
                var resultado = _servicioPlanta.ValidarCantidadUsuarios(id);
                return JsonOk(resultado);
            }
            catch (Exception ex) when (ex.Message.Contains("Planta no encontrada"))
            {
                return JsonError(ex.Message, HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ======================================================
        // GET api/planta/buscar?termino=xx&activo=true&maxResultados=20
        // Buscador liviano (combos, autocomplete)
        // ======================================================
        [HttpGet]
        [Route("buscar")]
        public HttpResponseMessage BuscarPlantas(
            string termino,
            bool activo = true,
            int maxResultados = 20)
        {
            try
            {
                var items = _servicioPlanta.Buscar(termino, activo, maxResultados);
                return JsonOk(items);
            }
            catch
            {
                return JsonError("Error al buscar plantas.", HttpStatusCode.InternalServerError);
            }
        }

        // ======================================================
        // POST api/planta/crear
        // Crear planta
        // ======================================================
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage CrearPlanta([FromBody] Models.DTOs.PlantaCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var planta = _servicioPlanta.CrearPlanta(dto, username);
                return JsonOk(planta);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ======================================================
        // PUT api/planta/actualizar
        // Actualizar planta
        // ======================================================
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage ActualizarPlanta([FromBody] Models.DTOs.PlantaUpdateDto dto)
        {
            if (dto == null || dto.id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioPlanta.ActualizarPlanta(dto, username);

                return JsonOk(new { message = "Planta actualizada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ======================================================
        // POST api/planta/eliminar
        // Baja lógica
        // ======================================================
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage EliminarPlanta(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioPlanta.EliminarPlanta(id, username);

                return JsonOk(new { message = "Planta de baja corectamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }        

        // ======================================================
        // POST api/planta/activar
        // Reactivar planta
        // ======================================================
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage ActivarPlanta(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioPlanta.ActivarPlanta(id, username);

                return JsonOk(new { message = "Planta activada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }



        // ======================================================
        // Helpers
        // ======================================================
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
