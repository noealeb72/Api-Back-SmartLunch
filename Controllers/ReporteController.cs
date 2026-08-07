using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    [Authorize]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/reporte")]
    public class ReporteController : BaseApiController
    {
        private readonly IServicioReporte _servicioReporte;

        // Constructor sin parámetros (requerido por Web API)
        public ReporteController()
        {
            _servicioReporte = new ServicioReporte();
        }

        // Constructor con parámetros (para inyección de dependencias/testing)
        public ReporteController(IServicioReporte servicioReporte)
        {
            _servicioReporte = servicioReporte ?? throw new ArgumentNullException(nameof(servicioReporte));
        }


        // ============================================================
        // 1) Reporte por usuario (legajo) + rango de fechas + plantaId
        // ============================================================
        /// <summary>
        /// user = legajo (string numérico)
        /// desde / hasta = DateTime (solo fecha)
        /// plantaId = id de planta (null o 0 = sin filtro)
        /// </summary>
        /*[HttpGet]
        [Route("User")]
        [AllowAnonymous]
        public HttpResponseMessage GetUserReport(
            string user,
            DateTime desde,
            DateTime hasta,
            int? plantaId = null)
        {
            try
            {
                if (!int.TryParse(user, out var legajo))
                    return JsonError("El parámetro 'user' debe ser un legajo numérico.");

                // Normalizo a solo fecha y hago rango inclusivo usando < hasta+1día
                var fechadesde = desde.Date;
                var fechahastaInc = hasta.Date.AddDays(1);

                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    bool filtrarPlanta = plantaId.HasValue && plantaId.Value > 0;

                    var read = ctx.sl_usuario
                        .Where(u =>
                            u.deletemark != true &&
                            u.legajo == legajo //&&
                            //(!filtrarPlanta || u.planta_id == plantaId.Value)
                        )
                        .Select(u => new
                        {
                            u.nombre,
                            u.apellido,
                            u.dni,
                            u.foto,
                            u.legajo,
                            u.plannutricional,
                            u.proyecto_id,
                            u.centrodecosto_id,
                            u.planta_id,
                            u.domicilio,

                            // Comandas consumidas en el rango + descripción del plato
                            consumidos = (
                                from c in ctx.sl_comanda
                                join p in ctx.sl_plato on c.plato_id equals p.id into gj
                                from p in gj.DefaultIfEmpty()
                                where c.usuario_id == u.dni
                                      && c.deletemark != true
                                      && c.fecha >= fechadesde
                                      && c.fecha < fechahastaInc
                                orderby c.fecha descending, c.id descending
                                select new
                                {
                                    c.id,
                                    c.fecha,
                                    c.monto,
                                    c.estado,
                                    platoId = c.plato_id,
                                    descripcionPlato = p != null ? p.descripcion : null,
                                    c.bonificado,
                                }
                            ).ToList(),

                            // Bonificaciones acumuladas en sl_usuario
                            bonificados = u.bonificaciones,

                            // Cantidad de pedidos bonificados a invitados dentro del rango
                            bonificadosInvitados = ctx.sl_comanda
                                .Where(c => c.usuario_id == u.id
                                         && c.deletemark != true
                                         && (c.invitado == false)
                                         && c.fecha >= fechadesde
                                         && c.fecha < fechahastaInc)
                                .Count(),

                            // Monto total del rango
                            monto = ctx.sl_comanda
                                .Where(c => c.usuario_id == u.id
                                         && c.deletemark != true
                                         && c.fecha >= fechadesde
                                         && c.fecha < fechahastaInc)
                                .Select(c => (double?)c.monto)
                                .DefaultIfEmpty(0d)
                                .Sum(),

                            // Lista de estados en el rango
                            estados = ctx.sl_comanda
                                .Where(c => c.usuario_id == u.id
                                         && c.deletemark != true
                                         && c.fecha >= fechadesde
                                         && c.fecha < fechahastaInc)
                                .Select(c => c.estado)
                                .ToList(),

                            // Último estado del rango
                            ultimoEstado = ctx.sl_comanda
                                .Where(c => c.usuario_id == u.id
                                         && c.deletemark != true
                                         && c.fecha >= fechadesde
                                         && c.fecha < fechahastaInc)
                                .OrderByDescending(c => c.fecha)
                                .ThenByDescending(c => c.id)
                                .Select(c => c.estado)
                                .FirstOrDefault(),

                            // Descripción del plato de la última comanda en el rango
                            ultimoPlato = (
                                from c in ctx.sl_comanda
                                join p in ctx.sl_plato on c.plato_id equals p.id into gj
                                from p in gj.DefaultIfEmpty()
                                where c.usuario_id == u.id
                                      && c.deletemark != true
                                      && c.fecha >= fechadesde
                                      && c.fecha < fechahastaInc
                                orderby c.fecha descending, c.id descending
                                select p.descripcion
                            ).FirstOrDefault(),

                            // Lista de platos distintos consumidos en el rango
                            descripcionesPlatos = (
                                from c in ctx.sl_comanda
                                join p in ctx.sl_plato on c.plato_id equals p.id into gj
                                from p in gj.DefaultIfEmpty()
                                where c.usuario_id == u.id
                                      && c.deletemark != true
                                      && c.fecha >= fechadesde
                                      && c.fecha < fechahastaInc
                                select p.descripcion
                            ).Distinct().ToList()
                        })
                        .FirstOrDefault();

                    return JsonOk(read);
                }
            }
            catch
            {
                return JsonError("Error al obtener el reporte del usuario.", HttpStatusCode.InternalServerError);
            }
        }*/
        [AuthorizeWith403ForForbidden(Roles = "Gerencia")]
        [HttpGet]
        [Route("User")]
        public HttpResponseMessage GetUserReport(string user, DateTime desde, DateTime hasta, int? plantaId = null)
        {
            try
            {
                if (!int.TryParse(user, out var legajo))
                    return JsonError("El parámetro 'user' debe ser un legajo numérico.");

                var reporte = _servicioReporte.ObtenerReporteUsuario(legajo, desde, hasta, plantaId);
                return JsonOk(reporte);
            }
            catch (Exception ex)
            {
                // Incluir InnerException para más detalles
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                }
                return JsonError($"Error al obtener el reporte del usuario: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }



        // ==========================================
        // 2) Reporte general con filtros por IDs
        // ==========================================
        /// <summary>
        /// plantaId, centrodecostoId, proyectoId por ID (null = sin filtro).
        /// </summary>
       // GET api/reporte/General
        [AuthorizeWith403ForForbidden(Roles = "Gerencia")]
        [HttpGet]
        [Route("General")]
        public HttpResponseMessage GetGeneralReport(
            DateTime? fechaDesde = null,
            DateTime? fechaHasta = null,
            int? platoId = null,
            int? proyectoId = null,
            int? plantaId = null,
            int? jerarquiaId = null,
            int? centrodecostoId = null,
            string estado = "")
        {
            try
            {
                var reporte = _servicioReporte.ObtenerReporteGestion(
                    fechaDesde: fechaDesde,
                    fechaHasta: fechaHasta,
                    platoId: platoId,
                    proyectoId: proyectoId,
                    plantaId: plantaId,
                    jerarquiaId: jerarquiaId,
                    centrodecostoId: centrodecostoId,
                    estado: estado);
                return JsonOk(reporte);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                }
                return JsonError($"Error al obtener el reporte de gestión: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }

        // ==================================================
        // 3) Listado de comandas con fechas + IDs
        // ==================================================
        /// <summary>
        /// fechadesde / fechahasta como DateTime? (null = sin filtro)
        /// platoId = id de plato
        /// centrodecostoId, proyectoId, plantaId = IDs (null = sin filtro)
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Gerencia")]
        [HttpGet]
        [Route("Comandas")]
        public HttpResponseMessage GetComandas(
            DateTime? fechadesde = null,
            DateTime? fechahasta = null,
            int? platoId = null,
            int? centrodecostoId = null,
            int? proyectoId = null,
            int? plantaId = null)
        {
            try
            {
                var comandas = _servicioReporte.ObtenerComandasReporte(fechadesde, fechahasta, platoId, centrodecostoId, proyectoId, plantaId);
                return JsonOk(comandas);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener las comandas: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Reporte de facturación: comandas recibidas (R) con reparto empleado/empresa según bonificación de jerarquía del usuario.
        /// </summary>
        /// <param name="fechaDesde">YYYY-MM-DD</param>
        /// <param name="fechaHasta">YYYY-MM-DD</param>
        [AuthorizeWith403ForForbidden(Roles = "Gerencia")]
        [HttpGet]
        [Route("Facturacion")]
        public HttpResponseMessage GetReporteFacturacion(
            string fechaDesde,
            string fechaHasta,
            int? plantaId = null,
            int? proyectoId = null,
            int? centrodecostoId = null)
        {
            if (string.IsNullOrWhiteSpace(fechaDesde) || string.IsNullOrWhiteSpace(fechaHasta))
                return JsonError("fechaDesde y fechaHasta son obligatorias (formato YYYY-MM-DD).", HttpStatusCode.BadRequest);

            if (!DateTime.TryParseExact(fechaDesde.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d1))
                return JsonError("fechaDesde inválida. Use formato YYYY-MM-DD.", HttpStatusCode.BadRequest);

            if (!DateTime.TryParseExact(fechaHasta.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d2))
                return JsonError("fechaHasta inválida. Use formato YYYY-MM-DD.", HttpStatusCode.BadRequest);

            try
            {
                var reporte = _servicioReporte.ObtenerReporteFacturacion(d1, d2, plantaId, proyectoId, centrodecostoId);
                return JsonOk(reporte);
            }
            catch (Exception ex)
            {
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                return JsonError($"Error al obtener el reporte de facturación: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }
    }
}
