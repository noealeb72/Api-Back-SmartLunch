using smartlunch_api.Dtos;
using smartlunch_api.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Text;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioComanda
    {
        PagedResultDto<ComandaListadoDto> ObtenerLista(int page, int pageSize, DateTime? fechaDesde, DateTime? fechaHasta, int? usuarioId, string search, bool soloActivos, string estado = null);
        ComandaDetalleDto ObtenerPorId(int id);
        ComandaDetalleDto Crear(ComandaCreateDto dto, int usuarioId);
        ComandaDetalleDto CrearConDescuento(ComandaCreateDto dto, int usuarioId);
        List<PreviewBonificacionDto> PrevisualizarBonificacion(int usuarioId);
        void Actualizar(ComandaUpdateDto dto);
        void Eliminar(int id);
        void Activar(int id);
        void Cancelar(int id);
        ComandaDetalleDto Despachar(int npedido);
        void Devolver(int npedido, int? calificacion = null);
        ComandaDetalleDto Recibir(int npedido, int? calificacion = null);
        List<ComandaDetalleDto> ObtenerXDia(DateTime fecha, int usuarioId);
        // Método sobrecargado con más parámetros (usado internamente por ServicioInicio)
        List<ComandaDetalleDto> ObtenerXDia(DateTime? fechaDesde, DateTime? fechaHasta, int usuarioId, int turnoId, int plantaId, int centroCostoId, int proyectoId, int jerarquiaId, string estado);
        List<ComandaImpresionDto> ObtenerDatosImpresion(ComandaImpresionRequestDto request);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioComanda : IServicioComanda
    {
        private readonly ILoggerService _logger;
        private readonly IServicioReglaBonificacion _servicioReglaBonificacion;

        public ServicioComanda(ILoggerService logger = null, IServicioReglaBonificacion servicioReglaBonificacion = null)
        {
            _logger = logger;
            _servicioReglaBonificacion = servicioReglaBonificacion ?? new ServicioReglaBonificacion();
        }

        // ===================== MÉTODOS HELPER =====================

        /// <summary>
        /// Maneja excepciones de validación de Entity Framework y genera mensajes descriptivos
        /// </summary>
        private Exception HandleValidationException(DbEntityValidationException ex, string operacion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se produjeron errores de validación al {operacion} la comanda:");
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

        /// <summary>
        /// Construye un ComandaDetalleDto desde una entidad y sus relaciones cargadas
        /// </summary>
        private ComandaDetalleDto ConstruirComandaDetalleDto(sl_comanda entity, sl_usuario usuario, sl_menudd menu = null)
        {
            // Si no viene el menú, intentar obtenerlo desde la entidad (si tiene navegación)
            if (menu == null && entity.menudd_id > 0)
            {
                // Nota: Esto requeriría cargar el menú, pero por ahora usamos los datos de la entidad
            }

            return new ComandaDetalleDto
            {
                Id = entity.id,
                Npedido = entity.npedido,
                Fecha = entity.fecha,
                Monto = entity.monto,
                Bonificado = entity.bonificado,
                Invitado = entity.invitado,
                Calificacion = entity.calificacion,
                Estado = entity.estado,
                Comentario = entity.comentario,
                UsuarioId = entity.usuario_id,
                UsuarioNombre = usuario != null ? $"{usuario.nombre} {usuario.apellido}" : "",
                PlatoId = entity.plato_id,
                PlatoDescripcion = entity.Plato?.descripcion ?? "",
                TurnoId = entity.turno_id,
                TurnoNombre = entity.Turno?.nombre ?? "",
                PlantaId = entity.planta_id,
                PlantaDescripcion = entity.Planta?.nombre ?? "",
                CentroDeCostoId = entity.centrodecosto_id,
                CentroDeCostoDescripcion = entity.CentroDeCosto?.descripcion ?? "",
                ProyectoId = entity.proyecto_id,
                ProyectoDescripcion = entity.Proyecto?.nombre ?? "",
                JerarquiaId = entity.jerarquia_id,
                JerarquiaDescripcion = entity.Jerarquia?.nombre ?? "",
                PlatoImporte = entity.Plato?.costo ?? 0,
                CostoProveedor = entity.costo_proveedor,
                Foto = entity.Plato?.foto,
                NutricionalId = entity.Plato?.plannutricional_id ?? 0,
                NutricionalNombre = entity.Plato?.plannutricional?.nombre ?? ""
            };
        }
        
        public PagedResultDto<ComandaListadoDto> ObtenerLista(
        int page,
        int pageSize,
        DateTime? fechaDesde,
        DateTime? fechaHasta,
        int? usuarioId,
        string search,
        bool soloActivos,
        string estado = null)
            {
                // ===================== VALIDACIÓN DE ENTRADA =====================
                if (page < 1) page = 1;
                if (pageSize <= 0 || pageSize > 100) pageSize = 20;

                // Validar rango de fechas
                if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
                    throw new Exception("La fecha desde no puede ser mayor que la fecha hasta.");

                // Validar usuarioId si se proporciona
                if (usuarioId.HasValue && usuarioId.Value <= 0)
                    throw new Exception("El ID del usuario debe ser mayor a 0.");

                // Validar longitud de búsqueda
                if (!string.IsNullOrWhiteSpace(search) && search.Length > 200)
                    throw new Exception("El texto de búsqueda no puede exceder 200 caracteres.");

                try
                {
                    using (var ctx = new DataContext())
                    {
                        ctx.Configuration.LazyLoadingEnabled = false;

                        // ===================== LOGGING: Inicio de búsqueda =====================
                        _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de comandas", new
                        {
                            Page = page,
                            PageSize = pageSize,
                            FechaDesde = fechaDesde,
                            FechaHasta = fechaHasta,
                            UsuarioId = usuarioId,
                            HasSearch = !string.IsNullOrWhiteSpace(search),
                            SoloActivos = soloActivos
                        });

                    var query = ctx.sl_comanda
                        .Include(c => c.Usuario)
                        .Include(c => c.Plato)
                        .Include(c => c.Turno)
                        .Include(c => c.Planta)
                        .Include(c => c.CentroDeCosto)
                        .Include(c => c.Proyecto)
                        .Include(c => c.Jerarquia)
                        .AsQueryable();

                    // Activos / eliminados lógicos
                    query = soloActivos
                        ? query.Where(x => !x.deletemark)
                        : query.Where(x => x.deletemark);

                    // Fechas: filtrar solo por día (ignorar hora) usando TruncateTime para que coincida con registros que tienen fecha+hora
                    DateTime? desde = fechaDesde.HasValue ? fechaDesde.Value.Date : (DateTime?)null;
                    DateTime? hasta = fechaHasta.HasValue ? fechaHasta.Value.Date : (DateTime?)null;
                    if (desde.HasValue)
                        query = query.Where(m => DbFunctions.TruncateTime(m.fecha) >= desde.Value);
                    if (hasta.HasValue)
                        query = query.Where(m => DbFunctions.TruncateTime(m.fecha) <= hasta.Value);

                    // Usuario
                    if (usuarioId.HasValue && usuarioId.Value > 0)
                        query = query.Where(m => m.usuario_id == usuarioId.Value);

                    // Estado: "Todos" o null = todos los estados; "P", "E", "R", etc. = filtrar por ese estado
                    if (!string.IsNullOrWhiteSpace(estado) && !estado.Trim().Equals("Todos", StringComparison.OrdinalIgnoreCase))
                        query = query.Where(m => m.estado == estado.Trim());

                    // Búsqueda
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();

                        query = query.Where(m =>
                            (m.Turno != null && (m.Turno.nombre ?? "").ToLower().Contains(s)) ||
                            (m.Plato != null && ((m.Plato.descripcion ?? "").ToLower().Contains(s) ||
                                                 (m.Plato.codigo ?? "").ToLower().Contains(s))) ||
                            (m.Planta != null && (m.Planta.nombre ?? "").ToLower().Contains(s)) ||
                            (m.CentroDeCosto != null && (m.CentroDeCosto.nombre ?? "").ToLower().Contains(s)) ||
                            (m.Proyecto != null && (m.Proyecto.nombre ?? "").ToLower().Contains(s)) ||
                            (m.Jerarquia != null && (m.Jerarquia.nombre ?? "").ToLower().Contains(s))
                        );
                    }

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderByDescending(c => c.fecha)
                        .ThenByDescending(c => c.id)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(c => new ComandaListadoDto
                        {
                            Id = c.id,
                            Npedido = c.npedido,
                            Fecha = c.fecha,
                            Monto = c.monto,
                            Bonificado = c.bonificado,
                            Invitado = c.invitado,
                            Calificacion = c.calificacion,
                            Estado = c.estado,
                            Comentario = c.comentario,
                            UsuarioId = c.usuario_id,
                            UsuarioNombre = c.Usuario.nombre + " " + c.Usuario.apellido,
                            PlatoId = c.plato_id,
                            PlatoDescripcion = c.Plato.descripcion,
                            TurnoId = c.turno_id,
                            TurnoNombre = c.Turno.nombre,
                            PlantaId = c.planta_id,
                            PlantaDescripcion = c.Planta.nombre,
                            CentroDeCostoId = c.centrodecosto_id,
                            CentroDeCostoDescripcion = c.CentroDeCosto.descripcion,
                            ProyectoId = c.proyecto_id,
                            ProyectoDescripcion = c.Proyecto.nombre,
                            JerarquiaId = c.jerarquia_id,
                            JerarquiaDescripcion = c.Jerarquia.nombre,
                            Foto = c.Usuario.foto,
                        })
                        .ToList();

                        var resultado = new PagedResultDto<ComandaListadoDto>
                        {
                            page = page,
                            pageSize = pageSize,
                            totalItems = totalItems,
                            totalPages = totalPages,
                            items = items
                        };

                        // ===================== LOGGING: Búsqueda exitosa =====================
                        _logger?.LogInformation("ObtenerLista: Búsqueda completada exitosamente", new
                        {
                            Page = page,
                            PageSize = pageSize,
                            TotalItems = totalItems,
                            TotalPages = totalPages,
                            ItemsReturned = items.Count
                        });

                        return resultado;
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogError("ObtenerLista: Error al obtener lista de comandas", ex, new
                    {
                        Page = page,
                        PageSize = pageSize,
                        ExceptionType = ex.GetType().Name
                    });
                    throw;
                }
            }


        public ComandaDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_comanda
                        .Where(c => c.id == id && !c.deletemark)
                        .Include(c => c.Usuario)
                        .Include(c => c.Plato)
                        .Include(c => c.Plato.plannutricional)
                        .Include(c => c.Turno)
                        .Include(c => c.Planta)
                        .Include(c => c.CentroDeCosto)
                        .Include(c => c.Proyecto)
                        .Include(c => c.Jerarquia)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Comanda no encontrada", new { ComandaId = id });
                        throw new Exception("Comanda no encontrada.");
                    }

                    // ===================== LOGGING: Acceso a comanda =====================
                    _logger?.LogInformation("ObtenerPorId: Comanda obtenida", new
                    {
                        ComandaId = entity.id,
                        Npedido = entity.npedido,
                        Estado = entity.estado
                    });

                    // Construir DTO desde la entidad ya cargada
                    var dto = new ComandaDetalleDto
                    {
                        Id = entity.id,
                        Npedido = entity.npedido,
                        Fecha = entity.fecha,
                        Monto = entity.monto,
                        Bonificado = entity.bonificado,
                        Invitado = entity.invitado,
                        Calificacion = entity.calificacion,
                        Estado = entity.estado,
                        Comentario = entity.comentario,
                        UsuarioId = entity.usuario_id,
                        UsuarioNombre = entity.Usuario != null ? $"{entity.Usuario.nombre} {entity.Usuario.apellido}" : "",
                        PlatoId = entity.plato_id,
                        PlatoDescripcion = entity.Plato?.descripcion ?? "",
                        TurnoId = entity.turno_id,
                        TurnoNombre = entity.Turno?.nombre ?? "",
                        PlantaId = entity.planta_id,
                        PlantaDescripcion = entity.Planta?.nombre ?? "",
                        CentroDeCostoId = entity.centrodecosto_id,
                        CentroDeCostoDescripcion = entity.CentroDeCosto?.descripcion ?? "",
                        ProyectoId = entity.proyecto_id,
                        ProyectoDescripcion = entity.Proyecto?.nombre ?? "",
                        JerarquiaId = entity.jerarquia_id,
                        JerarquiaDescripcion = entity.Jerarquia?.nombre ?? "",
                        PlatoImporte = entity.Plato?.costo ?? 0,
                        CostoProveedor = entity.costo_proveedor,
                        Foto = entity.Plato?.foto,
                        NutricionalId = entity.Plato?.plannutricional_id ?? 0,
                        NutricionalNombre = entity.Plato?.plannutricional?.nombre ?? ""
                    };

                    return dto;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message == "Comanda no encontrada."))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener comanda", ex, new { ComandaId = id });
                throw;
            }
        }

        public ComandaDetalleDto Crear(ComandaCreateDto dto, int usuarioId)
        {
            if (dto == null)
                throw new Exception("Datos inválidos.");

            // ===================== VALIDACIÓN DE ENTRADA PARA SQL RAW QUERIES =====================
            // Validar rango válido del ID antes de ejecutar cualquier consulta SQL
            if (dto.MenuddId <= 0)
                throw new Exception("El ID del menú debe ser mayor a 0.");

            // Validar usuarioId también (se usa en la creación de la comanda)
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // ===================== VALIDAR QUE EL MENÚ EXISTA Y ESTÉ ACTIVO (ANTES DE SQL) =====================
                // Esta validación se hace ANTES de ejecutar SQL raw queries para evitar consultas innecesarias
                // Cargar menú con todas las relaciones necesarias para construir el DTO directamente
                var menu = ctx.sl_menudd
                    .Include(m => m.Plato)
                    .Include(m => m.Plato.plannutricional)
                    .Include(m => m.Turno)
                    .Include(m => m.Planta)
                    .Include(m => m.CentroDeCosto)
                    .Include(m => m.Proyecto)
                    .Include(m => m.Jerarquia)
                    .FirstOrDefault(m => m.id == dto.MenuddId && !m.deletemark);
                if (menu == null)
                    throw new Exception("No existe el menú del día o está dado de baja.");

                // ===================== VALIDAR JERARQUÍA NO NULL =====================
                // La jerarquía es requerida para crear la comanda
                if (!menu.jerarquia_id.HasValue)
                    throw new Exception("El menú del día no tiene una jerarquía asignada.");

                // ===================== VALIDAR QUE EL USUARIO EXISTA Y ESTÉ ACTIVO =====================
                var usuario = ctx.sl_usuario.FirstOrDefault(u => u.id == usuarioId && !u.deletemark);
                if (usuario == null)
                    throw new Exception("El usuario especificado no existe o está inactivo.");

                // ===================== LOGGING: Inicio de creación de comanda =====================
                _logger?.LogInformation("Crear: Iniciando creación de comanda", new
                {
                    UsuarioId = usuarioId,
                    MenuddId = dto.MenuddId,
                    PlatoId = menu.plato_id,
                    TurnoId = menu.turno_id,
                    Monto = dto.Monto,
                    Bonificado = dto.Bonificado,
                    Invitado = dto.Invitado
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // Sequence en vez de MAX()+UPDLOCK/HOLDLOCK, que serializaba todos los pedidos de la app
                        int nextNPedido = ctx.Database.SqlQuery<int>(
                            "SELECT NEXT VALUE FOR dbo.seq_npedido"
                        ).Single();

                        // ===================== VALIDAR CUPO Y RESERVAR (SUMAR COMANDA) EN 1 SOLO PASO =====================
                        // Esto asegura que comandas nunca supere cantidad, incluso con concurrencia.

                        int rows = ctx.Database.ExecuteSqlCommand(@"
                            UPDATE sl_menudd
                            SET comandas = ISNULL(comandas,0) + 1
                            WHERE id = @p0
                              AND deletemark = 0
                              AND ISNULL(comandas,0) < cantidad
                        ", dto.MenuddId);

                        if (rows == 0)
                            throw new Exception("No hay cantidad disponible para realizar el pedido (cupo agotado).");

                        // ===================== CREAR COMANDA =====================
                        // Usar valores del menú como fuente de verdad para garantizar consistencia de datos
                        // Los valores del DTO (monto, bonificado, invitado, estado, comentario) vienen validados del frontend
                         var entity = new sl_comanda
                         {
                             npedido = nextNPedido,
                             menudd_id = dto.MenuddId,
                             usuario_id = usuarioId,

                             // ✅ Usar valores del menú (fuente de verdad) para garantizar consistencia
                             plato_id = menu.plato_id,
                             turno_id = menu.turno_id,
                             // Fecha y hora completas (día del menú + hora/minuto/segundo de creación)
                             fecha = menu.fecha.Date.Add(DateTime.Now.TimeOfDay),
                             planta_id = menu.planta_id,
                             centrodecosto_id = menu.centrodecosto_id,
                             proyecto_id = menu.proyecto_id,
                             jerarquia_id = menu.jerarquia_id.Value,  // Ya validado que no es null arriba

                             // Valores del DTO (validados en frontend)
                             monto = dto.Monto,
                             // Snapshot del costo de proveedor al momento del pedido (no se relee en vivo después)
                             costo_proveedor = menu.Plato?.costo_proveedor ?? 0m,
                             bonificado = dto.Bonificado,
                             invitado = dto.Invitado,
                             calificacion = dto.Calificacion,

                             estado = string.IsNullOrWhiteSpace(dto.Estado)
                                ? "P"
                                : (dto.Estado.Trim().Length > 20 ? dto.Estado.Trim().Substring(0, 20) : dto.Estado.Trim()),

                             comentario = !string.IsNullOrEmpty(dto.Comentario)
                                ? (dto.Comentario.Length > 200 ? dto.Comentario.Substring(0, 200) : dto.Comentario)
                                : dto.Comentario,

                             deletemark = false,
                         };

                         ctx.sl_comanda.Add(entity);

                         // Guardar
                         ctx.SaveChanges();
                         tx.Commit();

                         // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                         // Construir el DTO desde los datos ya cargados en memoria
                         var resultado = new ComandaDetalleDto
                         {
                             Id = entity.id,
                             Npedido = entity.npedido,
                             Fecha = entity.fecha,
                             Monto = entity.monto,
                             Bonificado = entity.bonificado,
                             Invitado = entity.invitado,
                             Calificacion = entity.calificacion,
                             Estado = entity.estado,
                             Comentario = entity.comentario,
                             UsuarioId = entity.usuario_id,
                             UsuarioNombre = $"{usuario.nombre} {usuario.apellido}",
                             PlatoId = entity.plato_id,
                             PlatoDescripcion = menu.Plato?.descripcion ?? "",
                             TurnoId = entity.turno_id,
                             TurnoNombre = menu.Turno?.nombre ?? "",
                             PlantaId = entity.planta_id,
                             PlantaDescripcion = menu.Planta?.nombre ?? "",
                             CentroDeCostoId = entity.centrodecosto_id,
                             CentroDeCostoDescripcion = menu.CentroDeCosto?.descripcion ?? "",
                             ProyectoId = entity.proyecto_id,
                             ProyectoDescripcion = menu.Proyecto?.nombre ?? "",
                             JerarquiaId = entity.jerarquia_id,
                             JerarquiaDescripcion = menu.Jerarquia?.nombre ?? "",
                             PlatoImporte = menu.Plato?.costo ?? 0,
                             CostoProveedor = entity.costo_proveedor,
                             Foto = menu.Plato?.foto,
                             NutricionalId = menu.Plato?.plannutricional_id ?? 0,
                             NutricionalNombre = menu.Plato?.plannutricional?.nombre ?? ""
                         };

                         // ===================== LOGGING: Comanda creada exitosamente =====================
                         _logger?.LogInformation("Crear: Comanda creada exitosamente", new
                         {
                             ComandaId = resultado.Id,
                             Npedido = resultado.Npedido,
                             UsuarioId = resultado.UsuarioId,
                             MenuddId = dto.MenuddId,
                             Estado = resultado.Estado
                         });

                         return resultado;
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        
                        // ===================== LOGGING: Deadlock detectado =====================
                        _logger?.LogWarning("Crear: Deadlock detectado al crear comanda", ex, new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            ErrorNumber = ex.Number,
                            ErrorMessage = ex.Message
                        });
                        
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();

                        // ===================== LOGGING: Error de validación =====================
                        var validationException = HandleValidationException(ex, "crear");
                        _logger?.LogError("Crear: Error de validación al guardar comanda", ex, new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            ValidationErrors = validationException.Message
                        });
                        
                        throw validationException;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        
                        // ===================== LOGGING: Error general =====================
                        _logger?.LogError("Crear: Error al crear comanda", ex, new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            ExceptionType = ex.GetType().Name,
                            ExceptionMessage = ex.Message
                        });
                        
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Crea una comanda calculando automáticamente el monto según bonificaciones.
        /// Similar a Crear pero calcula el descuento basándose en:
        /// - Bonificación por día del menú (si existe)
        /// - Bonificación de la jerarquía (si no hay bonificación por día)
        /// - Flag bonificado del DTO
        /// </summary>
        public ComandaDetalleDto CrearConDescuento(ComandaCreateDto dto, int usuarioId)
        {
            if (dto == null)
                throw new Exception("Datos inválidos.");

            // ===================== VALIDACIÓN DE ENTRADA PARA SQL RAW QUERIES =====================
            // Validar rango válido del ID antes de ejecutar cualquier consulta SQL
            if (dto.MenuddId <= 0)
                throw new Exception("El ID del menú debe ser mayor a 0.");

            // Validar usuarioId también (se usa en la creación de la comanda)
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // ===================== VALIDAR QUE EL MENÚ EXISTA Y ESTÉ ACTIVO (ANTES DE SQL) =====================
                // Esta validación se hace ANTES de ejecutar SQL raw queries para evitar consultas innecesarias
                // Cargar menú con todas las relaciones necesarias para construir el DTO directamente
                var menu = ctx.sl_menudd
                    .Include(m => m.Plato)
                    .Include(m => m.Plato.plannutricional)
                    .Include(m => m.Turno)
                    .Include(m => m.Planta)
                    .Include(m => m.CentroDeCosto)
                    .Include(m => m.Proyecto)
                    .Include(m => m.Jerarquia)
                    .FirstOrDefault(m => m.id == dto.MenuddId && !m.deletemark);
                if (menu == null)
                    throw new Exception("No existe el menú del día o está dado de baja.");

                // ===================== VALIDAR JERARQUÍA NO NULL =====================
                // La jerarquía es requerida para crear la comanda
                if (!menu.jerarquia_id.HasValue)
                    throw new Exception("El menú del día no tiene una jerarquía asignada.");

                // ===================== VALIDAR QUE EL USUARIO EXISTA Y ESTÉ ACTIVO =====================
                var usuario = ctx.sl_usuario.FirstOrDefault(u => u.id == usuarioId && !u.deletemark);
                if (usuario == null)
                    throw new Exception("El usuario especificado no existe o está inactivo.");

                // ===================== CALCULAR MONTO CON DESCUENTO =====================
                // 1. Obtener costo del plato
                decimal costoPlato = menu.Plato?.costo ?? 0;
                if (costoPlato <= 0)
                    throw new Exception("El plato no tiene un costo válido.");

                // 1b. Costo de proveedor: se congela en la comanda al momento del pedido
                // para que no cambie retroactivamente si el costo del plato se actualiza después.
                decimal costoProveedor = menu.Plato?.costo_proveedor ?? 0m;

                // ===================== LOGGING: Inicio de creación de comanda con descuento =====================
                _logger?.LogInformation("CrearConDescuento: Iniciando creación de comanda con cálculo de descuento", new
                {
                    UsuarioId = usuarioId,
                    MenuddId = dto.MenuddId,
                    PlatoId = menu.plato_id,
                    TurnoId = menu.turno_id,
                    CostoPlato = costoPlato,
                    Invitado = dto.Invitado
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // ===================== POSICIÓN DEL PEDIDO Y REGLA DE BONIFICACIÓN APLICABLE =====================
                        // Se calcula DENTRO de la transacción serializable (misma que reserva el cupo más abajo)
                        // para que dos pedidos casi simultáneos del mismo usuario no vean ambos "es mi primer
                        // pedido de hoy". "Hoy" es la fecha del menú, y cuenta cualquier comanda activa (no
                        // cancelada) del usuario en esa fecha, sin importar turno, comedor ni tipo de plato.
                        var fechaDelMenu = menu.fecha.Date;
                        int pedidosActivosHoy = ctx.sl_comanda.Count(c =>
                            c.usuario_id == usuarioId &&
                            !c.deletemark &&
                            c.estado != "C" &&
                            DbFunctions.TruncateTime(c.fecha) == fechaDelMenu);
                        int posicionPedido = pedidosActivosHoy + 1;

                        var reglaAplicable = _servicioReglaBonificacion.ObtenerReglaAplicable(
                            ctx,
                            menu.turno_id,
                            usuario.jerarquia_id,
                            menu.planta_id,
                            menu.Plato?.plannutricional_id ?? 0,
                            menu.plato_id,
                            dto.Invitado,
                            posicionPedido,
                            menu.fecha);

                        // Mismo cálculo que usa la previsualización (PrevisualizarBonificacion), para que
                        // el precio que se le mostró al comensal antes de confirmar y lo que efectivamente
                        // se cobra acá no puedan divergir.
                        decimal montoCalculado = _servicioReglaBonificacion.AplicarEfecto(costoPlato, reglaAplicable);
                        bool bonificadoResultante = montoCalculado < costoPlato;

                        _logger?.LogInformation("CrearConDescuento: Regla de bonificación evaluada", new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            PosicionPedido = posicionPedido,
                            ReglaId = reglaAplicable?.id,
                            ReglaNombre = reglaAplicable?.nombre,
                            MontoCalculado = montoCalculado,
                            Bonificado = bonificadoResultante
                        });

                        // Sequence en vez de MAX()+UPDLOCK/HOLDLOCK, que serializaba todos los pedidos de la app
                        int nextNPedido = ctx.Database.SqlQuery<int>(
                            "SELECT NEXT VALUE FOR dbo.seq_npedido"
                        ).Single();

                        // ===================== VALIDAR CUPO Y RESERVAR (SUMAR COMANDA) EN 1 SOLO PASO =====================
                        // Esto asegura que comandas nunca supere cantidad, incluso con concurrencia.

                        int rows = ctx.Database.ExecuteSqlCommand(@"
                            UPDATE sl_menudd
                            SET comandas = ISNULL(comandas,0) + 1
                            WHERE id = @p0
                              AND deletemark = 0
                              AND ISNULL(comandas,0) < cantidad
                        ", dto.MenuddId);

                        if (rows == 0)
                            throw new Exception("No hay cantidad disponible para realizar el pedido (cupo agotado).");

                        // ===================== CREAR COMANDA =====================
                        // Usar valores del menú como fuente de verdad para garantizar consistencia de datos
                        // El monto se calcula automáticamente según bonificaciones
                        var entity = new sl_comanda
                        {
                            npedido = nextNPedido,
                            menudd_id = dto.MenuddId,
                            usuario_id = usuarioId,

                            // ✅ Usar valores del menú (fuente de verdad) para garantizar consistencia
                            plato_id = menu.plato_id,
                            turno_id = menu.turno_id,
                            // Fecha y hora completas (día del menú + hora/minuto/segundo de creación)
                            fecha = menu.fecha.Date.Add(DateTime.Now.TimeOfDay),
                            planta_id = menu.planta_id,
                            centrodecosto_id = menu.centrodecosto_id,
                            proyecto_id = menu.proyecto_id,
                            jerarquia_id = menu.jerarquia_id.Value,  // Ya validado que no es null arriba

                            // ✅ Monto calculado automáticamente según la regla de bonificación aplicable
                            // (o el costo completo si ninguna regla activa matchea este pedido)
                            monto = montoCalculado,
                            // Snapshot del costo de proveedor al momento del pedido
                            costo_proveedor = costoProveedor,
                            bonificado = bonificadoResultante,
                            invitado = dto.Invitado,
                            calificacion = dto.Calificacion,

                            estado = string.IsNullOrWhiteSpace(dto.Estado)
                                ? "P"
                                : (dto.Estado.Trim().Length > 20 ? dto.Estado.Trim().Substring(0, 20) : dto.Estado.Trim()),

                            comentario = !string.IsNullOrEmpty(dto.Comentario)
                                ? (dto.Comentario.Length > 200 ? dto.Comentario.Substring(0, 200) : dto.Comentario)
                                : dto.Comentario,

                            deletemark = false,
                        };

                        ctx.sl_comanda.Add(entity);

                        // Guardar
                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                        // Construir el DTO desde los datos ya cargados en memoria
                        var resultado = new ComandaDetalleDto
                        {
                            Id = entity.id,
                            Npedido = entity.npedido,
                            Fecha = entity.fecha,
                            Monto = entity.monto,
                            Bonificado = entity.bonificado,
                            Invitado = entity.invitado,
                            Calificacion = entity.calificacion,
                            Estado = entity.estado,
                            Comentario = entity.comentario,
                            UsuarioId = entity.usuario_id,
                            UsuarioNombre = $"{usuario.nombre} {usuario.apellido}",
                            PlatoId = entity.plato_id,
                            PlatoDescripcion = menu.Plato?.descripcion ?? "",
                            TurnoId = entity.turno_id,
                            TurnoNombre = menu.Turno?.nombre ?? "",
                            PlantaId = entity.planta_id,
                            PlantaDescripcion = menu.Planta?.nombre ?? "",
                            CentroDeCostoId = entity.centrodecosto_id,
                            CentroDeCostoDescripcion = menu.CentroDeCosto?.descripcion ?? "",
                            ProyectoId = entity.proyecto_id,
                            ProyectoDescripcion = menu.Proyecto?.nombre ?? "",
                            JerarquiaId = entity.jerarquia_id,
                            JerarquiaDescripcion = menu.Jerarquia?.nombre ?? "",
                            PlatoImporte = menu.Plato?.costo ?? 0,
                            CostoProveedor = entity.costo_proveedor,
                            Foto = menu.Plato?.foto,
                            NutricionalId = menu.Plato?.plannutricional_id ?? 0,
                            NutricionalNombre = menu.Plato?.plannutricional?.nombre ?? ""
                        };

                        // ===================== LOGGING: Comanda creada exitosamente =====================
                        _logger?.LogInformation("CrearConDescuento: Comanda creada exitosamente con descuento calculado", new
                        {
                            ComandaId = resultado.Id,
                            Npedido = resultado.Npedido,
                            UsuarioId = resultado.UsuarioId,
                            MenuddId = dto.MenuddId,
                            Estado = resultado.Estado,
                            MontoCalculado = resultado.Monto,
                            CostoPlato = costoPlato,
                            ReglaId = reglaAplicable?.id,
                            Bonificado = resultado.Bonificado
                        });

                        return resultado;
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        
                        // ===================== LOGGING: Deadlock detectado =====================
                        _logger?.LogWarning("CrearConDescuento: Deadlock detectado al crear comanda", ex, new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            ErrorNumber = ex.Number,
                            ErrorMessage = ex.Message
                        });
                        
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();

                        // ===================== LOGGING: Error de validación =====================
                        var validationException = HandleValidationException(ex, "crear con descuento");
                        _logger?.LogError("CrearConDescuento: Error de validación al guardar comanda", ex, new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            ValidationErrors = validationException.Message
                        });
                        
                        throw validationException;
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        
                        // ===================== LOGGING: Error general =====================
                        _logger?.LogError("CrearConDescuento: Error al crear comanda con descuento", ex, new
                        {
                            UsuarioId = usuarioId,
                            MenuddId = dto.MenuddId,
                            ExceptionType = ex.GetType().Name,
                            ExceptionMessage = ex.Message
                        });
                        
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Calcula, para cada ítem del menú del día de hoy, el precio que le correspondería
        /// al usuario si lo pidiera ahora mismo según las reglas de bonificación activas.
        /// No crea ninguna comanda ni reserva cupo — es de solo lectura, para que el front
        /// pueda mostrar el precio (tachado/final) antes de que el comensal confirme.
        /// La "posición del pedido" se calcula una sola vez (cuántos pedidos activos ya
        /// tiene hoy + 1) y se reutiliza para todos los ítems, porque no cambia según cuál
        /// elija: es su próximo pedido, sea cual sea el plato.
        /// </summary>
        public List<PreviewBonificacionDto> PrevisualizarBonificacion(int usuarioId)
        {
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var usuario = ctx.sl_usuario.FirstOrDefault(u => u.id == usuarioId && !u.deletemark);
                if (usuario == null)
                    throw new Exception("El usuario especificado no existe o está inactivo.");

                var hoy = DateTime.Now.Date;

                int pedidosActivosHoy = ctx.sl_comanda.Count(c =>
                    c.usuario_id == usuarioId &&
                    !c.deletemark &&
                    c.estado != "C" &&
                    DbFunctions.TruncateTime(c.fecha) == hoy);
                int posicionPedido = pedidosActivosHoy + 1;

                var menusHoy = ctx.sl_menudd
                    .Include(m => m.Plato)
                    .Where(m => !m.deletemark && DbFunctions.TruncateTime(m.fecha) == hoy)
                    .ToList();

                var resultado = new List<PreviewBonificacionDto>();
                foreach (var menu in menusHoy)
                {
                    decimal costoPlato = menu.Plato?.costo ?? 0;

                    var reglaAplicable = _servicioReglaBonificacion.ObtenerReglaAplicable(
                        ctx,
                        menu.turno_id,
                        usuario.jerarquia_id,
                        menu.planta_id,
                        menu.Plato?.plannutricional_id ?? 0,
                        menu.plato_id,
                        false, // la previsualización no sabe todavía si el pedido será "invitado"
                        posicionPedido,
                        menu.fecha);

                    decimal precioFinal = _servicioReglaBonificacion.AplicarEfecto(costoPlato, reglaAplicable);

                    resultado.Add(new PreviewBonificacionDto
                    {
                        MenuddId = menu.id,
                        CostoLista = costoPlato,
                        PrecioFinal = precioFinal,
                        Bonificado = precioFinal < costoPlato,
                        ReglaNombre = reglaAplicable?.nombre
                    });
                }

                return resultado;
            }
        }

        public void Actualizar(ComandaUpdateDto dto)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            // Validar rango de calificación si se proporciona
            if (dto.Calificacion.HasValue && (dto.Calificacion.Value < 1 || dto.Calificacion.Value > 5))
                throw new Exception("La calificación debe estar entre 1 y 5.");

            try
            {
                using (var ctx = new DataContext())
                {
                    var entity = ctx.sl_comanda.FirstOrDefault(c => c.id == dto.Id && !c.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("Actualizar: Comanda no encontrada", new { ComandaId = dto.Id });
                        throw new Exception("Comanda no encontrada.");
                    }

                    // Guardar valores anteriores para logging
                    var estadoAnterior = entity.estado;
                    var montoAnterior = entity.monto;

                    entity.monto = dto.Monto;
                    entity.bonificado = dto.Bonificado;
                    entity.invitado = dto.Invitado;
                    entity.calificacion = dto.Calificacion;
                    // Truncar campos según StringLength del modelo
                    entity.estado = !string.IsNullOrEmpty(dto.Estado) ? (dto.Estado.Length > 20 ? dto.Estado.Substring(0, 20) : dto.Estado) : dto.Estado;
                    entity.comentario = !string.IsNullOrEmpty(dto.Comentario) ? (dto.Comentario.Length > 200 ? dto.Comentario.Substring(0, 200) : dto.Comentario) : dto.Comentario;

                    // ===================== LOGGING: Inicio de actualización =====================
                    _logger?.LogInformation("Actualizar: Iniciando actualización de comanda", new
                    {
                        ComandaId = dto.Id,
                        Npedido = entity.npedido,
                        EstadoAnterior = estadoAnterior,
                        EstadoNuevo = entity.estado,
                        MontoAnterior = montoAnterior,
                        MontoNuevo = entity.monto
                    });

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Actualización exitosa =====================
                        _logger?.LogInformation("Actualizar: Comanda actualizada exitosamente", new
                        {
                            ComandaId = dto.Id,
                            Npedido = entity.npedido
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Actualizar: Error de validación al actualizar comanda", ex, new
                        {
                            ComandaId = dto.Id,
                            ValidationErrors = HandleValidationException(ex, "actualizar").Message
                        });
                        throw HandleValidationException(ex, "actualizar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Actualizar: Deadlock detectado", ex, new
                        {
                            ComandaId = dto.Id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Comanda no encontrada") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Actualizar: Error al actualizar comanda", ex, new { ComandaId = dto?.Id });
                throw;
            }
        }

        public void Eliminar(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    var entity = ctx.sl_comanda.FirstOrDefault(c => c.id == id && !c.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("Eliminar: Comanda no encontrada", new { ComandaId = id });
                        throw new Exception("Comanda no encontrada.");
                    }

                    // ===================== LOGGING: Inicio de eliminación =====================
                    _logger?.LogInformation("Eliminar: Iniciando eliminación lógica de comanda", new
                    {
                        ComandaId = id,
                        Npedido = entity.npedido,
                        Estado = entity.estado
                    });

                    entity.deletemark = true;
                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Eliminación exitosa =====================
                        _logger?.LogInformation("Eliminar: Comanda eliminada exitosamente", new
                        {
                            ComandaId = id,
                            Npedido = entity.npedido
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Eliminar: Error de validación al eliminar comanda", ex, new
                        {
                            ComandaId = id,
                            ValidationErrors = HandleValidationException(ex, "eliminar").Message
                        });
                        throw HandleValidationException(ex, "eliminar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Eliminar: Deadlock detectado", ex, new
                        {
                            ComandaId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Comanda no encontrada") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Eliminar: Error al eliminar comanda", ex, new { ComandaId = id });
                throw;
            }
        }

        public void Activar(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    var entity = ctx.sl_comanda.FirstOrDefault(c => c.id == id && c.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("Activar: Comanda no encontrada o ya está activa", new { ComandaId = id });
                        throw new Exception("Comanda no encontrada.");
                    }

                    // ===================== LOGGING: Inicio de activación =====================
                    _logger?.LogInformation("Activar: Iniciando activación de comanda", new
                    {
                        ComandaId = id,
                        Npedido = entity.npedido,
                        Estado = entity.estado
                    });

                    entity.deletemark = false;
                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Activación exitosa =====================
                        _logger?.LogInformation("Activar: Comanda activada exitosamente", new
                        {
                            ComandaId = id,
                            Npedido = entity.npedido
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Activar: Error de validación al activar comanda", ex, new
                        {
                            ComandaId = id,
                            ValidationErrors = HandleValidationException(ex, "activar").Message
                        });
                        throw HandleValidationException(ex, "activar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Activar: Deadlock detectado", ex, new
                        {
                            ComandaId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Comanda no encontrada") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Activar: Error al activar comanda", ex, new { ComandaId = id });
                throw;
            }
        }

        public void Cancelar(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El número de pedido debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var entity = ctx.sl_comanda.FirstOrDefault(c => c.npedido == id && !c.deletemark);
                if (entity == null)
                {
                    _logger?.LogWarning("Cancelar: Comanda no encontrada", new { Npedido = id });
                    throw new Exception("Comanda no encontrada.");
                }

                // Solo se puede cancelar si está pendiente
                if (entity.estado != "P" && entity.estado != "PENDIENTE")
                {
                    _logger?.LogWarning("Cancelar: Intento de cancelar comanda en estado inválido", new
                    {
                        Npedido = id,
                        EstadoActual = entity.estado
                    });
                    throw new Exception("Solo se pueden cancelar comandas pendientes.");
                }

                // ===================== LOGGING: Inicio de cancelación =====================
                _logger?.LogInformation("Cancelar: Iniciando cancelación de comanda", new
                {
                    ComandaId = entity.id,
                    Npedido = entity.npedido,
                    MenuddId = entity.menudd_id,
                    EstadoAnterior = entity.estado
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        entity.estado = "C"; // Cancelado

                        // ===================== DECREMENTAR CONTADOR DE FORMA ATÓMICA =====================
                        // Usar UPDATE atómico para prevenir race conditions
                        int rows = ctx.Database.ExecuteSqlCommand(@"
                            UPDATE sl_menudd
                            SET comandas = comandas - 1
                            WHERE id = @p0
                              AND deletemark = 0
                              AND comandas > 0
                        ", entity.menudd_id);

                        if (rows == 0)
                        {
                            _logger?.LogWarning("Cancelar: No se pudo decrementar contador (menú no encontrado o comandas = 0)", new
                            {
                                MenuddId = entity.menudd_id
                            });
                            // No lanzar excepción, solo registrar warning
                        }

                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== LOGGING: Cancelación exitosa =====================
                        _logger?.LogInformation("Cancelar: Comanda cancelada exitosamente", new
                        {
                            ComandaId = entity.id,
                            Npedido = entity.npedido,
                            MenuddId = entity.menudd_id
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Cancelar: Deadlock detectado", ex, new
                        {
                            Npedido = id,
                            ComandaId = entity.id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Cancelar: Error de validación al cancelar comanda", ex, new
                        {
                            Npedido = id,
                            ComandaId = entity.id,
                            ValidationErrors = HandleValidationException(ex, "cancelar").Message
                        });
                        throw HandleValidationException(ex, "cancelar");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Cancelar: Error al cancelar comanda", ex, new
                        {
                            Npedido = id,
                            ComandaId = entity.id,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        public ComandaDetalleDto Despachar(int npedido)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (npedido <= 0)
                throw new Exception("El número de pedido debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // Cargar entidad con relaciones necesarias para construir DTO
                var entity = ctx.sl_comanda
                    .Include(c => c.Usuario)
                    .Include(c => c.Plato)
                    .Include(c => c.Plato.plannutricional)
                    .Include(c => c.Turno)
                    .Include(c => c.Planta)
                    .Include(c => c.CentroDeCosto)
                    .Include(c => c.Proyecto)
                    .Include(c => c.Jerarquia)
                    .FirstOrDefault(c => c.npedido == npedido && !c.deletemark);

                if (entity == null)
                {
                    _logger?.LogWarning("Despachar: Comanda no encontrada", new { Npedido = npedido });
                    throw new Exception("Comanda no encontrada.");
                }

                // Solo se puede despachar si está pendiente (P) o pendiente temporal (PT)
                if (entity.estado != "P" && entity.estado != "PT")
                {
                    _logger?.LogWarning("Despachar: Intento de despachar comanda en estado inválido", new
                    {
                        Npedido = npedido,
                        EstadoActual = entity.estado
                    });
                    throw new Exception("Solo se pueden despachar comandas pendientes.");
                }

                // Guardar estado anterior para determinar el nuevo estado
                string estadoAnterior = entity.estado;

                // ===================== LOGGING: Inicio de despacho =====================
                _logger?.LogInformation("Despachar: Iniciando despacho de comanda", new
                {
                    ComandaId = entity.id,
                    Npedido = entity.npedido,
                    MenuddId = entity.menudd_id,
                    EstadoAnterior = estadoAnterior
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // Si el estado anterior era PT, cambiar a R (Recibido), sino cambiar a E (En Aceptación/Despachado)
                        entity.estado = estadoAnterior == "PT" ? "R" : "E";

                        // ===================== INCREMENTAR CONTADOR DE FORMA ATÓMICA =====================
                        // Usar UPDATE atómico para prevenir race conditions
                        int rows = ctx.Database.ExecuteSqlCommand(@"
                            UPDATE sl_menudd
                            SET despachado = ISNULL(despachado, 0) + 1
                            WHERE id = @p0
                              AND deletemark = 0
                        ", entity.menudd_id);

                        if (rows == 0)
                        {
                            _logger?.LogWarning("Despachar: No se pudo incrementar contador de despachados", new
                            {
                                MenuddId = entity.menudd_id
                            });
                            // No lanzar excepción, solo registrar warning
                        }

                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                        var resultado = ConstruirComandaDetalleDto(entity, entity.Usuario);

                        // ===================== LOGGING: Despacho exitoso =====================
                        _logger?.LogInformation("Despachar: Comanda despachada exitosamente", new
                        {
                            ComandaId = entity.id,
                            Npedido = entity.npedido,
                            MenuddId = entity.menudd_id,
                            EstadoNuevo = entity.estado
                        });

                        return resultado;
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Despachar: Deadlock detectado", ex, new
                        {
                            Npedido = npedido,
                            ComandaId = entity.id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Despachar: Error de validación al despachar comanda", ex, new
                        {
                            Npedido = npedido,
                            ComandaId = entity.id,
                            ValidationErrors = HandleValidationException(ex, "despachar").Message
                        });
                        throw HandleValidationException(ex, "despachar");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Despachar: Error al despachar comanda", ex, new
                        {
                            Npedido = npedido,
                            ComandaId = entity.id,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        public void Devolver(int npedido, int? calificacion = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (npedido <= 0)
                throw new Exception("El número de pedido debe ser mayor a 0.");

            // Validar rango de calificación si se proporciona
            if (calificacion.HasValue && (calificacion.Value < 1 || calificacion.Value > 5))
                throw new Exception("La calificación debe estar entre 1 y 5.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var entity = ctx.sl_comanda.FirstOrDefault(c => c.npedido == npedido && !c.deletemark);
                if (entity == null)
                {
                    _logger?.LogWarning("Devolver: Comanda no encontrada", new { Npedido = npedido });
                    throw new Exception("Comanda no encontrada.");
                }

                // Solo se puede devolver si está en estado E (En Aceptación), R (Recibido) o PT (Pendiente Temporal)
                if (entity.estado != "E" && entity.estado != "R" && entity.estado != "PT")
                {
                    _logger?.LogWarning("Devolver: Intento de devolver comanda en estado inválido", new
                    {
                        Npedido = npedido,
                        EstadoActual = entity.estado
                    });
                    throw new Exception("Solo se pueden devolver comandas en estado 'En Aceptación', 'Recibido' o 'Pendiente Temporal'.");
                }

                // ===================== LOGGING: Inicio de devolución =====================
                _logger?.LogInformation("Devolver: Iniciando devolución de comanda", new
                {
                    ComandaId = entity.id,
                    Npedido = entity.npedido,
                    MenuddId = entity.menudd_id,
                    EstadoAnterior = entity.estado,
                    Calificacion = calificacion
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        entity.estado = "D"; // Devuelto

                        // Guardar calificación si se proporciona
                        if (calificacion.HasValue)
                        {
                            entity.calificacion = calificacion.Value;
                        }

                        // ===================== DECREMENTAR CONTADOR DE FORMA ATÓMICA =====================
                        // Usar UPDATE atómico para prevenir race conditions
                        int rows = ctx.Database.ExecuteSqlCommand(@"
                            UPDATE sl_menudd
                            SET comandas = comandas - 1
                            WHERE id = @p0
                              AND deletemark = 0
                              AND comandas > 0
                        ", entity.menudd_id);

                        if (rows == 0)
                        {
                            _logger?.LogWarning("Devolver: No se pudo decrementar contador (menú no encontrado o comandas = 0)", new
                            {
                                MenuddId = entity.menudd_id
                            });
                            // No lanzar excepción, solo registrar warning
                        }

                        // Marcar explícitamente la entidad como modificada para asegurar que se guarde la calificación
                        ctx.Entry(entity).State = System.Data.Entity.EntityState.Modified;
                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== LOGGING: Devolución exitosa =====================
                        _logger?.LogInformation("Devolver: Comanda devuelta exitosamente", new
                        {
                            ComandaId = entity.id,
                            Npedido = entity.npedido,
                            MenuddId = entity.menudd_id,
                            EstadoNuevo = entity.estado,
                            Calificacion = entity.calificacion
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Devolver: Deadlock detectado", ex, new
                        {
                            Npedido = npedido,
                            ComandaId = entity.id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Devolver: Error de validación al devolver comanda", ex, new
                        {
                            Npedido = npedido,
                            ComandaId = entity.id,
                            ValidationErrors = HandleValidationException(ex, "devolver").Message
                        });
                        throw HandleValidationException(ex, "devolver");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Devolver: Error al devolver comanda", ex, new
                        {
                            Npedido = npedido,
                            ComandaId = entity.id,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        public ComandaDetalleDto Recibir(int npedido, int? calificacion = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (npedido <= 0)
                throw new Exception("El número de pedido debe ser mayor a 0.");

            // Validar rango de calificación si se proporciona
            if (calificacion.HasValue && (calificacion.Value < 1 || calificacion.Value > 5))
                throw new Exception("La calificación debe estar entre 1 y 5.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // Cargar entidad con relaciones necesarias para construir DTO
                var entity = ctx.sl_comanda
                    .Include(c => c.Usuario)
                    .Include(c => c.Plato)
                    .Include(c => c.Plato.plannutricional)
                    .Include(c => c.Turno)
                    .Include(c => c.Planta)
                    .Include(c => c.CentroDeCosto)
                    .Include(c => c.Proyecto)
                    .Include(c => c.Jerarquia)
                    .FirstOrDefault(c => c.npedido == npedido && !c.deletemark);

                if (entity == null)
                {
                    _logger?.LogWarning("Recibir: Comanda no encontrada", new { Npedido = npedido });
                    throw new Exception("Comanda no encontrada.");
                }

                // Solo se puede recibir si está en estado E (En Aceptación) o PT (Pendiente Temporal)
                if (entity.estado != "E" && entity.estado != "PT")
                {
                    _logger?.LogWarning("Recibir: Intento de recibir comanda en estado inválido", new
                    {
                        Npedido = npedido,
                        EstadoActual = entity.estado
                    });
                    throw new Exception("Solo se pueden recibir comandas en estado 'En Aceptación' o 'Pendiente Temporal'.");
                }

                // ===================== LOGGING: Inicio de recepción =====================
                _logger?.LogInformation("Recibir: Iniciando recepción de comanda", new
                {
                    ComandaId = entity.id,
                    Npedido = entity.npedido,
                    EstadoAnterior = entity.estado,
                    Calificacion = calificacion
                });

                try
                {
                    entity.estado = "R"; // Recibido

                    // Guardar calificación si se proporciona
                    if (calificacion.HasValue)
                    {
                        entity.calificacion = calificacion.Value;
                    }

                    // Marcar explícitamente la entidad como modificada para asegurar que se guarde la calificación
                    ctx.Entry(entity).State = System.Data.Entity.EntityState.Modified;
                    ctx.SaveChanges();

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = ConstruirComandaDetalleDto(entity, entity.Usuario);

                    // ===================== LOGGING: Recepción exitosa =====================
                    _logger?.LogInformation("Recibir: Comanda recibida exitosamente", new
                    {
                        ComandaId = entity.id,
                        Npedido = entity.npedido,
                        EstadoNuevo = entity.estado,
                        Calificacion = entity.calificacion
                    });

                    return resultado;
                }
                catch (DbEntityValidationException ex)
                {
                    _logger?.LogError("Recibir: Error de validación al recibir comanda", ex, new
                    {
                        Npedido = npedido,
                        ComandaId = entity.id,
                        ValidationErrors = HandleValidationException(ex, "recibir").Message
                    });
                    throw HandleValidationException(ex, "recibir");
                }
                catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                {
                    _logger?.LogWarning("Recibir: Deadlock detectado", ex, new
                    {
                        Npedido = npedido,
                        ComandaId = entity.id,
                        ErrorNumber = ex.Number
                    });
                    throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("Recibir: Error al recibir comanda", ex, new
                    {
                        Npedido = npedido,
                        ComandaId = entity.id,
                        ExceptionType = ex.GetType().Name
                    });
                    throw;
                }
            }
        }

        // Implementación de la interfaz: ObtenerXDia(DateTime fecha, int usuarioId)
        public List<ComandaDetalleDto> ObtenerXDia(DateTime fecha, int usuarioId)
        {
            // Usar el método sobrecargado con valores por defecto
            return ObtenerXDia(
                fechaDesde: fecha.Date,
                fechaHasta: fecha.Date.AddDays(1).AddTicks(-1), // Fin del día
                usuarioId: usuarioId,
                turnoId: 0, // Sin filtro
                plantaId: 0, // Sin filtro
                centroCostoId: 0, // Sin filtro
                proyectoId: 0, // Sin filtro
                jerarquiaId: 0, // Sin filtro
                estado: null // Sin filtro
            );
        }

        // Método sobrecargado con más parámetros (usado internamente por ServicioInicio)
        public List<ComandaDetalleDto> ObtenerXDia(
          DateTime? fechaDesde,
          DateTime? fechaHasta,
          int usuarioId,
          int turnoId,
          int plantaId,
          int centroCostoId,
          int proyectoId,
          int jerarquiaId,
          string estado)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            // Validar rango de fechas
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
                throw new Exception("La fecha desde no puede ser mayor que la fecha hasta.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de consulta =====================
                    _logger?.LogInformation("ObtenerXDia: Iniciando consulta de comandas por día", new
                    {
                        FechaDesde = fechaDesde,
                        FechaHasta = fechaHasta,
                        UsuarioId = usuarioId,
                        TurnoId = turnoId,
                        PlantaId = plantaId
                    });

                var query = ctx.sl_comanda
                    .Where(c => !c.deletemark)
                    .Include(c => c.Usuario)
                    .Include(c => c.Plato)
                    .Include("Plato.plannutricional")
                    .Include(c => c.Turno)
                    .Include(c => c.Planta)
                    .Include(c => c.CentroDeCosto)
                    .Include(c => c.Proyecto)
                    .Include(c => c.Jerarquia)
                    .AsQueryable();

                if (fechaDesde.HasValue)
                {
                    var desde = fechaDesde.Value.Date;
                    query = query.Where(c => c.fecha >= desde);
                }

                if (fechaHasta.HasValue)
                {
                    var hastaExclusivo = fechaHasta.Value.Date.AddDays(1);
                    query = query.Where(c => c.fecha < hastaExclusivo);
                }

                if (usuarioId > 0)
                    query = query.Where(c => c.usuario_id == usuarioId);

                /*if (turnoId > 0)
                    query = query.Where(c => c.turno_id == turnoId);*/

                if (plantaId > 0)
                    query = query.Where(c => c.planta_id == plantaId);

                if (centroCostoId > 0)
                    query = query.Where(c => c.centrodecosto_id == centroCostoId);

                if (proyectoId > 0)
                    query = query.Where(c => c.proyecto_id == proyectoId);

                if (jerarquiaId > 0)
                    query = query.Where(c => c.jerarquia_id == jerarquiaId);

                /*if (!string.IsNullOrWhiteSpace(estado))
                {
                    var e = estado.Trim().ToLower();
                    query = query.Where(c => (c.estado ?? "").ToLower() == e);
                }*/
                query = query.Where(c => c.estado == "P" || c.estado == "E");

                var items = query
                    .OrderByDescending(c => c.fecha)
                    .ThenByDescending(c => c.id)
                    .Select(c => new ComandaDetalleDto
                    {
                        Id = c.id,
                        Npedido = c.npedido,
                        Fecha = c.fecha,
                        Monto = c.monto,
                        Bonificado = c.bonificado,
                        Invitado = c.invitado,
                        Calificacion = c.calificacion,
                        Estado = c.estado,
                        Comentario = c.comentario,
                        UsuarioId = c.usuario_id,
                        UsuarioNombre = c.Usuario.nombre + " " + c.Usuario.apellido,
                        PlatoId = c.plato_id,
                        PlatoDescripcion = c.Plato.descripcion,
                        TurnoId = c.turno_id,
                        TurnoNombre = c.Turno.nombre,
                        PlantaId = c.planta_id,
                        PlantaDescripcion = c.Planta.nombre,
                        CentroDeCostoId = c.centrodecosto_id,
                        CentroDeCostoDescripcion = c.CentroDeCosto.descripcion,
                        ProyectoId = c.proyecto_id,
                        ProyectoDescripcion = c.Proyecto.nombre,
                        JerarquiaId = c.jerarquia_id,
                        JerarquiaDescripcion = c.Jerarquia.nombre,
                        PlatoImporte = c.Plato.costo,
                        CostoProveedor = c.costo_proveedor,
                        Foto = c.Plato.foto,
                        NutricionalId = c.Plato.plannutricional_id,
                        NutricionalNombre = c.Plato.plannutricional.nombre,
                    })
                    .ToList();

                    // ===================== LOGGING: Consulta exitosa =====================
                    _logger?.LogInformation("ObtenerXDia: Consulta completada exitosamente", new
                    {
                        UsuarioId = usuarioId,
                        ItemsReturned = items.Count
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerXDia: Error al obtener comandas por día", ex, new
                {
                    UsuarioId = usuarioId,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }

        // ================= IMPRESIÓN =================
        public List<ComandaImpresionDto> ObtenerDatosImpresion(ComandaImpresionRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "La solicitud de impresión no puede ser nula.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    // ===================== LOGGING: Inicio de impresión =====================
                    _logger?.LogInformation("ObtenerDatosImpresion: Iniciando obtención de datos para impresión", new
                    {
                        FechaDesde = request.FechaDesde,
                        FechaHasta = request.FechaHasta,
                        UsuarioId = request.UsuarioId,
                        Estado = request.Estado
                    });

                    var query = ctx.sl_comanda
                        .Include(c => c.Usuario)
                        .Include(c => c.Menudd)
                        .Include(c => c.Menudd.Plato)
                        .Include(c => c.Menudd.Turno)
                        .Include(c => c.Menudd.Planta)
                        .Include(c => c.Menudd.CentroDeCosto)
                        .Include(c => c.Menudd.Proyecto)
                        .Include(c => c.Menudd.Jerarquia)
                        .AsNoTracking()
                        .AsQueryable();

                    // Aplicar filtro de estado (deletemark)
                    if (request.Estado == "Activo")
                        query = query.Where(c => !c.deletemark);
                    else if (request.Estado == "Inactivo")
                        query = query.Where(c => c.deletemark);
                    // Si es un estado específico (pendiente, preparando, etc.), filtrar por estado
                    else if (!string.IsNullOrWhiteSpace(request.Estado) && request.Estado != "Todos")
                        query = query.Where(c => c.estado == request.Estado);

                    // Aplicar filtro de fechas (calcular fuera del Where para que EF traduzca a SQL)
                    DateTime? desde = request.FechaDesde.HasValue ? request.FechaDesde.Value.Date : (DateTime?)null;
                    DateTime? hastaExclusivo = request.FechaHasta.HasValue ? request.FechaHasta.Value.Date.AddDays(1) : (DateTime?)null;

                    if (desde.HasValue)
                        query = query.Where(c => c.fecha >= desde.Value);

                    if (hastaExclusivo.HasValue)
                        query = query.Where(c => c.fecha < hastaExclusivo.Value);

                    // Aplicar filtro de usuario
                    if (request.UsuarioId.HasValue && request.UsuarioId.Value > 0)
                        query = query.Where(c => c.usuario_id == request.UsuarioId.Value);

                    // Ordenar por fecha y npedido
                    var items = query
                        .OrderBy(c => c.fecha)
                        .ThenBy(c => c.npedido)
                        .Select(c => new ComandaImpresionDto
                        {
                            Npedido = request.IncluirNpedido ? (int?)c.npedido : null,
                            Fecha = request.IncluirFecha ? (DateTime?)c.fecha : null,
                            Usuario = request.IncluirUsuario ? c.Usuario.nombre : null,
                            Plato = request.IncluirPlato ? c.Menudd.Plato.descripcion : null,
                            Turno = request.IncluirTurno ? c.Menudd.Turno.nombre : null,
                            Planta = request.IncluirPlanta ? c.Menudd.Planta.nombre : null,
                            CentroCosto = request.IncluirCentroCosto ? c.Menudd.CentroDeCosto.nombre : null,
                            Proyecto = request.IncluirProyecto ? c.Menudd.Proyecto.nombre : null,
                            Jerarquia = request.IncluirJerarquia ? (c.Menudd.Jerarquia != null ? c.Menudd.Jerarquia.nombre : null) : null,
                            Monto = request.IncluirMonto ? (decimal?)c.monto : null,
                            Bonificado = request.IncluirBonificado ? (bool?)c.bonificado : null,
                            Invitado = request.IncluirInvitado ? (bool?)c.invitado : null,
                            Estado = request.IncluirEstado ? c.estado : null,
                            Calificacion = request.IncluirCalificacion ? c.calificacion : null
                        })
                        .ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    var totalItems = items.Count;
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = totalItems,
                        FechaDesde = request.FechaDesde,
                        FechaHasta = request.FechaHasta,
                        Estado = request.Estado
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex, new
                {
                    FechaDesde = request?.FechaDesde,
                    FechaHasta = request?.FechaHasta,
                    Estado = request?.Estado,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }
    }
}
