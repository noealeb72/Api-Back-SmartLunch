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
    public interface IServicioReporte
    {
        ReporteUsuarioDto ObtenerReporteUsuario(int legajo, DateTime desde, DateTime hasta, int? plantaId = null);
        ReporteGeneralDto ObtenerReporteGeneral(int? plantaId = null, int? centrodecostoId = null, int? proyectoId = null);
        List<ComandaListadoReporteDto> ObtenerComandasReporte(DateTime? fechadesde = null, DateTime? fechahasta = null, int? platoId = null, int? centrodecostoId = null, int? proyectoId = null, int? plantaId = null);
        List<ReporteGestionDto> ObtenerReporteGestion(DateTime? fechaDesde = null, DateTime? fechaHasta = null, int? platoId = null, int? proyectoId = null, int? plantaId = null, int? jerarquiaId = null, int? centrodecostoId = null, string estado="");
        List<ReporteFacturacionDTO> ObtenerReporteFacturacion(DateTime fechaDesde, DateTime fechaHasta, int? plantaId = null, int? proyectoId = null, int? centrodecostoId = null);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioReporte : IServicioReporte
    {
        private readonly ILoggerService _logger;

        public ServicioReporte(ILoggerService logger = null)
        {
            _logger = logger;
        }

        // ===================== MÉTODOS HELPER =====================

        /// <summary>
        /// Maneja excepciones de validación de Entity Framework y genera mensajes descriptivos
        /// </summary>
        private Exception HandleValidationException(DbEntityValidationException ex, string operacion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el reporte:");
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
        // Reporte por usuario (legajo) + rango de fechas + plantaId
        // ============================================
        public ReporteUsuarioDto ObtenerReporteUsuario(int legajo, DateTime desde, DateTime hasta, int? plantaId = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (legajo <= 0)
                throw new Exception("El legajo debe ser mayor a 0.");

            if (desde > hasta)
                throw new Exception("La fecha de inicio no puede ser posterior a la fecha de fin.");

            // Validar rango de fechas razonable (máximo 1 año)
            var diasDiferencia = (hasta - desde).Days;
            if (diasDiferencia > 365)
                throw new Exception("El rango de fechas no puede exceder 365 días.");

            if (diasDiferencia < 0)
                throw new Exception("El rango de fechas es inválido.");

            if (plantaId.HasValue && plantaId.Value <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            try
            {
                var fechadesde = desde.Date;
                var fechahastaInc = hasta.Date.AddDays(1);

                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    // ===================== LOGGING: Inicio de reporte =====================
                    _logger?.LogInformation("ObtenerReporteUsuario: Iniciando obtención de reporte de usuario", new
                    {
                        Legajo = legajo,
                        FechaDesde = desde,
                        FechaHasta = hasta,
                        PlantaId = plantaId
                    });

                    // Obtener usuario con todas sus relaciones usando Include
                    var usuario = ctx.sl_usuario
                        .Include(u => u.planta)
                        .Include(u => u.centrodecosto)
                        .Include(u => u.proyecto)
                        .Include(u => u.jerarquia)
                        .Include(u => u.plannutricional)
                        .Where(u => u.deletemark != true && u.legajo == legajo)
                        .FirstOrDefault();

                    if (usuario == null)
                        throw new Exception("Usuario no encontrado.");

                    // Filtrar por planta si se especifica
                    if (plantaId.HasValue && plantaId.Value > 0 && usuario.planta_id != plantaId.Value)
                        throw new Exception("El usuario no pertenece a la planta especificada.");

                    // Cargar las relaciones de forma explícita si no se cargaron con Include
                    // Esto es una medida de seguridad adicional
                    if (usuario.planta_id.HasValue && usuario.planta == null)
                        usuario.planta = ctx.sl_planta.FirstOrDefault(p => p.id == usuario.planta_id.Value);
                    
                    if (usuario.centrodecosto_id.HasValue && usuario.centrodecosto == null)
                        usuario.centrodecosto = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == usuario.centrodecosto_id.Value);
                    
                    if (usuario.proyecto_id.HasValue && usuario.proyecto == null)
                        usuario.proyecto = ctx.sl_proyecto.FirstOrDefault(p => p.id == usuario.proyecto_id.Value);
                    
                    if (usuario.jerarquia_id.HasValue && usuario.jerarquia == null)
                        usuario.jerarquia = ctx.sl_jerarquia.FirstOrDefault(j => j.id == usuario.jerarquia_id.Value);
                    
                    if (usuario.plannutricional_id.HasValue && usuario.plannutricional == null)
                        usuario.plannutricional = ctx.sl_plannutricional.FirstOrDefault(p => p.id == usuario.plannutricional_id.Value);

                // Obtener todas las comandas del rango (con turno y nro pedido)
                var comandas = (
                    from c in ctx.sl_comanda
                    join p in ctx.sl_plato on c.plato_id equals p.id into gj
                    from p in gj.DefaultIfEmpty()
                    join t in ctx.sl_turno on c.turno_id equals t.id into gt
                    from t in gt.DefaultIfEmpty()
                    where c.deletemark != true
                          && c.usuario_id == usuario.id
                          && c.fecha >= fechadesde
                          && c.fecha < fechahastaInc
                    orderby c.fecha descending, c.id descending
                    select new ComandaReporteDto
                    {
                        Id = c.id,
                        Npedido = c.npedido,
                        TurnoId = c.turno_id,
                        TurnoNombre = t != null ? t.nombre : null,
                        Fecha = c.fecha,
                        Monto = c.monto,
                        Estado = c.estado ?? string.Empty,
                        PlatoId = c.plato_id,
                        DescripcionPlato = p != null ? p.descripcion : null,
                        Bonificado = c.bonificado,
                        Invitado = c.invitado
                    }
                ).ToList();

                var ultimo = comandas.FirstOrDefault();

                // Contar bonificaciones de invitados en el rango
                var bonificadosInvitadosRango = comandas.Count(x => x.Invitado && x.Bonificado);

                // Construir DTO con todas las relaciones
                var dto = new ReporteUsuarioDto
                {
                    Id = usuario.id,
                    Nombre = usuario.nombre,
                    Apellido = usuario.apellido,
                    Dni = usuario.dni,
                    Foto = usuario.foto,
                    Legajo = usuario.legajo,
                    Domicilio = usuario.domicilio,

                    // Relaciones del usuario
                    PlantaId = usuario.planta_id,
                    PlantaNombre = usuario.planta != null ? usuario.planta.nombre : null,
                    PlantaDescripcion = usuario.planta != null ? usuario.planta.descripcion : null,

                    PerfilNutricionalId = usuario.plannutricional_id.HasValue ? usuario.plannutricional_id.Value : 0,
                    PerfilNutricionalNombre = usuario.plannutricional != null ? usuario.plannutricional.nombre : null,

                    CentroDeCostoId = usuario.centrodecosto_id,
                    CentroDeCostoNombre = usuario.centrodecosto != null ? usuario.centrodecosto.nombre : null,
                    CentroDeCostoDescripcion = usuario.centrodecosto != null ? usuario.centrodecosto.descripcion : null,

                    ProyectoId = usuario.proyecto_id,
                    ProyectoNombre = usuario.proyecto != null ? usuario.proyecto.nombre : null,
                    ProyectoDescripcion = usuario.proyecto != null ? usuario.proyecto.descripcion : null,

                    JerarquiaId = usuario.jerarquia_id,
                    JerarquiaNombre = usuario.jerarquia != null ? usuario.jerarquia.nombre : null,
                    JerarquiaDescripcion = usuario.jerarquia != null ? usuario.jerarquia.descripcion : null,

                    PlanNutricionalId = usuario.plannutricional_id,
                    PlanNutricionalNombre = usuario.plannutricional != null ? usuario.plannutricional.nombre : null,
                    PlanNutricionalDescripcion = usuario.plannutricional != null ? usuario.plannutricional.descripcion : null,

                    // Bonificaciones
                    Bonificados = usuario.bonificaciones.HasValue ? (int)usuario.bonificaciones.Value : 0,
                    BonificacionesInvitadoAcum = usuario.bonificaciones_invitado.HasValue ? (int)usuario.bonificaciones_invitado.Value : 0,
                    BonificadosInvitadosRango = bonificadosInvitadosRango,

                    

                    // Comandas del rango
                    Consumidos = comandas,

                    // Estadísticas del rango
                    Monto = comandas.Sum(x => (double?)x.Monto) ?? 0d,
                    Estados = comandas.Select(x => x.Estado).Where(e => !string.IsNullOrEmpty(e)).ToList(),
                    UltimoEstado = ultimo?.Estado,
                    UltimoPlato = ultimo?.DescripcionPlato,
                    DescripcionesPlatos = comandas
                        .Select(x => x.DescripcionPlato)
                        .Where(x => !string.IsNullOrEmpty(x))
                        .Distinct()
                        .ToList()
                };

                    // ===================== LOGGING: Reporte exitoso =====================
                    _logger?.LogInformation("ObtenerReporteUsuario: Reporte obtenido exitosamente", new
                    {
                        Legajo = legajo,
                        UsuarioId = dto.Id,
                        TotalComandas = dto.Consumidos?.Count ?? 0,
                        MontoTotal = dto.Monto
                    });

                    return dto;
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Usuario no encontrado") || ex.Message.Contains("no pertenece") || ex.Message.Contains("rango de fechas") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ObtenerReporteUsuario: Error al obtener reporte de usuario", ex, new
                {
                    Legajo = legajo,
                    FechaDesde = desde,
                    FechaHasta = hasta,
                    PlantaId = plantaId
                });
                throw new Exception($"Error al obtener el reporte del usuario: {ex.Message}", ex);
            }
        }

        // ============================================
        // Reporte general con filtros por IDs
        // ============================================
        public ReporteGeneralDto ObtenerReporteGeneral(int? plantaId = null, int? centrodecostoId = null, int? proyectoId = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (plantaId.HasValue && plantaId.Value <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (centrodecostoId.HasValue && centrodecostoId.Value <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            if (proyectoId.HasValue && proyectoId.Value <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de reporte =====================
                    _logger?.LogInformation("ObtenerReporteGeneral: Iniciando obtención de reporte general", new
                    {
                        PlantaId = plantaId,
                        CentrodecostoId = centrodecostoId,
                        ProyectoId = proyectoId
                    });

                var qBase = ctx.sl_comanda.AsNoTracking().Where(c => c.deletemark == false);

                var platosplanta = qBase
                    .Where(c => !plantaId.HasValue || c.planta_id == plantaId.Value)
                    .Count();

                var platoscc = qBase
                    .Where(c => !centrodecostoId.HasValue || c.centrodecosto_id == centrodecostoId.Value)
                    .Count();

                var platosproyecto = qBase
                    .Where(c => !proyectoId.HasValue || c.proyecto_id == proyectoId.Value)
                    .Count();

                var platosdistintos = ctx.sl_plato
                    .AsNoTracking()
                    .Where(p => p.deletemark == false)
                    .Count();

                var califQuery = qBase
                    .Where(c => c.estado == "R")
                    .Select(c => (double?)c.calificacion);

                double promediocalificacion = 0;
                if (califQuery.Any())
                    promediocalificacion = Math.Round(califQuery.Average().GetValueOrDefault(0), 0);

                var devueltos = qBase
                    .Where(c => c.estado == "D")
                    .Count();

                var monto = qBase
                    .Where(c => c.estado == "E" || c.estado == "R")
                    .Select(c => (double?)c.monto)
                    .DefaultIfEmpty(0d)
                    .Sum();

                    var resultado = new ReporteGeneralDto
                    {
                        PlatosPlanta = platosplanta,
                        PlatosCc = platoscc,
                        PlatosProyecto = platosproyecto,
                        PlatosDistintos = platosdistintos,
                        PromedioCalificacion = promediocalificacion,
                        Devueltos = devueltos,
                        Monto = monto ?? 0d
                    };

                    // ===================== LOGGING: Reporte exitoso =====================
                    _logger?.LogInformation("ObtenerReporteGeneral: Reporte obtenido exitosamente", new
                    {
                        PlatosPlanta = resultado.PlatosPlanta,
                        PlatosCc = resultado.PlatosCc,
                        PlatosProyecto = resultado.PlatosProyecto,
                        MontoTotal = resultado.Monto
                    });

                    return resultado;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerReporteGeneral: Error al obtener reporte general", ex, new
                {
                    PlantaId = plantaId,
                    CentrodecostoId = centrodecostoId,
                    ProyectoId = proyectoId
                });
                throw;
            }
        }

        // ============================================
        // Listado de comandas con fechas + IDs
        // ============================================
        public List<ComandaListadoReporteDto> ObtenerComandasReporte(DateTime? fechadesde = null, DateTime? fechahasta = null, int? platoId = null, int? centrodecostoId = null, int? proyectoId = null, int? plantaId = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (fechadesde.HasValue && fechahasta.HasValue && fechadesde.Value > fechahasta.Value)
                throw new Exception("La fecha de inicio no puede ser posterior a la fecha de fin.");

            if (fechadesde.HasValue && fechahasta.HasValue)
            {
                var diasDiferencia = (fechahasta.Value - fechadesde.Value).Days;
                if (diasDiferencia > 365)
                    throw new Exception("El rango de fechas no puede exceder 365 días.");
            }

            if (platoId.HasValue && platoId.Value <= 0)
                throw new Exception("El ID del plato debe ser mayor a 0.");

            if (centrodecostoId.HasValue && centrodecostoId.Value <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            if (proyectoId.HasValue && proyectoId.Value <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            if (plantaId.HasValue && plantaId.Value <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de reporte =====================
                    _logger?.LogInformation("ObtenerComandasReporte: Iniciando obtención de comandas para reporte", new
                    {
                        FechaDesde = fechadesde,
                        FechaHasta = fechahasta,
                        PlatoId = platoId,
                        CentrodecostoId = centrodecostoId,
                        ProyectoId = proyectoId,
                        PlantaId = plantaId
                    });

                    DateTime? desde = fechadesde?.Date;
                    DateTime? hastaInc = fechahasta?.Date.AddDays(1);

                var q = ctx.sl_comanda.AsNoTracking().Where(c => c.deletemark == false);

                if (desde.HasValue) q = q.Where(c => c.fecha >= desde.Value);
                if (hastaInc.HasValue) q = q.Where(c => c.fecha < hastaInc.Value);
                if (platoId.HasValue && platoId.Value > 0)
                    q = q.Where(c => c.plato_id == platoId.Value);
                if (centrodecostoId.HasValue && centrodecostoId.Value > 0)
                    q = q.Where(c => c.centrodecosto_id == centrodecostoId.Value);
                if (proyectoId.HasValue && proyectoId.Value > 0)
                    q = q.Where(c => c.proyecto_id == proyectoId.Value);
                if (plantaId.HasValue && plantaId.Value > 0)
                    q = q.Where(c => c.planta_id == plantaId.Value);

                // LEFT JOIN a usuario, plato, planta, cc y proyecto
                var read =
                    from c in q
                    join u in ctx.sl_usuario.AsNoTracking() on c.usuario_id equals u.id into gu
                    from u in gu.DefaultIfEmpty()
                    join p0 in ctx.sl_plato.AsNoTracking() on c.plato_id equals p0.id into gp
                    from p in gp.DefaultIfEmpty()
                    join pla0 in ctx.sl_planta.AsNoTracking() on c.planta_id equals pla0.id into gpla
                    from pla in gpla.DefaultIfEmpty()
                    join cc0 in ctx.sl_centrodecosto.AsNoTracking() on c.centrodecosto_id equals cc0.id into gcc
                    from cc in gcc.DefaultIfEmpty()
                    join proy0 in ctx.sl_proyecto.AsNoTracking() on c.proyecto_id equals proy0.id into gproy
                    from proy in gproy.DefaultIfEmpty()
                    join t0 in ctx.sl_turno.AsNoTracking() on c.turno_id equals t0.id into gturno
                    from t in gturno.DefaultIfEmpty()
                    orderby c.fecha descending, c.id descending
                    select new ComandaListadoReporteDto
                    {
                        Npedido = c.npedido,
                        TurnoId = c.turno_id,
                        TurnoNombre = t != null ? t.nombre : null,
                        Fecha = c.fecha,
                        PlantaId = c.planta_id,
                        PlantaNombre = pla != null ? pla.nombre : null,
                        CentrodecostoId = c.centrodecosto_id,
                        CentrodecostoNombre = cc != null ? cc.nombre : null,
                        ProyectoId = c.proyecto_id,
                        ProyectoNombre = proy != null ? proy.nombre : null,
                        UsuarioId = c.usuario_id,
                        UsuarioNombre = u != null ? u.nombre : null,
                        UsuarioApellido = u != null ? u.apellido : null,
                        PlatoId = c.plato_id,
                        PlatoNombre = p != null ? p.descripcion : null,
                        Estado = c.estado,
                        Bonificado = c.bonificado,
                        Calificacion = c.calificacion,
                        Monto = c.monto,
                        Invitado = c.invitado
                    };

                    var resultados = read.ToList();

                    // ===================== LOGGING: Reporte exitoso =====================
                    _logger?.LogInformation("ObtenerComandasReporte: Comandas obtenidas exitosamente", new
                    {
                        FechaDesde = fechadesde,
                        FechaHasta = fechahasta,
                        TotalComandas = resultados.Count
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerComandasReporte: Error al obtener comandas para reporte", ex, new
                {
                    FechaDesde = fechadesde,
                    FechaHasta = fechahasta,
                    PlatoId = platoId,
                    CentrodecostoId = centrodecostoId,
                    ProyectoId = proyectoId,
                    PlantaId = plantaId
                });
                throw;
            }
        }

        // ============================================
        // Reporte de gestión (tabla detallada)
        // ============================================
        public List<ReporteGestionDto> ObtenerReporteGestion(DateTime? fechaDesde = null, DateTime? fechaHasta = null, int? platoId = null, int? proyectoId = null, int? plantaId = null, int? jerarquiaId = null, int? centrodecostoId = null, string estado = "")
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
                throw new Exception("La fecha de inicio no puede ser posterior a la fecha de fin.");

            if (fechaDesde.HasValue && fechaHasta.HasValue)
            {
                var diasDiferencia = (fechaHasta.Value - fechaDesde.Value).Days;
                if (diasDiferencia > 365)
                    throw new Exception("El rango de fechas no puede exceder 365 días.");
            }

            if (platoId.HasValue && platoId.Value <= 0)
                throw new Exception("El ID del plato debe ser mayor a 0.");

            if (proyectoId.HasValue && proyectoId.Value <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            if (plantaId.HasValue && plantaId.Value <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (jerarquiaId.HasValue && jerarquiaId.Value <= 0)
                throw new Exception("El ID de la jerarquía debe ser mayor a 0.");

            if (centrodecostoId.HasValue && centrodecostoId.Value <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    // ===================== LOGGING: Inicio de reporte =====================
                    _logger?.LogInformation("ObtenerReporteGestion: Iniciando obtención de reporte de gestión", new
                    {
                        FechaDesde = fechaDesde,
                        FechaHasta = fechaHasta,
                        PlatoId = platoId,
                        ProyectoId = proyectoId,
                        PlantaId = plantaId,
                        JerarquiaId = jerarquiaId,
                        CentrodecostoId = centrodecostoId,
                        Estado = estado
                    });

                    // Normalizar fechas en C# antes de usar en LINQ (EF no puede traducir .Date)
                    DateTime? desde = null;
                    if (fechaDesde.HasValue)
                    {
                        desde = fechaDesde.Value.Date;
                    }
                    
                    DateTime? hastaInc = null;
                    if (fechaHasta.HasValue)
                    {
                        hastaInc = fechaHasta.Value.Date.AddDays(1);
                    }

                var q = ctx.sl_comanda.AsNoTracking().Where(c => c.deletemark == false);

                // Aplicar filtros
                if (desde.HasValue)
                    q = q.Where(c => c.fecha >= desde.Value);
                if (hastaInc.HasValue)
                    q = q.Where(c => c.fecha < hastaInc.Value);
                if (platoId.HasValue && platoId.Value > 0)
                    q = q.Where(c => c.plato_id == platoId.Value);
                if (proyectoId.HasValue && proyectoId.Value > 0)
                    q = q.Where(c => c.proyecto_id == proyectoId.Value);
                if (plantaId.HasValue && plantaId.Value > 0)
                    q = q.Where(c => c.planta_id == plantaId.Value);
                if (jerarquiaId.HasValue && jerarquiaId.Value > 0)
                    q = q.Where(c => c.jerarquia_id == jerarquiaId.Value);
                if (centrodecostoId.HasValue && centrodecostoId.Value > 0)
                    q = q.Where(c => c.centrodecosto_id == centrodecostoId.Value);
                if (estado != string.Empty)
                    q = q.Where(c => c.estado == estado);
                // LEFT JOIN a todas las tablas relacionadas
                var read =
                    from c in q
                    join u in ctx.sl_usuario.AsNoTracking() on c.usuario_id equals u.id into gu
                    from u in gu.DefaultIfEmpty()
                    join p in ctx.sl_plato.AsNoTracking() on c.plato_id equals p.id into gp
                    from plato in gp.DefaultIfEmpty()
                    join pla in ctx.sl_planta.AsNoTracking() on c.planta_id equals pla.id into gpla
                    from planta in gpla.DefaultIfEmpty()
                    join cc in ctx.sl_centrodecosto.AsNoTracking() on c.centrodecosto_id equals cc.id into gcc
                    from centroCosto in gcc.DefaultIfEmpty()
                    join proy in ctx.sl_proyecto.AsNoTracking() on c.proyecto_id equals proy.id into gproy
                    from proyecto in gproy.DefaultIfEmpty()
                    join jer in ctx.sl_jerarquia.AsNoTracking() on c.jerarquia_id equals jer.id into gjer
                    from jerarquia in gjer.DefaultIfEmpty()
                    orderby c.fecha descending, c.id descending
                    select new
                    {
                        Fecha = c.fecha,
                        Planta = planta != null ? planta.nombre : null,
                        CC = centroCosto != null ? centroCosto.nombre : null,
                        Proyecto = proyecto != null ? proyecto.nombre : null,
                        Perfil = jerarquia != null ? jerarquia.nombre : null,
                        Legajo = u != null ? u.legajo : 0,
                        UsuarioNombre = u != null ? u.nombre : null,
                        UsuarioApellido = u != null ? u.apellido : null,
                        PlatoCodigo = plato != null ? plato.codigo : null,
                        PlatoDescripcion = plato != null ? plato.descripcion : null,
                        Estado = c.estado ?? "",
                        Bonificacion = c.bonificado,
                        Costo = c.monto
                    };

                // Materializar la consulta y luego transformar en memoria
                var resultados = read.ToList();
                
                    var resultadosFinales = resultados.Select(r => new ReporteGestionDto
                    {
                        Fecha = r.Fecha,
                        Planta = r.Planta,
                        CC = r.CC,
                        Proyecto = r.Proyecto,
                        Perfil = r.Perfil,
                        Legajo = r.Legajo,
                        NombreCompleto = !string.IsNullOrWhiteSpace(r.UsuarioNombre) || !string.IsNullOrWhiteSpace(r.UsuarioApellido)
                            ? ((r.UsuarioNombre ?? "") + " " + (r.UsuarioApellido ?? "")).Trim()
                            : null,
                        Plato = !string.IsNullOrWhiteSpace(r.PlatoCodigo) && !string.IsNullOrWhiteSpace(r.PlatoDescripcion)
                            ? $"{r.PlatoCodigo}-{r.PlatoDescripcion}"
                            : (r.PlatoDescripcion ?? r.PlatoCodigo ?? ""),
                        Estado = r.Estado,
                        Bonificacion = r.Bonificacion,
                        Costo = r.Costo
                    }).ToList();

                    // ===================== LOGGING: Reporte exitoso =====================
                    _logger?.LogInformation("ObtenerReporteGestion: Reporte de gestión obtenido exitosamente", new
                    {
                        FechaDesde = fechaDesde,
                        FechaHasta = fechaHasta,
                        TotalRegistros = resultadosFinales.Count
                    });

                    return resultadosFinales;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerReporteGestion: Error al obtener reporte de gestión", ex, new
                {
                    FechaDesde = fechaDesde,
                    FechaHasta = fechaHasta,
                    PlatoId = platoId,
                    ProyectoId = proyectoId,
                    PlantaId = plantaId,
                    JerarquiaId = jerarquiaId,
                    CentrodecostoId = centrodecostoId,
                    Estado = estado
                });
                throw;
            }
        }

        /// <summary>
        /// Comandas en estado Recibido (R) en el rango de fechas; reparto empleado/empresa según bonificación de la jerarquía del usuario.
        /// </summary>
        public List<ReporteFacturacionDTO> ObtenerReporteFacturacion(
            DateTime fechaDesde,
            DateTime fechaHasta,
            int? plantaId = null,
            int? proyectoId = null,
            int? centrodecostoId = null)
        {
            var desde = fechaDesde.Date;
            var hastaExclusivo = fechaHasta.Date.AddDays(1);

            if (desde > fechaHasta.Date)
                throw new Exception("La fecha de inicio no puede ser posterior a la fecha de fin.");

            if ((fechaHasta.Date - fechaDesde.Date).Days > 365)
                throw new Exception("El rango de fechas no puede exceder 365 días.");

            if (plantaId.HasValue && plantaId.Value <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (proyectoId.HasValue && proyectoId.Value <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            if (centrodecostoId.HasValue && centrodecostoId.Value <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    _logger?.LogInformation("ObtenerReporteFacturacion: Iniciando reporte", new
                    {
                        FechaDesde = desde,
                        FechaHasta = fechaHasta.Date,
                        PlantaId = plantaId,
                        ProyectoId = proyectoId,
                        CentrodecostoId = centrodecostoId
                    });

                    var q = ctx.sl_comanda.AsNoTracking()
                        .Where(c => !c.deletemark && c.estado == "R" && c.fecha >= desde && c.fecha < hastaExclusivo);

                    if (plantaId.HasValue && plantaId.Value > 0)
                        q = q.Where(c => c.planta_id == plantaId.Value);
                    if (proyectoId.HasValue && proyectoId.Value > 0)
                        q = q.Where(c => c.proyecto_id == proyectoId.Value);
                    if (centrodecostoId.HasValue && centrodecostoId.Value > 0)
                        q = q.Where(c => c.centrodecosto_id == centrodecostoId.Value);

                    var raw = (
                        from c in q
                        join u in ctx.sl_usuario.AsNoTracking() on c.usuario_id equals u.id
                        join p in ctx.sl_plato.AsNoTracking() on c.plato_id equals p.id into gp
                        from plato in gp.DefaultIfEmpty()
                        join pl in ctx.sl_planta.AsNoTracking() on c.planta_id equals pl.id into gpl
                        from planta in gpl.DefaultIfEmpty()
                        orderby c.fecha, c.id
                        select new
                        {
                            u.legajo,
                            u.apellido,
                            u.nombre,
                            UsuarioActivo = !u.deletemark,
                            c.fecha,
                            PlatoDescripcion = plato != null ? plato.descripcion : null,
                            PlantaNombre = planta != null ? planta.nombre : null,
                            PlatoImporte = plato != null ? plato.costo : 0m,
                            // Snapshot congelado al momento del pedido: no se relee sl_plato.costo_proveedor
                            // en vivo, para que este reporte no cambie retroactivamente si el costo del
                            // plato se actualiza después.
                            c.costo_proveedor,
                            c.bonificado,
                            // Monto que realmente pagó el empleado, ya calculado y congelado al momento del
                            // pedido por el motor de reglas de bonificación (ServicioComanda.CrearConDescuento).
                            c.monto
                        }).ToList();

                    var resultado = raw.Select(r =>
                    {
                        var platoImporte = r.PlatoImporte;
                        // El reparto se deriva de lo que realmente quedó grabado en la comanda, no se
                        // recalcula acá — así queda correcto sin importar qué regla (o ninguna) haya
                        // aplicado al momento del pedido.
                        var montoEmpleado = r.monto;
                        var montoEmpresa = platoImporte - montoEmpleado;

                        return new ReporteFacturacionDTO
                        {
                            Legajo = r.legajo.ToString(),
                            Apellido = r.apellido ?? string.Empty,
                            Nombre = r.nombre ?? string.Empty,
                            EstadoUsuario = r.UsuarioActivo ? "Activo" : "Inactivo",
                            Fecha = r.fecha,
                            PlatoDescripcion = r.PlatoDescripcion ?? string.Empty,
                            PlantaNombre = r.PlantaNombre ?? string.Empty,
                            PlatoImporte = platoImporte,
                            MontoEmpleado = montoEmpleado,
                            MontoEmpresa = montoEmpresa,
                            CostoProveedor = r.costo_proveedor,
                            Bonificado = r.bonificado
                        };
                    }).ToList();

                    _logger?.LogInformation("ObtenerReporteFacturacion: Reporte obtenido", new { TotalRegistros = resultado.Count });

                    return resultado;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerReporteFacturacion: Error", ex, new
                {
                    FechaDesde = fechaDesde,
                    FechaHasta = fechaHasta,
                    PlantaId = plantaId,
                    ProyectoId = proyectoId,
                    CentrodecostoId = centrodecostoId
                });
                throw;
            }
        }
    }
}

