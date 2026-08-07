using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Cors;
using smartlunch_api.Dtos;
using smartlunch_api.Services;

namespace smartlunch_api.Controllers
{
    [Authorize]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/fichada")]
    public class FichadaController : BaseApiController
    {
        private readonly IServicioFichada _servicio;

        public FichadaController(IServicioFichada servicio)
        {
            _servicio = servicio ?? throw new ArgumentNullException(nameof(servicio));
        }

        public FichadaController() : this(new ServicioFichada())
        {
        }

        // ===================== LISTA =====================
        // GET api/fichada/lista
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            int? identificadorUsuario = null,
            int? turnoId = null,
            int? idDispositivo = null)
        {
            try
            {
                var result = _servicio.ObtenerLista(
                    page,
                    pageSize,
                    fechaDesde,
                    fechaHasta,
                    identificadorUsuario,
                    soloActivos: true
                );

                return JsonOk(result);
            }
            catch (Exception)
            {
                return JsonError("Error al obtener el listado de fichadas.",
                    HttpStatusCode.InternalServerError);
            }
        }

        // ===================== DETALLE =====================
        // GET api/fichada/{id}
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                var dto = _servicio.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Fichada no encontrada.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch (Exception)
            {
                return JsonError("Error al obtener la fichada.",
                    HttpStatusCode.InternalServerError);
            }
        }

        // ===================== CREAR =====================
        // POST api/fichada/crear
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage Crear([FromBody] FichadaCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var creada = _servicio.CrearFichada(dto);
                return JsonOk(creada, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===================== Helper usuario (por si luego lo necesitás) =====================
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
