using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;



namespace smartlunch_api.Controllers
{
    // Sin [EnableCors] propio: usa la política global (lista blanca de orígenes) configurada
    // en WebApiConfig.cs, igual que el resto de los controllers. Antes tenía origins: "*",
    // que exponía estos endpoints (incluida la creación de pedidos) a cualquier sitio web.
    [Authorize]
    [RoutePrefix("api/comanda")]
    public class ComandaController : BaseApiController
    {
        private readonly IServicioComanda _service;

        public ComandaController(IServicioComanda service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public ComandaController() : this(new ServicioComanda())
        {
        }

        /// <summary>
        /// Obtiene una lista paginada de comandas con filtros opcionales
        /// </summary>
        /// <param name="page">Número de página (por defecto: 1)</param>
        /// <param name="pageSize">Tamaño de página (por defecto: 20 para ver más registros en despacho; máx 100)</param>
        /// <param name="fechaDesde">Fecha desde para filtrar</param>
        /// <param name="fechaHasta">Fecha hasta para filtrar</param>
        /// <param name="usuarioId">Filtrar por ID de usuario</param>
        /// <param name="turnoId">Filtrar por ID de turno</param>
        /// <param name="plantaId">Filtrar por ID de planta</param>
        /// <param name="centroCostoId">Filtrar por ID de centro de costo</param>
        /// <param name="proyectoId">Filtrar por ID de proyecto</param>
        /// <param name="jerarquiaId">Filtrar por ID de jerarquía</param>
        /// <param name="estado">Filtrar por estado de la comanda</param>
        /// <returns>Lista paginada de comandas</returns>
        /// <response code="200">Lista obtenida exitosamente</response>
        /// <response code="401">No autorizado</response>
        /// <response code="500">Error interno del servidor</response>
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage Lista(
            int page = 1,
            int pageSize = 10,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            int? usuarioId = null,
            int? turnoId = null,
            int? plantaId = null,
            int? centroCostoId = null,
            int? proyectoId = null,
            int? jerarquiaId = null,
            string estado = null,
             string search = null)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 20;

                var result = _service.ObtenerLista(
                    page, 
                    pageSize,
                    fechaDesde, 
                    fechaHasta,
                    usuarioId,
                    search,
                    soloActivos: true,
                    estado: estado);

                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener las comandas: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene el detalle de una comanda por su ID
        /// </summary>
        /// <param name="id">ID de la comanda</param>
        /// <returns>Detalle de la comanda</returns>
        /// <response code="200">Comanda encontrada</response>
        /// <response code="401">No autorizado</response>
        /// <response code="404">Comanda no encontrada</response>
        /// <response code="500">Error interno del servidor</response>
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage Detalle(int id)
        {
            try
            {
                if (id <= 0)
                    return JsonError("El ID debe ser mayor a 0.", HttpStatusCode.BadRequest);

                var dto = _service.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Comanda no encontrada.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener la comanda: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Crea una nueva comanda
        /// </summary>
        /// <param name="dto">Datos de la comanda a crear</param>
        /// <returns>Comanda creada</returns>
        /// <response code="201">Comanda creada exitosamente</response>
        /// <response code="400">Datos inválidos o errores de validación</response>
        /// <response code="401">No autorizado</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpPost]
        [Route("crear")]
        public IHttpActionResult Crear([FromBody] ComandaCreateDto dto, int usuarioId)
        {
            try
            {
                // El usuarioId siempre sale del token, nunca del parámetro que manda el cliente:
                // si no, cualquier usuario autenticado podría pedir en nombre de otro pasando su Id.
                var usuarioIdToken = GetUsuarioIdFromToken();
                if (usuarioIdToken <= 0)
                    return Content(HttpStatusCode.Unauthorized, new { ok = false, message = "Token inválido o sin usuario." });

                var result = _service.Crear(dto, usuarioIdToken); // debe sumar menudd.comandas + 1 adentro del service
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Crea una nueva comanda calculando automáticamente el monto según bonificaciones.
        /// El monto se calcula basándose en:
        /// - Bonificación por día del menú (si existe)
        /// - Bonificación de la jerarquía (si no hay bonificación por día)
        /// - Flag bonificado del DTO
        /// </summary>
        /// <param name="dto">Datos de la comanda a crear (el campo Monto será ignorado y calculado automáticamente)</param>
        /// <param name="usuarioId">ID del usuario que crea la comanda</param>
        /// <returns>Comanda creada con monto calculado</returns>
        /// <response code="201">Comanda creada exitosamente</response>
        /// <response code="400">Datos inválidos o errores de validación</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpPost]
        [Route("crear-con-descuento")]
        public IHttpActionResult CrearConDescuento([FromBody] ComandaCreateDto dto, int usuarioId)
        {
            try
            {
                // El usuarioId siempre sale del token, nunca del parámetro que manda el cliente:
                // si no, cualquier usuario autenticado podría pedir en nombre de otro pasando su Id.
                var usuarioIdToken = GetUsuarioIdFromToken();
                if (usuarioIdToken <= 0)
                    return Content(HttpStatusCode.Unauthorized, new { ok = false, message = "Token inválido o sin usuario." });

                var result = _service.CrearConDescuento(dto, usuarioIdToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    message = ex.Message
                });
            }
        }

        /// <summary>
        /// Previsualiza, para cada ítem del menú del día de hoy, el precio que le
        /// correspondería al usuario si lo pidiera ahora mismo según las reglas de
        /// bonificación activas. No crea ninguna comanda; es de solo lectura.
        /// </summary>
        [HttpGet]
        [Route("previsualizar-bonificacion")]
        public HttpResponseMessage PrevisualizarBonificacion(int usuarioId)
        {
            try
            {
                // El usuarioId siempre sale del token, nunca del parámetro que manda el cliente.
                var usuarioIdToken = GetUsuarioIdFromToken();
                if (usuarioIdToken <= 0)
                    return JsonError("Token inválido o sin usuario.", HttpStatusCode.Unauthorized);

                var result = _service.PrevisualizarBonificacion(usuarioIdToken);
                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al previsualizar la bonificación: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        // PUT api/comanda/actualizar
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage Actualizar([FromBody] ComandaUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                _service.Actualizar(dto);
                return JsonOk(new { message = "Comanda actualizada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/comanda/eliminar
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("eliminar")]
        public HttpResponseMessage Eliminar([FromBody] ComandaAccionDto dto)
        {
            if (dto == null || dto.Npedido <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                _service.Eliminar(dto.Npedido);
                return JsonOk(new { message = "Comanda eliminada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST api/comanda/activar
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage Activar([FromBody] ComandaAccionDto dto)
        {
            if (dto == null || dto.Npedido <= 0)
                return JsonError("Pedido inválido.", HttpStatusCode.BadRequest);

            try
            {
                _service.Activar(dto.Npedido);
                return JsonOk(new { message = "Comanda activada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // PUT api/comanda/cancelar - Cualquier usuario autenticado puede cancelar su pedido
        [HttpPut]
        [Route("cancelar")]
        public HttpResponseMessage Cancelar([FromBody] ComandaAccionDto dto)
        {
            if (dto == null || dto.Npedido <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);

            try
            {
                _service.Cancelar(dto.Npedido);
                return JsonOk(new { message = "Comanda cancelada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        // =========================
        // DEVOLVER -> D (Devuelto). Cualquier usuario autenticado puede devolver pedido.
        // PUT: /api/comanda/{npedido}/devolver
        // =========================
        [HttpPut]
        [Route("{npedido:int}/devolver")]
        public HttpResponseMessage Devolver([FromBody] ComandaAccionDto dto)
        {
            if (dto == null || dto.Npedido <= 0)
                return JsonError("Pedido inválido.", HttpStatusCode.BadRequest);
            try
            {
                _service.Devolver(dto.Npedido, dto.Calificacion); // si corresponde, resta menudd.comandas - 1 adentro del service
                return JsonOk(new { message = "Comanda devuelta correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        // =========================
        // DESPACHAR -> E (En Aceptación)
        // PUT: /api/comanda/{npedido}/despachar
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPut]
        [Route("despachar")]
        public HttpResponseMessage Despachar([FromBody] ComandaAccionDto dto)
        {
            if (dto == null || dto.Npedido <= 0)
                return JsonError("Id inválido.", HttpStatusCode.BadRequest);
            try
            {
                var result = _service.Despachar(dto.Npedido);
                return JsonOk(new { message = "Comanda despachada correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }
        // =========================
        // RECIBIR -> R (Recibido). Todas las jerarquías (roles) pueden recibir pedido.
        // PUT: /api/comanda/{npedido}/recibir
        // =========================
        [HttpPut]
        [Route("{npedido:int}/recibir")]
        public IHttpActionResult Recibir([FromBody] ComandaAccionDto dto)
        {
            if (dto == null || dto.Npedido <= 0)
                return Content(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    message = "Pedido inválido."
                });
            try
            {
                var result = _service.Recibir(dto.Npedido, dto.Calificacion);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    message = ex.Message
                });
            }
        }
        // ============================================
        // Helper usuario
        // ============================================
        private int GetUsuarioIdFromToken()
        {
            try
            {
                var identity = User?.Identity as ClaimsIdentity;
                if (identity == null || !identity.IsAuthenticated)
                    return 0;

                var claim = identity.Claims.FirstOrDefault(c => c.Type == "usuario");
                if (claim == null)
                    return 0;

                if (int.TryParse(claim.Value, out int usuarioId))
                    return usuarioId;

                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }
}
