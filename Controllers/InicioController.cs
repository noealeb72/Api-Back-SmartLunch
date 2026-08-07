using smartlunch_api.Models;
using smartlunch_api.Models.DTOs;
using smartlunch_api.Service;
using smartlunch_api.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [Authorize]
    [RoutePrefix("api/inicio")]
    public class InicioController : BaseApiController
    {
        private readonly ServicioInicio _inicioService = new ServicioInicio();
        private readonly IServicioUsuario _servicioUsuario = new ServicioUsuario();


        // =====================================
        // GET api/dashboard/inicio
        // Turnos + pedidos de hoy (no cancelados)
        // =====================================
        [HttpGet]
        [Route("web")]
        public HttpResponseMessage ObtenerBaseWeb()
        {
            try
            {
                int usuarioId = GetUsuarioIdFromToken();
                if (usuarioId <= 0)
                    return JsonError("Usuario no identificado.", HttpStatusCode.Unauthorized);

                var turnos = _inicioService.ObtenerWeb(usuarioId);
                return JsonOk(turnos);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener datos de inicio: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene datos de inicio web actualizados para un turno específico
        /// Similar a /api/inicio/web pero permite especificar el turno seleccionado
        /// Ideal para actualización periódica cada 2 segundos
        /// </summary>
        /// <param name="turnoId">ID del turno seleccionado (query 'turno'). Si no está disponible o es 0, se usa el primer turno.</param>
        /// <param name="fecha">Fecha (opcional, por defecto hoy). Formato: YYYY-MM-DD</param>
        /// <returns>Datos de inicio: Usuario, Turnos, Menú del día, Comandas del turno seleccionado</returns>
        [HttpGet]
        [Route("web-actualizado")]
        public HttpResponseMessage ObtenerWebActualizado([FromUri(Name = "turno")] int turnoId = 0, [FromUri] string fecha = null)
        {
            try
            {
                int usuarioId = GetUsuarioIdFromToken();
                if (usuarioId <= 0)
                    return JsonError("Usuario no identificado.", HttpStatusCode.Unauthorized);

                // Parsear fecha si se proporciona
                DateTime? fechaParam = null;
                if (!string.IsNullOrWhiteSpace(fecha))
                {
                    // Usar TryParseExact para asegurar que siempre se use la fecha del día correcto
                    if (DateTime.TryParseExact(fecha, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaParsed))
                        fechaParam = fechaParsed.Date;
                    else
                        return JsonError("Formato de fecha inválido. Use YYYY-MM-DD.", HttpStatusCode.BadRequest);
                }

                var resultado = _inicioService.ObtenerWebActualizado(usuarioId, turnoId, fechaParam);
                return JsonOk(resultado);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener datos de inicio actualizados: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene datos de inicio para tótem físico
        /// Este endpoint es público (no requiere autenticación) porque el tótem solo tiene el legajo de la tarjeta
        /// </summary>
        /// <param name="legajo">Número de legajo del usuario (obtenido de la tarjeta)</param>
        /// <returns>Datos de inicio: Usuario, Turnos, Menú del día</returns>
        [HttpGet]
        [AllowAnonymous] // Tótem físico no tiene token, solo legajo de la tarjeta
        [Route("totem")]
        public HttpResponseMessage ObtenerTotem(int legajo)
        {
            try
            {
                if (legajo <= 0)
                    return JsonError("El legajo debe ser mayor a cero.", HttpStatusCode.BadRequest);

                // Usar ServicioUsuario para obtener el usuario por legajo (ya tiene toda la lógica)
                var usuarioBase = _servicioUsuario.ObtenerPorLegajo(legajo);

                if (usuarioBase == null)
                    return JsonError($"No se encontró un usuario activo con el legajo {legajo}.", HttpStatusCode.NotFound);

                // Validar que el usuario esté activo
                if (!usuarioBase.Activo)
                    return JsonError($"El usuario con legajo {legajo} está inactivo.", HttpStatusCode.Forbidden);

                // Pasar el usuario completo al servicio (evita buscar de nuevo en BD)
                var resultado = _inicioService.ObtenerTotem(usuarioBase);
                
                if (resultado == null)
                    return JsonError("Error al obtener datos de inicio.", HttpStatusCode.InternalServerError);

                return JsonOk(resultado);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener datos de inicio: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene datos de inicio para tótem actualizados para un turno específico
        /// Similar a /api/inicio/web-actualizado pero para tótem (no requiere autenticación)
        /// Ideal para actualización periódica del tótem cada 2 segundos
        /// </summary>
        /// <param name="legajo">Número de legajo del usuario (obtenido de la tarjeta)</param>
        /// <param name="turnoId">ID del turno seleccionado</param>
        /// <param name="fecha">Fecha (opcional, por defecto hoy). Formato: YYYY-MM-DD</param>
        /// <returns>Datos de inicio: Usuario, Turnos, Menú del día, Comandas del turno seleccionado</returns>
        [HttpGet]
        [AllowAnonymous] // Tótem físico no tiene token, solo legajo de la tarjeta
        [Route("totem-actualizado")]
        public HttpResponseMessage ObtenerTotemActualizado(int legajo, [FromUri(Name = "turno")] int turnoId = 0, string fecha = null)
        {
            try
            {
                if (legajo <= 0)
                    return JsonError("El legajo debe ser mayor a cero.", HttpStatusCode.BadRequest);

                // Usar ServicioUsuario para obtener el usuario por legajo (ya tiene toda la lógica)
                var usuarioBase = _servicioUsuario.ObtenerPorLegajo(legajo);

                if (usuarioBase == null)
                    return JsonError($"No se encontró un usuario activo con el legajo {legajo}.", HttpStatusCode.NotFound);

                // Validar que el usuario esté activo
                if (!usuarioBase.Activo)
                    return JsonError($"El usuario con legajo {legajo} está inactivo.", HttpStatusCode.Forbidden);

                // Parsear fecha si se proporciona
                DateTime? fechaParam = null;
                if (!string.IsNullOrWhiteSpace(fecha))
                {
                    // Usar TryParseExact para asegurar que siempre se use la fecha del día correcto
                    if (DateTime.TryParseExact(fecha, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaParsed))
                        fechaParam = fechaParsed.Date;
                    else
                        return JsonError("Formato de fecha inválido. Use YYYY-MM-DD.", HttpStatusCode.BadRequest);
                }

                var resultado = _inicioService.ObtenerTotemActualizado(usuarioBase, turnoId, fechaParam);
                return JsonOk(resultado);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener datos de inicio actualizados: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================
        // GET api/dashboard/menu-del-dia?turnoId=1&fecha=2025-11-23
        // Menú del día SOLO del turno seleccionado
        // =====================================
        [HttpGet]
        [Route("menu-del-dia")]
        public HttpResponseMessage GetMenuDelDia(int turnoId, string fecha = null)
        {
            try
            {
                int usuarioId = GetUsuarioIdFromToken();
                if (usuarioId <= 0)
                    return JsonError("Usuario no identificado.", HttpStatusCode.Unauthorized);

                /*var plantaId = GetPlantaIdFromToken();
                if (!plantaId.HasValue)
                    return JsonError("Planta no disponible en el token.", HttpStatusCode.BadRequest);*/

                // Fecha (si no mandan, por defecto hoy)
                DateTime fechaDia;
                if (string.IsNullOrWhiteSpace(fecha))
                    fechaDia = DateTime.Today;
                else
                {
                    // Usar TryParseExact para asegurar que siempre se use la fecha del día correcto
                    if (DateTime.TryParseExact(fecha, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out DateTime fechaParsed))
                        fechaDia = fechaParsed.Date;
                    else
                        return JsonError("Formato de fecha inválido. Use YYYY-MM-DD.", HttpStatusCode.BadRequest);
                }

                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var menuQuery =
                        from m in ctx.sl_menudd
                        join pl in ctx.sl_plato on m.plato_id equals pl.id
                        join tu in ctx.sl_turno on m.turno_id equals tu.id
                        where m.deletemark == false
                              && m.fecha == fechaDia
                              && m.turno_id == turnoId
                              //&& m.planta_id == plantaId.Value
                        select new MenuDiaItemDto
                        {
                            menudd_id = m.id,
                            fecha = m.fecha,
                            turno_id = m.turno_id,
                            turno_nombre = tu.nombre,
                            plato_id = pl.id,
                            plato_codigo = pl.codigo,
                            plato_descripcion = pl.descripcion,
                            plato_costo = pl.costo
                        };

                    var menu = menuQuery
                        .OrderBy(x => x.plato_descripcion)
                        .ToList();

                    return JsonOk(menu);
                }
            }
            catch
            {
                return JsonError("Error al obtener el menú del día.", HttpStatusCode.InternalServerError);
            }
        }

        // =====================================
        // 
        // Información para cada uno de los campos que se necesitan
        // =====================================
        private int GetUsuarioIdFromToken()
        {
            try
            {
                var identity = User.Identity as ClaimsIdentity;
                var claim = identity?.Claims?.FirstOrDefault(c => c.Type == "usuario");
                if (claim == null || string.IsNullOrWhiteSpace(claim.Value))
                    return 0;
                return int.TryParse(claim.Value, out int id) ? id : 0;
            }
            catch
            {
                return 0;
            }
        }

        /*private int? GetPlantaIdFromToken()
        {
            var identity = User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == "planta_id");
            if (claim == null) return null;
            if (int.TryParse(claim.Value, out var id)) return id;
            return null;
        }
        private int? GetCentroCostoIdFromToken()
        {
            var identity = User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == "centro_costo_id");
            if (claim == null) return null;
            if (int.TryParse(claim.Value, out var id)) return id;
            return null;
        }

        private int? GetProyectoIdFromToken()
        {
            var identity = User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == "proyecto_id");
            if (claim == null) return null;
            if (int.TryParse(claim.Value, out var id)) return id;
            return null;
        }

        private int? GetJerarquiaIdFromToken()
        {
            var identity = User.Identity as ClaimsIdentity;
            var claim = identity?.Claims.FirstOrDefault(c => c.Type == "jerarquia_id");
            if (claim == null) return null;
            if (int.TryParse(claim.Value, out var id)) return id;
            return null;
        }*/
    }
}
