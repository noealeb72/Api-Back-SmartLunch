using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    [Authorize]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/menudd")]
    public class MenuddController : BaseApiController
    {
        private readonly IServicioMenudd _servicioMenudd;
        private readonly IServicioUsuario _servicioUsuario;
        private readonly IServicioComanda _servicioComanda;

        public MenuddController(IServicioMenudd servicioMenudd, IServicioUsuario servicioUsuario, IServicioComanda servicioComanda)
        {
            _servicioMenudd = servicioMenudd ?? throw new ArgumentNullException(nameof(servicioMenudd));
            _servicioUsuario = servicioUsuario ?? throw new ArgumentNullException(nameof(servicioUsuario));
            _servicioComanda = servicioComanda ?? throw new ArgumentNullException(nameof(servicioComanda));
        }

        public MenuddController() : this(new ServicioMenudd(), new ServicioUsuario(), new ServicioComanda())
        {
        }

        // ============================================
        // GET api/menudd/lista
        // ============================================
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            string search = null,
            bool activo = true,
            int? plantaId = null)
        {
            try
            {
                var result = _servicioMenudd.ObtenerLista(
                    page,
                    pageSize,
                    fechaDesde,
                    fechaHasta,
                    search,
                    activo,
                    plantaId
                );

                return JsonOk(result);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                }
                return JsonError($"Error al obtener el listado de menú del día: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }

        // ============================================
        // GET api/menudd/{id}
        // ============================================
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                var item = _servicioMenudd.ObtenerPorId(id);
                if (item == null)
                    return JsonError("Menú del día no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(item);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                }
                return JsonError($"Error al obtener el menú del día: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }

        // ============================================
        // POST api/menudd/crear
        // ============================================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage CrearMenu([FromBody] MenuddCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var creado = _servicioMenudd.CrearMenu(dto, username);

                return JsonOk(creado);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============================================
        // POST api/menudd/crear-multiples
        // Inserta varios menús del día (uno por uno)
        // ============================================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("crear-multiples")]
        public HttpResponseMessage CrearMenusMultiples([FromBody] MenuddCrearMultiplesRequestDto request)
        {
            if (request == null)
                return JsonError("Datos inválidos. Envíe un objeto con la propiedad 'menus' (array).", HttpStatusCode.BadRequest);

            if (request.Menus == null || request.Menus.Count == 0)
                return JsonError("El array 'menus' no puede estar vacío.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var resultado = _servicioMenudd.CrearMenusMultiples(request, username);

                return JsonOk(resultado);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============================================
        // PUT api/menudd/actualizar
        // ============================================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage ActualizarMenu([FromBody] MenuddUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioMenudd.ActualizarMenu(dto, username);

                return JsonOk(new { message = "Menú del día actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============================================
        // POST api/menudd/eliminar
        // ============================================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage EliminarMenu(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioMenudd.EliminarMenu(id, username);

                return JsonOk(new { message = "Menú dado de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage ActivarMenu(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioMenudd.ActivarMenu(id, username);

                return JsonOk(new { message = "Menú activo correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============================================
        // GET api/menudd/por-turno
        // ============================================
        [HttpGet]
        [Route("por-turno")]
        public HttpResponseMessage ObtenerMenuPorTurno(
            DateTime fecha,
            int? plantaId,
            int turnoId,
            int? centroCostoId = null,
            int? proyectoId = null,
            int? jerarquiaId = null,
            int? nutricionalId = null,
            bool soloConStock = true)
        {
            try
            {
                // Validar que plantaId no sea null
                if (!plantaId.HasValue || plantaId.Value <= 0)
                    return JsonError("El ID de planta es obligatorio.", HttpStatusCode.BadRequest);

                // Obtener usuarioId del token
                int usuarioId = GetUsuarioIdFromToken();

                // Normalizar fechas para las comandas
                DateTime fechaDesde = fecha.Date;
                DateTime fechaHasta = fecha.Date.AddDays(1).AddTicks(-1); // Fin del día

                // Llamar a ServicioMenudd para obtener el menú del día
                // Pasar usuarioId para que calcule el Estado de cada plato
                var menuDelDia = _servicioMenudd.ObtenerMenuPorTurno(
                    fecha,
                    plantaId.Value,
                    turnoId,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    nutricionalId,
                    soloConStock,
                    usuarioId > 0 ? (int?)usuarioId : null, // usuarioId para calcular Estado
                    null // estadoComanda (sin filtro)
                );

                // Llamar a ServicioComanda para obtener las comandas (solo si hay usuario autenticado)
                var comandas = new List<ComandaDetalleDto>();
                if (usuarioId > 0)
                {
                    comandas = _servicioComanda.ObtenerXDia(
                        fechaDesde,
                        fechaHasta,
                        usuarioId,
                        turnoId,
                        plantaId.Value,
                        centroCostoId ?? 0,
                        proyectoId ?? 0,
                        jerarquiaId ?? 0,
                        null // estado (sin filtro)
                    );
                }

                // Devolver DTO combinado
                var resultado = new MenuYComandasPorTurnoDto
                {
                    MenuDelDia = menuDelDia,
                    Comandas = comandas
                };

                return JsonOk(resultado);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener el menú del día y comandas por turno: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }
        // ============================================
        // GET api/menudd/por-turno
        // ============================================
        [HttpGet]
        [Route("turno")]
        public HttpResponseMessage ObtenerPorTurno(int turnoId, bool soloConStock = true, int? plantaId = null)
        {
            try
            {
                int usuarioId = GetUsuarioIdFromToken();
                if (usuarioId <= 0)
                    return JsonError("Usuario no identificado.", HttpStatusCode.Unauthorized);

                var usuario = _servicioUsuario.ObtenerPorId(usuarioId);

                if (usuarioId == 0)
                    return JsonError("Sin datos para ese usuario.", HttpStatusCode.Unauthorized);

                var fecha = DateTime.Now;

                // Por defecto, el comedor propio del usuario. Si pide otro (ej. cocinero viendo
                // otro comedor), solo se acepta si realmente hay menú cargado ahí para su jerarquía
                // y turno — no se confía en el plantaId del cliente a ciegas.
                int plantaIdAUsar = (int)usuario.PlantaId;
                if (plantaId.HasValue && plantaId.Value > 0 && plantaId.Value != plantaIdAUsar)
                {
                    var comedoresDisponibles = _servicioMenudd.ObtenerComedoresDisponibles(
                        fecha,
                        turnoId,
                        usuario.CentroCostoId,
                        usuario.ProyectoId,
                        usuario.JerarquiaId,
                        usuario.Plannutricional_id,
                        soloConStock
                    );

                    if (!comedoresDisponibles.Any(c => c.PlantaId == plantaId.Value))
                        return JsonError("No hay menú cargado para ese comedor en este turno.", HttpStatusCode.NotFound);

                    plantaIdAUsar = plantaId.Value;
                }

                var items = _servicioMenudd.ObtenerMenuPorTurno(
                    fecha,
                    plantaIdAUsar,
                    turnoId,
                    usuario.CentroCostoId,
                    usuario.ProyectoId,
                    usuario.JerarquiaId,
                    usuario.Plannutricional_id,//plan nutricional
                    soloConStock
                );

                return JsonOk(items);
            }
            catch
            {
                return JsonError("Error al obtener el menú del día por turno.", HttpStatusCode.InternalServerError);
            }
        }

        // ============================================
        // GET api/menudd/comedores-turno
        // Comedores con menú real cargado para el turno/jerarquía del usuario logueado
        // ============================================
        [HttpGet]
        [Route("comedores-turno")]
        public HttpResponseMessage ObtenerComedoresDisponibles(int turnoId, bool soloConStock = true)
        {
            try
            {
                int usuarioId = GetUsuarioIdFromToken();
                if (usuarioId <= 0)
                    return JsonError("Usuario no identificado.", HttpStatusCode.Unauthorized);

                var usuario = _servicioUsuario.ObtenerPorId(usuarioId);
                if (usuario == null)
                    return JsonError("Sin datos para ese usuario.", HttpStatusCode.Unauthorized);

                var fecha = DateTime.Now;

                var comedores = _servicioMenudd.ObtenerComedoresDisponibles(
                    fecha,
                    turnoId,
                    usuario.CentroCostoId,
                    usuario.ProyectoId,
                    usuario.JerarquiaId,
                    usuario.Plannutricional_id,
                    soloConStock
                );

                return JsonOk(comedores);
            }
            catch
            {
                return JsonError("Error al obtener los comedores disponibles.", HttpStatusCode.InternalServerError);
            }
        }

        // ============================================
        // POST api/menudd/impresion
        // ============================================
        [HttpPost]
        [Route("impresion")]
        public HttpResponseMessage ObtenerDatosImpresion([FromBody] MenuddImpresionRequestDto request)
        {
            if (request == null)
                return JsonError("La solicitud de impresión no puede ser nula.", HttpStatusCode.BadRequest);

            try
            {
                var datos = _servicioMenudd.ObtenerDatosImpresion(request);
                return JsonOk(datos);
            }
            catch (ArgumentNullException ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                }
                return JsonError($"Error al obtener los datos para impresión de menú del día: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }

        // ============================================
        // Helper usuario
        // ============================================
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

        private int GetUsuarioIdFromToken()
        {
            var identity = User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == "usuario");
            return claim != null ? int.Parse(claim.Value) : 0;
        }
    }
}
