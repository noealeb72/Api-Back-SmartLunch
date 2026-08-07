using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioReglaBonificacion
    {
        PagedResultDto<ReglaBonificacionListadoDto> ObtenerLista(int page, int pageSize, string search, bool activo);
        ReglaBonificacionDetalleDto ObtenerPorId(int id);
        ReglaBonificacionDetalleDto Crear(ReglaBonificacionCreateDto dto, string username);
        void Actualizar(ReglaBonificacionUpdateDto dto, string username);
        void Eliminar(int id, string username);
        void Activar(int id, string username);

        /// <summary>
        /// Busca la regla activa de mayor prioridad que matchea el pedido puntual (todas las
        /// condiciones no-nulas de la regla deben coincidir). Recibe un DataContext ya abierto
        /// para poder ejecutarse dentro de la misma transacción que ServicioComanda.CrearConDescuento.
        /// Devuelve null si ninguna regla matchea (en ese caso, se cobra el 100%).
        /// </summary>
        sl_regla_bonificacion ObtenerReglaAplicable(
            DataContext ctx,
            int turnoId,
            int? jerarquiaId,
            int plantaId,
            int plannutricionalId,
            int platoId,
            bool esInvitado,
            int posicionPedido,
            DateTime fecha);

        /// <summary>
        /// Aplica el efecto de la regla (Porcentaje/MontoFijo/CostoCero) sobre el costo de lista
        /// del plato. Si <paramref name="regla"/> es null (ninguna regla matcheó), devuelve el
        /// costo completo (se cobra el 100%). Usado tanto al crear la comanda como al
        /// previsualizar el precio antes de confirmar, para que ambos cálculos no puedan divergir.
        /// </summary>
        decimal AplicarEfecto(decimal costoPlato, sl_regla_bonificacion regla);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioReglaBonificacion : IServicioReglaBonificacion
    {
        private static readonly string[] TiposEfectoValidos = { "Porcentaje", "MontoFijo", "CostoCero" };

        private readonly ILoggerService _logger;

        public ServicioReglaBonificacion(ILoggerService logger = null)
        {
            _logger = logger;
        }

        private ReglaBonificacionListadoDto AListadoDto(sl_regla_bonificacion r)
        {
            return new ReglaBonificacionListadoDto
            {
                Id = r.id,
                Nombre = r.nombre,
                Prioridad = r.prioridad,
                TurnoIds = r.Turnos?.Select(t => t.id).ToList() ?? new List<int>(),
                TurnoNombres = r.Turnos?.Select(t => t.nombre).ToList() ?? new List<string>(),
                JerarquiaId = r.jerarquia_id,
                JerarquiaNombre = r.Jerarquia?.nombre,
                PlantaId = r.planta_id,
                PlantaNombre = r.Planta?.nombre,
                PlannutricionalId = r.plannutricional_id,
                PlannutricionalNombre = r.Plannutricional?.nombre,
                PlatoIds = r.Platos?.Select(p => p.id).ToList() ?? new List<int>(),
                PlatoNombres = r.Platos?.Select(p => p.descripcion).ToList() ?? new List<string>(),
                PosicionPedido = r.posicion_pedido,
                EsInvitado = r.es_invitado,
                FechaDesde = r.fecha_desde,
                FechaHasta = r.fecha_hasta,
                TipoEfecto = r.tipo_efecto,
                ValorEfecto = r.valor_efecto,
                Deletemark = r.deletemark
            };
        }

        private ReglaBonificacionDetalleDto ADetalleDto(sl_regla_bonificacion r)
        {
            return new ReglaBonificacionDetalleDto
            {
                Id = r.id,
                Nombre = r.nombre,
                Prioridad = r.prioridad,
                TurnoIds = r.Turnos?.Select(t => t.id).ToList() ?? new List<int>(),
                TurnoNombres = r.Turnos?.Select(t => t.nombre).ToList() ?? new List<string>(),
                JerarquiaId = r.jerarquia_id,
                JerarquiaNombre = r.Jerarquia?.nombre,
                PlantaId = r.planta_id,
                PlantaNombre = r.Planta?.nombre,
                PlannutricionalId = r.plannutricional_id,
                PlannutricionalNombre = r.Plannutricional?.nombre,
                PlatoIds = r.Platos?.Select(p => p.id).ToList() ?? new List<int>(),
                PlatoNombres = r.Platos?.Select(p => p.descripcion).ToList() ?? new List<string>(),
                PosicionPedido = r.posicion_pedido,
                EsInvitado = r.es_invitado,
                FechaDesde = r.fecha_desde,
                FechaHasta = r.fecha_hasta,
                TipoEfecto = r.tipo_efecto,
                ValorEfecto = r.valor_efecto
            };
        }

        private void ValidarDto(string tipoEfecto, decimal? valorEfecto, int? posicionPedido)
        {
            if (string.IsNullOrWhiteSpace(tipoEfecto) || !TiposEfectoValidos.Contains(tipoEfecto))
                throw new Exception("El tipo de efecto debe ser Porcentaje, MontoFijo o CostoCero.");

            if (tipoEfecto != "CostoCero" && !valorEfecto.HasValue)
                throw new Exception("El valor del efecto es obligatorio salvo que el tipo sea Costo cero.");

            if (tipoEfecto == "Porcentaje" && valorEfecto.HasValue && (valorEfecto.Value < 0 || valorEfecto.Value > 100))
                throw new Exception("El porcentaje debe estar entre 0 y 100.");

            if (tipoEfecto != "CostoCero" && valorEfecto.HasValue && valorEfecto.Value < 0)
                throw new Exception("El valor del efecto no puede ser negativo.");

            if (posicionPedido.HasValue && (posicionPedido.Value < 1 || posicionPedido.Value > 3))
                throw new Exception("La posición del pedido debe ser 1 (primero), 2 (segundo) o 3 (tercero en adelante).");
        }

        private Exception HandleValidationException(DbEntityValidationException ex, string operacion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se produjeron errores de validación al {operacion} la regla:");
            foreach (var eve in ex.EntityValidationErrors)
            {
                sb.AppendLine($"- Entidad \"{eve.Entry.Entity.GetType().Name}\" (estado {eve.Entry.State}):");
                foreach (var ve in eve.ValidationErrors)
                {
                    var msg = ve.ErrorMessage ?? string.Empty;
                    if (msg.Contains("is required"))
                        msg = $"El campo \"{ve.PropertyName}\" es obligatorio.";
                    else if (msg.Contains("maximum length"))
                        msg = $"El campo \"{ve.PropertyName}\" supera la longitud máxima permitida.";
                    else
                        msg = $"{ve.PropertyName}: {msg}";
                    sb.AppendLine("    • " + msg);
                }
            }
            return new Exception(sb.ToString(), ex);
        }

        // ============================================
        // Lista paginada
        // ============================================
        public PagedResultDto<ReglaBonificacionListadoDto> ObtenerLista(int page, int pageSize, string search, bool activo)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var query = ctx.sl_regla_bonificacion
                    .Include(r => r.Turnos)
                    .Include(r => r.Jerarquia)
                    .Include(r => r.Planta)
                    .Include(r => r.Plannutricional)
                    .Include(r => r.Platos)
                    .AsQueryable();

                query = activo
                    ? query.Where(r => !r.deletemark)
                    : query.Where(r => r.deletemark);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(r => (r.nombre ?? "").ToLower().Contains(s));
                }

                var totalItems = query.Count();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var items = query
                    .OrderByDescending(r => r.prioridad)
                    .ThenBy(r => r.nombre)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList()
                    .Select(AListadoDto)
                    .ToList();

                return new PagedResultDto<ReglaBonificacionListadoDto>
                {
                    page = page,
                    pageSize = pageSize,
                    totalItems = totalItems,
                    totalPages = totalPages,
                    items = items
                };
            }
        }

        // ============================================
        // Detalle por Id
        // ============================================
        public ReglaBonificacionDetalleDto ObtenerPorId(int id)
        {
            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var entity = ctx.sl_regla_bonificacion
                    .Include(r => r.Turnos)
                    .Include(r => r.Jerarquia)
                    .Include(r => r.Planta)
                    .Include(r => r.Plannutricional)
                    .Include(r => r.Platos)
                    .FirstOrDefault(r => r.id == id && r.deletemark != true);

                return entity == null ? null : ADetalleDto(entity);
            }
        }

        // ============================================
        // Crear
        // ============================================
        public ReglaBonificacionDetalleDto Crear(ReglaBonificacionCreateDto dto, string username)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre de la regla es obligatorio.");

            ValidarDto(dto.TipoEfecto, dto.ValorEfecto, dto.PosicionPedido);

            using (var ctx = new DataContext())
            {
                var existeNombre = ctx.sl_regla_bonificacion.Any(r =>
                    r.nombre == dto.Nombre && r.deletemark != true);
                if (existeNombre)
                    throw new Exception("Ya existe una regla con ese nombre.");

                var nombreTruncado = dto.Nombre.Length > 150 ? dto.Nombre.Substring(0, 150) : dto.Nombre;

                var entity = new sl_regla_bonificacion
                {
                    nombre = nombreTruncado,
                    prioridad = dto.Prioridad,
                    jerarquia_id = dto.JerarquiaId,
                    planta_id = dto.PlantaId,
                    plannutricional_id = dto.PlannutricionalId,
                    posicion_pedido = dto.PosicionPedido,
                    es_invitado = dto.EsInvitado,
                    fecha_desde = dto.FechaDesde,
                    fecha_hasta = dto.FechaHasta,
                    tipo_efecto = dto.TipoEfecto,
                    valor_efecto = dto.TipoEfecto == "CostoCero" ? null : dto.ValorEfecto,
                    createdate = DateTime.Now,
                    createuser = username,
                    deletemark = false
                };

                if (dto.TurnoIds != null && dto.TurnoIds.Count > 0)
                {
                    var turnosSeleccionados = ctx.sl_turno.Where(t => dto.TurnoIds.Contains(t.id)).ToList();
                    foreach (var turno in turnosSeleccionados)
                        entity.Turnos.Add(turno);
                }

                if (dto.PlatoIds != null && dto.PlatoIds.Count > 0)
                {
                    var platosSeleccionados = ctx.sl_plato.Where(p => dto.PlatoIds.Contains(p.id)).ToList();
                    foreach (var plato in platosSeleccionados)
                        entity.Platos.Add(plato);
                }

                ctx.sl_regla_bonificacion.Add(entity);

                try
                {
                    ctx.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    throw HandleValidationException(ex, "crear");
                }

                return ObtenerPorId(entity.id);
            }
        }

        // ============================================
        // Actualizar
        // ============================================
        public void Actualizar(ReglaBonificacionUpdateDto dto, string username)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre de la regla es obligatorio.");

            ValidarDto(dto.TipoEfecto, dto.ValorEfecto, dto.PosicionPedido);

            using (var ctx = new DataContext())
            {
                var entity = ctx.sl_regla_bonificacion
                    .Include(r => r.Turnos)
                    .Include(r => r.Platos)
                    .FirstOrDefault(r => r.id == dto.Id && r.deletemark != true);
                if (entity == null)
                    throw new Exception("Regla no encontrada.");

                var existeNombre = ctx.sl_regla_bonificacion.Any(r =>
                    r.id != dto.Id && r.nombre == dto.Nombre && r.deletemark != true);
                if (existeNombre)
                    throw new Exception("Ya existe otra regla con ese nombre.");

                entity.nombre = dto.Nombre.Length > 150 ? dto.Nombre.Substring(0, 150) : dto.Nombre;
                entity.prioridad = dto.Prioridad;
                entity.jerarquia_id = dto.JerarquiaId;
                entity.planta_id = dto.PlantaId;
                entity.plannutricional_id = dto.PlannutricionalId;

                entity.Turnos.Clear();
                if (dto.TurnoIds != null && dto.TurnoIds.Count > 0)
                {
                    var turnosSeleccionados = ctx.sl_turno.Where(t => dto.TurnoIds.Contains(t.id)).ToList();
                    foreach (var turno in turnosSeleccionados)
                        entity.Turnos.Add(turno);
                }

                entity.Platos.Clear();
                if (dto.PlatoIds != null && dto.PlatoIds.Count > 0)
                {
                    var platosSeleccionados = ctx.sl_plato.Where(p => dto.PlatoIds.Contains(p.id)).ToList();
                    foreach (var plato in platosSeleccionados)
                        entity.Platos.Add(plato);
                }

                entity.posicion_pedido = dto.PosicionPedido;
                entity.es_invitado = dto.EsInvitado;
                entity.fecha_desde = dto.FechaDesde;
                entity.fecha_hasta = dto.FechaHasta;
                entity.tipo_efecto = dto.TipoEfecto;
                entity.valor_efecto = dto.TipoEfecto == "CostoCero" ? null : dto.ValorEfecto;
                entity.updatedate = DateTime.Now;
                entity.updateuser = username;

                try
                {
                    ctx.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    throw HandleValidationException(ex, "actualizar");
                }
            }
        }

        // ============================================
        // Baja lógica
        // ============================================
        public void Eliminar(int id, string username)
        {
            using (var ctx = new DataContext())
            {
                var entity = ctx.sl_regla_bonificacion.FirstOrDefault(r => r.id == id && r.deletemark != true);
                if (entity == null)
                    throw new Exception("Regla no encontrada.");

                entity.deletemark = true;
                entity.updatedate = DateTime.Now;
                entity.updateuser = username;

                ctx.SaveChanges();
            }
        }

        // ============================================
        // Activar
        // ============================================
        public void Activar(int id, string username)
        {
            using (var ctx = new DataContext())
            {
                var entity = ctx.sl_regla_bonificacion.FirstOrDefault(r => r.id == id && r.deletemark == true);
                if (entity == null)
                    throw new Exception("Regla no encontrada.");

                entity.deletemark = false;
                entity.updatedate = DateTime.Now;
                entity.updateuser = username;

                ctx.SaveChanges();
            }
        }

        // ============================================
        // Evaluación: regla aplicable a un pedido puntual
        // ============================================
        public sl_regla_bonificacion ObtenerReglaAplicable(
            DataContext ctx,
            int turnoId,
            int? jerarquiaId,
            int plantaId,
            int plannutricionalId,
            int platoId,
            bool esInvitado,
            int posicionPedido,
            DateTime fecha)
        {
            var fechaSolo = fecha.Date;

            var query = ctx.sl_regla_bonificacion
                .Where(r => !r.deletemark)
                .Where(r => !r.Turnos.Any() || r.Turnos.Any(t => t.id == turnoId))
                .Where(r => r.jerarquia_id == null || r.jerarquia_id == jerarquiaId)
                .Where(r => r.planta_id == null || r.planta_id == plantaId)
                .Where(r => r.plannutricional_id == null || r.plannutricional_id == plannutricionalId)
                .Where(r => !r.Platos.Any() || r.Platos.Any(p => p.id == platoId))
                .Where(r => r.es_invitado == null || r.es_invitado == esInvitado)
                .Where(r => r.fecha_desde == null || r.fecha_desde <= fechaSolo)
                .Where(r => r.fecha_hasta == null || r.fecha_hasta >= fechaSolo)
                // Nota: se evita el operador ternario dentro del Where porque LINQ-to-Entities (EF6)
                // no siempre lo traduce correctamente a SQL. Se expresa como OR de condiciones simples:
                // - sin posición configurada => aplica siempre
                // - coincidencia exacta (cubre 1, 2 y también 3 contra 3)
                // - regla "3 = tercero en adelante" contra cualquier posición real >= 3
                .Where(r =>
                    r.posicion_pedido == null ||
                    r.posicion_pedido == posicionPedido ||
                    (r.posicion_pedido >= 3 && posicionPedido >= 3));

            return query
                .OrderByDescending(r => r.prioridad)
                .ThenBy(r => r.id)
                .FirstOrDefault();
        }

        public decimal AplicarEfecto(decimal costoPlato, sl_regla_bonificacion regla)
        {
            if (regla == null)
                return costoPlato;

            decimal montoCalculado;
            switch (regla.tipo_efecto)
            {
                case "CostoCero":
                    montoCalculado = 0m;
                    break;
                case "MontoFijo":
                    montoCalculado = regla.valor_efecto ?? costoPlato;
                    break;
                case "Porcentaje":
                default:
                    montoCalculado = costoPlato * (1 - (regla.valor_efecto ?? 0) / 100);
                    break;
            }
            return Math.Round(montoCalculado, 2, MidpointRounding.AwayFromZero);
        }
    }
}
