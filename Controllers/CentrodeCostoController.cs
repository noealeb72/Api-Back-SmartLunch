using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    //////[EnableCors(origins: "*", headers: "*", methods: "*")]
    [Authorize]
    [RoutePrefix("api/centrodecosto")]
    public class CentroDeCostoController : BaseApiController
    {
        private readonly IServicioCentroDeCosto _service;

        public CentroDeCostoController(IServicioCentroDeCosto service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public CentroDeCostoController() : this(new ServicioCentroDeCosto())
        {
        }

        // ===================== LISTA PAGINADA =====================
        // GET api/centrodecosto/lista?page=1&pageSize=10&search=abc&soloActivos=true
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
            catch (Exception)
            {
                return JsonError("Error al obtener los centros de costo.", HttpStatusCode.InternalServerError);
            }
        }

        // ===================== DETALLE =====================
        // GET api/centrodecosto/5
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage Detalle(int id)
        {
            try
            {
                var dto = _service.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Centro de costo no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch (Exception)
            {
                return JsonError("Error al obtener el centro de costo.", HttpStatusCode.InternalServerError);
            }
        }

        // ===================== VALIDAR USUARIOS (solo Admin y Gerencia) =====================
        // GET api/centrodecosto/{id}/validar-usuarios
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("{id:int}/validar-usuarios")]
        public HttpResponseMessage ValidarCantidadUsuarios(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var resultado = _service.ValidarCantidadUsuarios(id);
                return JsonOk(resultado);
            }
            catch (Exception ex) when (ex.Message.Contains("Centro de costo no encontrado"))
            {
                return JsonError(ex.Message, HttpStatusCode.NotFound);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===================== CREAR =====================
        // POST api/centrodecosto/crear
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage Crear([FromBody] CentroDeCostoCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = GetUsername();
                var creado = _service.Crear(dto, username);

                return JsonOk(creado, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===================== ACTUALIZAR =====================
        // PUT api/centrodecosto/actualizar
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage Actualizar([FromBody] CentroDeCostoUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = GetUsername();
                _service.Actualizar(dto, username);

                return JsonOk(new { message = "Centro de costo actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===================== BAJA LÓGICA =====================
        // POST api/centrodecosto/baja
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage Eliminar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var username = GetUsername();
                _service.Eliminar(id, username);

                return JsonOk(new { message = "Centro de costo dado de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===================== ACTIVAR =====================
        // POST api/centrodecosto/activar
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage Activar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                var username = GetUsername();
                _service.Activar(id, username);

                return JsonOk(new { message = "Centro de costo activado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }
    }
}
