using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioMenudd
    {
        PagedResultDto<MenuddListadoDto> ObtenerLista(int page, int pageSize, DateTime? fechaDesde, DateTime? fechaHasta, string search, bool soloActivos, int? plantaId = null);
        MenuddDetalleDto ObtenerPorId(int id);
        MenuddDetalleDto CrearMenu(MenuddCreateDto dto, string username);
        /// <summary>Crea varios menús del día; inserta uno por uno. Si uno es duplicado se omite y se siguen creando el resto.</summary>
        MenuddCrearMultiplesResponseDto CrearMenusMultiples(MenuddCrearMultiplesRequestDto request, string username);
        void ActualizarMenu(MenuddUpdateDto dto, string username);
        void EliminarMenu(int id, string username);
        void ActivarMenu(int id, string username);
        //List<MenuddPorTurnoDto> ObtenerMenuPorTurno(int turnoId, DateTime fecha, int plantaId);
        // Método sobrecargado con más parámetros (usado internamente por ServicioInicio)
        List<MenuddPorTurnoDto> ObtenerMenuPorTurno(DateTime fecha, int? plantaId, int turnoId, int? centroCostoId, int? proyectoId, int? jerarquiaId,int? nutricionalId, bool soloConStock, int? usuarioId = null, string estadoComanda = null);
        // Versión async del mismo método: usada por el polling de Inicio (cada 2s) para no bloquear un hilo de IIS por request.
        Task<List<MenuddPorTurnoDto>> ObtenerMenuPorTurnoAsync(DateTime fecha, int? plantaId, int turnoId, int? centroCostoId, int? proyectoId, int? jerarquiaId, int? nutricionalId, bool soloConStock, int? usuarioId = null, string estadoComanda = null);
        List<MenuddImpresionDto> ObtenerDatosImpresion(MenuddImpresionRequestDto request);
        List<MenuddComedorDisponibleDto> ObtenerComedoresDisponibles(DateTime fecha, int turnoId, int? centroCostoId, int? proyectoId, int? jerarquiaId, int? nutricionalId, bool soloConStock);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioMenudd : IServicioMenudd
    {
        private readonly ILoggerService _logger;

        public ServicioMenudd(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el menú del día:");
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
        // ================= LISTA PAGINADA =================
        public PagedResultDto<MenuddListadoDto> ObtenerLista(
            int page,
            int pageSize,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
           string search,
            bool soloActivos,
            int? plantaId = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            // Validar rango de fechas
            if (fechaDesde.HasValue && fechaHasta.HasValue && fechaDesde.Value > fechaHasta.Value)
                throw new Exception("La fecha desde no puede ser mayor que la fecha hasta.");

            // Validar longitud de búsqueda
            if (!string.IsNullOrWhiteSpace(search) && search.Length > 200)
                throw new Exception("El texto de búsqueda no puede exceder 200 caracteres.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de menús del día", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        FechaDesde = fechaDesde,
                        FechaHasta = fechaHasta,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        SoloActivos = soloActivos,
                        PlantaId = plantaId
                    });

                    var query = ctx.sl_menudd
                        .Include(m => m.Turno)
                        .Include(m => m.Plato)
                        .Include(m => m.Planta)
                        .Include(m => m.CentroDeCosto)
                        .Include(m => m.Proyecto)
                        .Include(m => m.Jerarquia)
                        .AsQueryable();

                    query = soloActivos
                        ? query.Where(x => !x.deletemark)
                        : query.Where(x => x.deletemark);

                    // Filtros
                    DateTime? desde = null;
                    if (fechaDesde.HasValue)
                    {
                        desde = fechaDesde.Value.Date; 
                    }

                    DateTime? hasta = null;
                    if (fechaHasta.HasValue)
                    {
                        hasta = fechaHasta.Value.Date; 
                    }
                    
                    if (desde.HasValue)
                        query = query.Where(m => m.fecha >= desde.Value);

                    if (hasta.HasValue)
                        query = query.Where(m => m.fecha <= hasta.Value);

                    if (plantaId.HasValue && plantaId.Value > 0)
                        query = query.Where(m => m.planta_id == plantaId.Value);

                    // Búsqueda por texto (turno, plato, planta, centro de costo, proyecto, jerarquía)
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var searchLower = search.Trim().ToLower();
                        query = query.Where(m =>
                            (m.Turno.nombre ?? "").ToLower().Contains(searchLower) ||
                            (m.Plato.descripcion ?? "").ToLower().Contains(searchLower) ||
                            (m.Plato.codigo ?? "").ToLower().Contains(searchLower) ||
                            (m.Planta.nombre ?? "").ToLower().Contains(searchLower) ||
                            (m.CentroDeCosto.nombre ?? "").ToLower().Contains(searchLower) ||
                            (m.Proyecto.nombre ?? "").ToLower().Contains(searchLower) ||
                            (m.Jerarquia != null && (m.Jerarquia.nombre ?? "").ToLower().Contains(searchLower))
                        );
                    }               

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderBy(x => x.fecha)
                        .ThenBy(x => x.turno_id)
                        .ThenBy(x => x.plato_id)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(x => new MenuddListadoDto
                        {
                            Id = x.id,
                            Fecha = x.fecha,
                            TurnoId = x.turno_id,
                            TurnoNombre = x.Turno.nombre,
                            PlatoId = x.plato_id,
                            PlatoNombre = x.Plato.descripcion,
                            Cantidad = x.cantidad,
                            Comandas = x.comandas,
                            Despachado = x.despachado,
                            PlantaId = x.planta_id,
                            PlantaNombre = x.Planta.nombre,
                            CentroCostoId = x.centrodecosto_id,
                            CentroCostoNombre = x.CentroDeCosto.nombre,
                            ProyectoId = x.proyecto_id,
                            ProyectoNombre = x.Proyecto.nombre,
                            JerarquiaId = x.jerarquia_id,
                            JerarquiaNombre = x.Jerarquia.nombre,
                            PlanNutricionalId = x.Plato.plannutricional_id,
                            PlanNutricionalNombre = x.Plato.plannutricional.nombre,
                            Activo = !x.deletemark
                        })
                        .ToList();

                    var resultado = new PagedResultDto<MenuddListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de menús del día", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }

        // ================= DETALLE =================
        public MenuddDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var dto = ctx.sl_menudd
                        .Include(m => m.Turno)
                        .Include(m => m.Plato)
                        .Include(m => m.Planta)
                        .Include(m => m.CentroDeCosto)
                        .Include(m => m.Proyecto)
                        .Include(m => m.Jerarquia)
                        .Where(m => m.id == id && !m.deletemark)
                        .Select(m => new MenuddDetalleDto
                        {
                        Id = m.id,
                        Fecha = m.fecha,
                        TurnoId = m.turno_id,
                        TurnoNombre = m.Turno.nombre,
                        PlatoId = m.plato_id,
                        PlatoNombre = m.Plato.descripcion,
                        Cantidad = m.cantidad,
                        Comandas = m.comandas,
                        Despachado = m.despachado,
                        PlantaId = m.planta_id,
                        PlantaNombre = m.Planta.nombre,
                        CentroCostoId = m.centrodecosto_id,
                        CentroCostoNombre = m.CentroDeCosto.nombre,
                        ProyectoId = m.proyecto_id,
                        ProyectoNombre = m.Proyecto.nombre,
                        JerarquiaId = m.jerarquia_id,
                        JerarquiaNombre = m.Jerarquia != null ? m.Jerarquia.nombre : null,
                        DeleteMark = m.deletemark,
                            PlanNutricionalId = m.Plato.plannutricional_id,
                            PlanNutricionalNombre = m.Plato.plannutricional.nombre,
                            //Createdate = m.createdate,
                           /* Createuser = m.createuser,
                            Updatedate = m.updatedate,
                            Updateuser = m.updateuser*/
                        })
                        .FirstOrDefault();

                    if (dto == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Menú del día no encontrado", new { MenuddId = id });
                        throw new Exception("Menú del día no encontrado.");
                    }

                    // ===================== LOGGING: Acceso a menú del día =====================
                    _logger?.LogInformation("ObtenerPorId: Menú del día obtenido", new
                    {
                        MenuddId = id,
                        Fecha = dto.Fecha,
                        PlatoId = dto.PlatoId
                    });

                    return dto;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message == "Menú del día no encontrado."))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener menú del día", ex, new { MenuddId = id });
                throw;
            }
        }

        // ================= CREAR =================
        public MenuddDetalleDto CrearMenu(MenuddCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new Exception("Datos inválidos.");

            if (dto.Cantidad <= 0)
                throw new Exception("La cantidad debe ser mayor a cero.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            // Validar rangos de Comandas y Despachado
            if (dto.Comandas.HasValue && dto.Comandas.Value > dto.Cantidad)
                throw new Exception("El número de comandas no puede ser mayor que la cantidad.");

            if (dto.Despachado.HasValue && dto.Despachado.Value > dto.Cantidad)
                throw new Exception("El número de despachados no puede ser mayor que la cantidad.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // ===================== LOGGING: Inicio de creación =====================
                _logger?.LogInformation("CrearMenu: Iniciando creación de menú del día", new
                {
                    Fecha = dto.Fecha,
                    TurnoId = dto.TurnoId,
                    PlatoId = dto.PlatoId,
                    Cantidad = dto.Cantidad,
                    Username = username
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // --- Validar existencia de maestros ---
                        ValidarMaestros(ctx, dto.TurnoId, dto.PlatoId, dto.PlantaId,
                                        dto.CentroCostoId, dto.ProyectoId, dto.JerarquiaId);

                        // --- Validar que no exista el mismo menú (misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía) ---
                        if (ExisteMenuddDuplicado(ctx, dto.Fecha, dto.TurnoId, dto.PlatoId, dto.PlantaId, dto.CentroCostoId, dto.ProyectoId, dto.JerarquiaId, null))
                            throw new Exception("Ya existe un menú del día con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía. No se puede guardar dos veces el mismo menú.");

                        // Cargar relaciones necesarias para construir DTO
                        var turno = ctx.sl_turno.FirstOrDefault(t => t.id == dto.TurnoId);
                        var plato = ctx.sl_plato
                            .Include(p => p.plannutricional)
                            .FirstOrDefault(p => p.id == dto.PlatoId);
                        var planta = ctx.sl_planta.FirstOrDefault(p => p.id == dto.PlantaId);
                        var centroCosto = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == dto.CentroCostoId);
                        var proyecto = ctx.sl_proyecto.FirstOrDefault(p => p.id == dto.ProyectoId);
                        var jerarquia = dto.JerarquiaId > 0
                            ? ctx.sl_jerarquia.FirstOrDefault(j => j.id == dto.JerarquiaId)
                            : null;

                        var entity = new sl_menudd
                        {
                            fecha = dto.Fecha.Date,
                            turno_id = dto.TurnoId,
                            plato_id = dto.PlatoId,
                            cantidad = dto.Cantidad,
                            comandas = dto.Comandas ?? 0,
                            despachado = dto.Despachado ?? 0,
                            planta_id = dto.PlantaId,
                            centrodecosto_id = dto.CentroCostoId,
                            proyecto_id = dto.ProyectoId,
                            jerarquia_id = dto.JerarquiaId,

                            deletemark = false,
                            createdate = DateTime.Now,
                            //createuser = username
                        };

                        ctx.sl_menudd.Add(entity);
                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                        var resultado = new MenuddDetalleDto
                        {
                            Id = entity.id,
                            Fecha = entity.fecha,
                            TurnoId = entity.turno_id,
                            TurnoNombre = turno?.nombre,
                            PlatoId = entity.plato_id,
                            PlatoNombre = plato?.descripcion,
                            Cantidad = entity.cantidad,
                            Comandas = entity.comandas,
                            Despachado = entity.despachado,
                            PlantaId = entity.planta_id,
                            PlantaNombre = planta?.nombre,
                            CentroCostoId = entity.centrodecosto_id,
                            CentroCostoNombre = centroCosto?.nombre,
                            ProyectoId = entity.proyecto_id,
                            ProyectoNombre = proyecto?.nombre,
                            JerarquiaId = entity.jerarquia_id,
                            JerarquiaNombre = jerarquia?.nombre,
                            DeleteMark = entity.deletemark,
                            PlanNutricionalId = plato?.plannutricional_id,
                            PlanNutricionalNombre = plato?.plannutricional?.nombre
                        };

                        // ===================== LOGGING: Creación exitosa =====================
                        _logger?.LogInformation("CrearMenu: Menú del día creado exitosamente", new
                        {
                            MenuddId = resultado.Id,
                            Fecha = resultado.Fecha,
                            PlatoId = resultado.PlatoId
                        });

                        return resultado;
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("CrearMenu: Deadlock detectado al crear menú del día", ex, new
                        {
                            Fecha = dto.Fecha,
                            PlatoId = dto.PlatoId,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // Violación de UNIQUE KEY o UNIQUE INDEX
                    {
                        tx.Rollback();
                        _logger?.LogWarning("CrearMenu: Intento de crear menú duplicado", ex, new
                        {
                            Fecha = dto.Fecha,
                            PlatoId = dto.PlatoId,
                            TurnoId = dto.TurnoId,
                            PlantaId = dto.PlantaId,
                            ErrorNumber = ex.Number,
                            ErrorMessage = ex.Message
                        });
                        throw new Exception("Ya existe un menú del día activo con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía. No se pueden crear menús duplicados.");
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
                    {
                        tx.Rollback();
                        _logger?.LogWarning("CrearMenu: Intento de crear menú duplicado (DbUpdateException)", ex, new
                        {
                            Fecha = dto.Fecha,
                            PlatoId = dto.PlatoId,
                            TurnoId = dto.TurnoId,
                            PlantaId = dto.PlantaId,
                            ErrorNumber = ((SqlException)ex.InnerException).Number
                        });
                        throw new Exception("Ya existe un menú del día activo con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía. No se pueden crear menús duplicados.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("CrearMenu: Error de validación al guardar menú del día", ex, new
                        {
                            Fecha = dto.Fecha,
                            PlatoId = dto.PlatoId,
                            ValidationErrors = HandleValidationException(ex, "crear").Message
                        });
                        throw HandleValidationException(ex, "crear");
                    }
                    catch (DbUpdateException ex)
                    {
                        tx.Rollback();
                        // Extraer el mensaje de la excepción interna en castellano
                        string mensajeError = "Error al guardar el menú del día.";
                        if (ex.InnerException != null)
                        {
                            if (ex.InnerException is SqlException sqlEx)
                            {
                                // Mensajes específicos según el tipo de error SQL
                                switch (sqlEx.Number)
                                {
                                    case 2627:
                                    case 2601:
                                        mensajeError = "Ya existe un menú del día activo con los mismos datos. No se pueden crear menús duplicados.";
                                        break;
                                    case 547: // Foreign key constraint
                                        mensajeError = "No se puede crear el menú. Verifique que todos los datos relacionados (turno, plato, planta, etc.) existan y estén activos.";
                                        break;
                                    default:
                                        mensajeError = $"Error de base de datos: {sqlEx.Message}";
                                        break;
                                }
                            }
                            else
                            {
                                // Si el mensaje es el genérico de Entity Framework, buscar en excepciones más profundas
                                string mensajeGenerico = "An error occurred while updating the entries. See the inner exception for details.";
                                if (ex.InnerException.Message.Contains(mensajeGenerico) || 
                                    ex.InnerException.Message.Contains("error occurred while updating"))
                                {
                                    // Buscar SqlException en excepciones más profundas
                                    Exception excepcionActual = ex.InnerException;
                                    while (excepcionActual != null)
                                    {
                                        if (excepcionActual is SqlException sqlExProfundo)
                                        {
                                            // Encontramos un SqlException más profundo
                                            switch (sqlExProfundo.Number)
                                            {
                                                case 2627:
                                                case 2601:
                                                    mensajeError = "Ya existe un menú del día activo con los mismos datos. No se pueden crear menús duplicados.";
                                                    break;
                                                case 547:
                                                    mensajeError = "No se puede crear el menú. Verifique que todos los datos relacionados (turno, plato, planta, etc.) existan y estén activos.";
                                                    break;
                                                default:
                                                    mensajeError = $"Error de base de datos: {sqlExProfundo.Message}";
                                                    break;
                                            }
                                            break;
                                        }
                                        excepcionActual = excepcionActual.InnerException;
                                    }
                                    
                                    // Si no encontramos un SqlException, usar un mensaje genérico en castellano
                                    if (mensajeError == "Error al guardar el menú del día.")
                                    {
                                        mensajeError = "Error al guardar el menú del día. Verifique los datos ingresados y que no exista un menú duplicado.";
                                    }
                                }
                                else
                                {
                                    mensajeError = ex.InnerException.Message;
                                }
                            }
                        }
                        _logger?.LogError("CrearMenu: Error al actualizar la base de datos", ex, new
                        {
                            Fecha = dto?.Fecha,
                            PlatoId = dto?.PlatoId,
                            TurnoId = dto?.TurnoId,
                            PlantaId = dto?.PlantaId,
                            ErrorMessage = mensajeError,
                            InnerExceptionType = ex.InnerException?.GetType().Name
                        });
                        throw new Exception(mensajeError);
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("CrearMenu: Error al crear menú del día", ex, new
                        {
                            Fecha = dto?.Fecha,
                            PlatoId = dto?.PlatoId,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        // ================= CREAR MÚLTIPLES (uno por uno; duplicados se omiten) =================
        public MenuddCrearMultiplesResponseDto CrearMenusMultiples(MenuddCrearMultiplesRequestDto request, string username)
        {
            if (request == null || request.Menus == null || request.Menus.Count == 0)
                throw new Exception("Debe enviar al menos un menú en el array 'menus'.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            var creados = new List<MenuddDetalleDto>();
            var noCreados = new List<MenuddItemNoCreadoDto>();

            for (int i = 0; i < request.Menus.Count; i++)
            {
                var item = request.Menus[i];
                var dto = new MenuddCreateDto
                {
                    Fecha = item.Fecha,
                    TurnoId = item.TurnoId,
                    PlatoId = item.PlatoId,
                    Cantidad = item.Cantidad,
                    Comandas = 0,
                    Despachado = 0,
                    PlantaId = item.PlantaId,
                    CentroCostoId = item.CentroCostoId,
                    ProyectoId = item.ProyectoId,
                    JerarquiaId = item.JerarquiaId
                };

                try
                {
                    var creado = CrearMenu(dto, username);
                    creados.Add(creado);
                }
                catch (Exception ex)
                {
                    // Si es duplicado (ya existe), omitir y seguir con los demás
                    bool esDuplicado = (ex.Message != null && (
                        ex.Message.IndexOf("duplicado", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        ex.Message.IndexOf("Ya existe", StringComparison.OrdinalIgnoreCase) >= 0));

                    if (esDuplicado)
                    {
                        noCreados.Add(new MenuddItemNoCreadoDto { Indice = i + 1, Motivo = ex.Message });
                        _logger?.LogWarning("CrearMenusMultiples: Ítem {Indice} omitido (duplicado): {Motivo}", i + 1, ex.Message);
                    }
                    else
                        throw;
                }
            }

            _logger?.LogInformation("CrearMenusMultiples: Creados {Creados}, omitidos {NoCreados}", creados.Count, noCreados.Count);

            // Mensaje para el front cuando hay duplicados
            string mensaje;
            if (noCreados.Count == 0)
                mensaje = creados.Count == 1
                    ? "Se guardó 1 menú del día correctamente."
                    : string.Format("Se guardaron {0} menús del día correctamente.", creados.Count);
            else if (creados.Count == 0)
                mensaje = noCreados.Count == 1
                    ? "No se creó el menú: ya existe uno con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía (duplicado)."
                    : string.Format("Ningún menú se creó: los {0} ítems ya existían (duplicados). Revise fecha, turno, plato, planta, centro de costo, proyecto y jerarquía.", noCreados.Count);
            else
                mensaje = string.Format("Se guardaron {0} menús del día. {1} no se crearon por estar duplicados (misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía).", creados.Count, noCreados.Count);

            return new MenuddCrearMultiplesResponseDto { Creados = creados, NoCreados = noCreados, Mensaje = mensaje };
        }

        // ================= ACTUALIZAR =================
        public void ActualizarMenu(MenuddUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            if (dto.Cantidad <= 0)
                throw new Exception("La cantidad debe ser mayor a cero.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            // CRÍTICO: No permitir actualizar Comandas y Despachado desde el DTO
            // Estos campos son calculados automáticamente por el sistema
            // Si se necesitan actualizar, debe hacerse mediante métodos específicos del servicio

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var entity = ctx.sl_menudd.FirstOrDefault(m => m.id == dto.Id && !m.deletemark);
                if (entity == null)
                {
                    _logger?.LogWarning("ActualizarMenu: Menú del día no encontrado", new { MenuddId = dto.Id });
                    throw new Exception("Menú del día no encontrado.");
                }

                // Guardar valores anteriores para logging
                var cantidadAnterior = entity.cantidad;
                var fechaAnterior = entity.fecha;

                // ===================== LOGGING: Inicio de actualización =====================
                _logger?.LogInformation("ActualizarMenu: Iniciando actualización de menú del día", new
                {
                    MenuddId = dto.Id,
                    CantidadAnterior = cantidadAnterior,
                    CantidadNueva = dto.Cantidad,
                    FechaAnterior = fechaAnterior,
                    FechaNueva = dto.Fecha,
                    Username = username
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // Validar maestros
                        ValidarMaestros(ctx, dto.TurnoId, dto.PlatoId, dto.PlantaId,
                                        dto.CentroCostoId, dto.ProyectoId, dto.JerarquiaId);

                        // Validar que no exista otro menú activo con la misma combinación (excluyendo el que se está actualizando)
                        if (ExisteMenuddDuplicado(ctx, dto.Fecha, dto.TurnoId, dto.PlatoId, dto.PlantaId, dto.CentroCostoId, dto.ProyectoId, dto.JerarquiaId, dto.Id))
                            throw new Exception("No se puede actualizar el menú. Ya existe un menú del día con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía. No se puede guardar dos veces el mismo menú.");

                        entity.fecha = dto.Fecha.Date;
                        entity.turno_id = dto.TurnoId;
                        entity.plato_id = dto.PlatoId;
                        entity.cantidad = dto.Cantidad;
                        // NO actualizar comandas y despachado - son campos calculados
                        // entity.comandas = dto.Comandas ?? 0;  // ❌ NO PERMITIDO
                        // entity.despachado = dto.Despachado ?? 0;  // ❌ NO PERMITIDO
                        entity.planta_id = dto.PlantaId;
                        entity.centrodecosto_id = dto.CentroCostoId;
                        entity.proyecto_id = dto.ProyectoId;
                        entity.jerarquia_id = dto.JerarquiaId;

                        /*entity.updatedate = DateTime.Now;
                        entity.updateuser = username;*/

                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== LOGGING: Actualización exitosa =====================
                        _logger?.LogInformation("ActualizarMenu: Menú del día actualizado exitosamente", new
                        {
                            MenuddId = dto.Id,
                            Cantidad = entity.cantidad
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("ActualizarMenu: Deadlock detectado", ex, new
                        {
                            MenuddId = dto.Id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (SqlException ex) when (ex.Number == 2627 || ex.Number == 2601) // Violación de UNIQUE KEY o UNIQUE INDEX
                    {
                        tx.Rollback();
                        _logger?.LogWarning("ActualizarMenu: Intento de actualizar a un menú duplicado", ex, new
                        {
                            MenuddId = dto.Id,
                            Fecha = dto.Fecha,
                            PlatoId = dto.PlatoId,
                            TurnoId = dto.TurnoId,
                            PlantaId = dto.PlantaId,
                            ErrorNumber = ex.Number,
                            ErrorMessage = ex.Message
                        });
                        throw new Exception("No se puede actualizar el menú. Ya existe otro menú del día activo con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía.");
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is SqlException sqlEx && (sqlEx.Number == 2627 || sqlEx.Number == 2601))
                    {
                        tx.Rollback();
                        _logger?.LogWarning("ActualizarMenu: Intento de actualizar a un menú duplicado (DbUpdateException)", ex, new
                        {
                            MenuddId = dto.Id,
                            Fecha = dto.Fecha,
                            PlatoId = dto.PlatoId,
                            TurnoId = dto.TurnoId,
                            PlantaId = dto.PlantaId,
                            ErrorNumber = ((SqlException)ex.InnerException).Number
                        });
                        throw new Exception("No se puede actualizar el menú. Ya existe otro menú del día activo con la misma fecha, turno, plato, planta, centro de costo, proyecto y jerarquía.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("ActualizarMenu: Error de validación al actualizar menú del día", ex, new
                        {
                            MenuddId = dto.Id,
                            ValidationErrors = HandleValidationException(ex, "actualizar").Message
                        });
                        throw HandleValidationException(ex, "actualizar");
                    }
                    catch (DbUpdateException ex)
                    {
                        tx.Rollback();
                        // Extraer el mensaje de la excepción interna en castellano
                        string mensajeError = "Error al actualizar el menú del día.";
                        if (ex.InnerException != null)
                        {
                            if (ex.InnerException is SqlException sqlEx)
                            {
                                // Mensajes específicos según el tipo de error SQL
                                switch (sqlEx.Number)
                                {
                                    case 2627:
                                    case 2601:
                                        mensajeError = "No se puede actualizar el menú. Ya existe otro menú del día activo con los mismos datos.";
                                        break;
                                    case 547: // Foreign key constraint
                                        mensajeError = "No se puede actualizar el menú. Verifique que todos los datos relacionados (turno, plato, planta, etc.) existan y estén activos.";
                                        break;
                                    default:
                                        mensajeError = $"Error de base de datos: {sqlEx.Message}";
                                        break;
                                }
                            }
                            else
                            {
                                // Si el mensaje es el genérico de Entity Framework, buscar en excepciones más profundas
                                string mensajeGenerico = "An error occurred while updating the entries. See the inner exception for details.";
                                if (ex.InnerException.Message.Contains(mensajeGenerico) || 
                                    ex.InnerException.Message.Contains("error occurred while updating"))
                                {
                                    // Buscar SqlException en excepciones más profundas
                                    Exception excepcionActual = ex.InnerException;
                                    while (excepcionActual != null)
                                    {
                                        if (excepcionActual is SqlException sqlExProfundo)
                                        {
                                            // Encontramos un SqlException más profundo
                                            switch (sqlExProfundo.Number)
                                            {
                                                case 2627:
                                                case 2601:
                                                    mensajeError = "Ya existe un menú del día activo con los mismos datos. No se pueden crear menús duplicados.";
                                                    break;
                                                case 547:
                                                    mensajeError = "No se puede crear el menú. Verifique que todos los datos relacionados (turno, plato, planta, etc.) existan y estén activos.";
                                                    break;
                                                default:
                                                    mensajeError = $"Error de base de datos: {sqlExProfundo.Message}";
                                                    break;
                                            }
                                            break;
                                        }
                                        excepcionActual = excepcionActual.InnerException;
                                    }
                                    
                                    // Si no encontramos un SqlException, usar un mensaje genérico en castellano
                                    if (mensajeError == "Error al actualizar el menú del día.")
                                    {
                                        mensajeError = "Error al actualizar el menú del día. Verifique los datos ingresados y que no exista un menú duplicado.";
                                    }
                                }
                                else
                                {
                                    mensajeError = ex.InnerException.Message;
                                }
                            }
                        }
                        _logger?.LogError("ActualizarMenu: Error al actualizar la base de datos", ex, new
                        {
                            MenuddId = dto.Id,
                            Fecha = dto?.Fecha,
                            PlatoId = dto?.PlatoId,
                            TurnoId = dto?.TurnoId,
                            PlantaId = dto?.PlantaId,
                            ErrorMessage = mensajeError,
                            InnerExceptionType = ex.InnerException?.GetType().Name
                        });
                        throw new Exception(mensajeError);
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("ActualizarMenu: Error al actualizar menú del día", ex, new
                        {
                            MenuddId = dto.Id,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        // ================= BAJA LÓGICA =================
        public void EliminarMenu(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    var entity = ctx.sl_menudd.FirstOrDefault(m => m.id == id && !m.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("EliminarMenu: Menú del día no encontrado", new { MenuddId = id });
                        throw new Exception("Menú del día no encontrado.");
                    }

                    // ===================== LOGGING: Inicio de eliminación =====================
                    _logger?.LogInformation("EliminarMenu: Iniciando eliminación lógica de menú del día", new
                    {
                        MenuddId = id,
                        Fecha = entity.fecha,
                        PlatoId = entity.plato_id
                    });

                    entity.deletemark = true;
                   /* entity.updatedate = DateTime.Now;
                    entity.updateuser = username;*/

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Eliminación exitosa =====================
                        _logger?.LogInformation("EliminarMenu: Menú del día eliminado exitosamente", new
                        {
                            MenuddId = id
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("EliminarMenu: Error de validación al eliminar menú del día", ex, new
                        {
                            MenuddId = id,
                            ValidationErrors = HandleValidationException(ex, "eliminar").Message
                        });
                        throw HandleValidationException(ex, "eliminar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("EliminarMenu: Deadlock detectado", ex, new
                        {
                            MenuddId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Menú del día no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("EliminarMenu: Error al eliminar menú del día", ex, new { MenuddId = id });
                throw;
            }
        }

        // ================= ACTIVAR (quitar baja) =================
        public void ActivarMenu(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    var entity = ctx.sl_menudd.FirstOrDefault(m => m.id == id && m.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("ActivarMenu: Menú del día no encontrado o ya está activo", new { MenuddId = id });
                        throw new Exception("Menú del día no encontrado.");
                    }

                    // ===================== LOGGING: Inicio de activación =====================
                    _logger?.LogInformation("ActivarMenu: Iniciando activación de menú del día", new
                    {
                        MenuddId = id,
                        Fecha = entity.fecha,
                        PlatoId = entity.plato_id
                    });

                    entity.deletemark = false;
                   /* entity.updatedate = DateTime.Now;
                    entity.updateuser = username;*/

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Activación exitosa =====================
                        _logger?.LogInformation("ActivarMenu: Menú del día activado exitosamente", new
                        {
                            MenuddId = id
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("ActivarMenu: Error de validación al activar menú del día", ex, new
                        {
                            MenuddId = id,
                            ValidationErrors = HandleValidationException(ex, "activar").Message
                        });
                        throw HandleValidationException(ex, "activar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("ActivarMenu: Deadlock detectado", ex, new
                        {
                            MenuddId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Menú del día no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("ActivarMenu: Error al activar menú del día", ex, new { MenuddId = id });
                throw;
            }
        }

        // Implementación de la interfaz: ObtenerMenuPorTurno(int turnoId, DateTime fecha, int plantaId)
        //(DateTime fecha, int plantaId, int turnoId, int? centroCostoId, int? proyectoId, int? jerarquiaId,int? nutricionalId, bool soloConStock)
        /*public List<MenuddPorTurnoDto> ObtenerMenuPorTurno(int turnoId, DateTime fecha, int plantaId)
        {
            // Usar el método sobrecargado con valores por defecto
            return ObtenerMenuPorTurno(
                fecha: fecha,
                plantaId: plantaId,
                turnoId: turnoId,
                centroCostoId: null,
                proyectoId: null,
                jerarquiaId: null,
                nutricionalId: null,
                soloConStock: false
            );
        }*/

        // Método sobrecargado con más parámetros (usado internamente por ServicioInicio)
        public List<MenuddPorTurnoDto> ObtenerMenuPorTurno(
            DateTime fecha,
            int? plantaId,
            int turnoId,
            int? centroCostoId,
            int? proyectoId,
            int? jerarquiaId,
            int? nutricionalId,
            bool soloConStock=true,
            int? usuarioId = null,
            string estadoComanda = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (plantaId <= 0)
                throw new Exception("El ID de planta debe ser mayor a 0.");

            if (turnoId <= 0)
                throw new Exception("El ID de turno debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var query = ctx.sl_menudd
                        .Include(m => m.Plato)
                        .Include(m => m.Turno)
                        .Include(m => m.Planta)
                        .Include(m => m.CentroDeCosto)
                        .Include(m => m.Proyecto)
                        .Include(m => m.Jerarquia)
                      
                        .Where(m =>
                            !m.deletemark &&
                            m.fecha == fecha.Date &&
                            //m.planta_id == plantaId &&
                            m.turno_id == turnoId);

                    if (plantaId.HasValue && plantaId.Value > 0)
                        query = query.Where(m => m.planta_id == plantaId);

                    if (centroCostoId.HasValue && centroCostoId.Value > 0)
                        query = query.Where(m => m.centrodecosto_id == centroCostoId.Value);

                    if (proyectoId.HasValue && proyectoId.Value > 0)
                        query = query.Where(m => m.proyecto_id == proyectoId.Value);

                    if (jerarquiaId.HasValue && jerarquiaId.Value > 0)
                        query = query.Where(m => m.jerarquia_id == jerarquiaId.Value);

                    if (nutricionalId.HasValue && nutricionalId.Value > 0)
                        query = query.Where(m => m.Plato.plannutricional_id == nutricionalId.Value);

                    if (soloConStock)
                        query = query.Where(m => (m.cantidad - m.comandas) > 0);

                    // Materializar la query primero con Includes (eliminado Include duplicado)
                    var menusList = query
                        .Include("Plato.plannutricional")
                        .ToList();

                // Obtener las comandas asociadas (solo para verificar si hay pedido reservado y obtener el estado)
                var menuddIds = menusList.Select(m => m.id).ToList();
                var comandas = ctx.sl_comanda
                    .Where(c => !c.deletemark
                             && menuddIds.Contains(c.menudd_id)
                             && (usuarioId == null || c.usuario_id == usuarioId.Value))
                    .GroupBy(c => c.menudd_id)
                    .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.id).FirstOrDefault());

                var items = menusList
                    .OrderBy(m => m.Plato.descripcion)
                    .Select(m =>
                    {
                        var comanda = comandas.ContainsKey(m.id) ? comandas[m.id] : null;
                        var planNutricionalMenu = m.Plato?.plannutricional;

                        return new MenuddPorTurnoDto
                        {
                            Id = m.id,
                            Fecha = m.fecha,
                            TurnoId = m.turno_id,
                            TurnoNombre = m.Turno.nombre,
                            PlatoId = m.plato_id,
                            PlatoNombre = m.Plato.descripcion,
                            Cantidad = (m.cantidad - m.comandas),
                            Despachado = m.despachado,
                            Disponible = m.cantidad - m.despachado,
                            PlantaId = m.planta_id,
                            PlantaNombre = m.Planta.nombre,
                            CentroCostoId = m.centrodecosto_id,
                            CentroCostoNombre = m.CentroDeCosto.nombre,
                            ProyectoId = m.proyecto_id,
                            ProyectoNombre = m.Proyecto.nombre,
                            JerarquiaId = m.jerarquia_id,
                            JerarquiaNombre = m.Jerarquia != null ? m.Jerarquia.nombre : null,
                            Foto = m.Plato.foto,
                            // Mostrar siempre el plan nutricional del plato del menú
                            NutricionalId = planNutricionalMenu != null ? planNutricionalMenu.id : (int?)null,
                            NutricionalNombre = planNutricionalMenu != null ? planNutricionalMenu.nombre : null,
                            Importe = m.Plato.costo,
                            Estado = comanda != null ? comanda.estado : null
                        };
                    })
                    .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("ObtenerMenuPorTurno: Búsqueda completada", new
                    {
                        Fecha = fecha,
                        PlantaId = plantaId,
                        TurnoId = turnoId,
                        Resultados = items.Count
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerMenuPorTurno: Error al obtener menú por turno", ex, new
                {
                    Fecha = fecha,
                    PlantaId = plantaId,
                    TurnoId = turnoId,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }

        // Versión async del método anterior: usada por el polling de Inicio (cada 2s) para no bloquear
        // un hilo de IIS por request mientras espera a SQL Server.
        public async Task<List<MenuddPorTurnoDto>> ObtenerMenuPorTurnoAsync(
            DateTime fecha,
            int? plantaId,
            int turnoId,
            int? centroCostoId,
            int? proyectoId,
            int? jerarquiaId,
            int? nutricionalId,
            bool soloConStock = true,
            int? usuarioId = null,
            string estadoComanda = null)
        {
            if (plantaId <= 0)
                throw new Exception("El ID de planta debe ser mayor a 0.");

            if (turnoId <= 0)
                throw new Exception("El ID de turno debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var query = ctx.sl_menudd
                        .Include(m => m.Plato)
                        .Include(m => m.Turno)
                        .Include(m => m.Planta)
                        .Include(m => m.CentroDeCosto)
                        .Include(m => m.Proyecto)
                        .Include(m => m.Jerarquia)
                        .Where(m =>
                            !m.deletemark &&
                            m.fecha == fecha.Date &&
                            m.turno_id == turnoId);

                    if (plantaId.HasValue && plantaId.Value > 0)
                        query = query.Where(m => m.planta_id == plantaId);

                    if (centroCostoId.HasValue && centroCostoId.Value > 0)
                        query = query.Where(m => m.centrodecosto_id == centroCostoId.Value);

                    if (proyectoId.HasValue && proyectoId.Value > 0)
                        query = query.Where(m => m.proyecto_id == proyectoId.Value);

                    if (jerarquiaId.HasValue && jerarquiaId.Value > 0)
                        query = query.Where(m => m.jerarquia_id == jerarquiaId.Value);

                    if (nutricionalId.HasValue && nutricionalId.Value > 0)
                        query = query.Where(m => m.Plato.plannutricional_id == nutricionalId.Value);

                    if (soloConStock)
                        query = query.Where(m => (m.cantidad - m.comandas) > 0);

                    var menusList = await query
                        .Include("Plato.plannutricional")
                        .ToListAsync();

                    var menuddIds = menusList.Select(m => m.id).ToList();
                    var comandasRaw = await ctx.sl_comanda
                        .Where(c => !c.deletemark
                                 && menuddIds.Contains(c.menudd_id)
                                 && (usuarioId == null || c.usuario_id == usuarioId.Value))
                        .ToListAsync();
                    var comandas = comandasRaw
                        .GroupBy(c => c.menudd_id)
                        .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.id).FirstOrDefault());

                    var items = menusList
                        .OrderBy(m => m.Plato.descripcion)
                        .Select(m =>
                        {
                            var comanda = comandas.ContainsKey(m.id) ? comandas[m.id] : null;
                            var planNutricionalMenu = m.Plato?.plannutricional;

                            return new MenuddPorTurnoDto
                            {
                                Id = m.id,
                                Fecha = m.fecha,
                                TurnoId = m.turno_id,
                                TurnoNombre = m.Turno.nombre,
                                PlatoId = m.plato_id,
                                PlatoNombre = m.Plato.descripcion,
                                Cantidad = (m.cantidad - m.comandas),
                                Despachado = m.despachado,
                                Disponible = m.cantidad - m.despachado,
                                PlantaId = m.planta_id,
                                PlantaNombre = m.Planta.nombre,
                                CentroCostoId = m.centrodecosto_id,
                                CentroCostoNombre = m.CentroDeCosto.nombre,
                                ProyectoId = m.proyecto_id,
                                ProyectoNombre = m.Proyecto.nombre,
                                JerarquiaId = m.jerarquia_id,
                                JerarquiaNombre = m.Jerarquia != null ? m.Jerarquia.nombre : null,
                                Foto = m.Plato.foto,
                                NutricionalId = planNutricionalMenu != null ? planNutricionalMenu.id : (int?)null,
                                NutricionalNombre = planNutricionalMenu != null ? planNutricionalMenu.nombre : null,
                                Importe = m.Plato.costo,
                                Estado = comanda != null ? comanda.estado : null
                            };
                        })
                        .ToList();

                    _logger?.LogInformation("ObtenerMenuPorTurnoAsync: Búsqueda completada", new
                    {
                        Fecha = fecha,
                        PlantaId = plantaId,
                        TurnoId = turnoId,
                        Resultados = items.Count
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerMenuPorTurnoAsync: Error al obtener menú por turno", ex, new
                {
                    Fecha = fecha,
                    PlantaId = plantaId,
                    TurnoId = turnoId,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }

        // Comedores que realmente tienen menú cargado para esa fecha/turno/jerarquía (no el catálogo completo de plantas)
        public List<MenuddComedorDisponibleDto> ObtenerComedoresDisponibles(
            DateTime fecha,
            int turnoId,
            int? centroCostoId,
            int? proyectoId,
            int? jerarquiaId,
            int? nutricionalId,
            bool soloConStock = true)
        {
            if (turnoId <= 0)
                throw new Exception("El ID de turno debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var query = ctx.sl_menudd
                    .Include(m => m.Plato)
                    .Include(m => m.Planta)
                    .Where(m => !m.deletemark && m.fecha == fecha.Date && m.turno_id == turnoId);

                if (centroCostoId.HasValue && centroCostoId.Value > 0)
                    query = query.Where(m => m.centrodecosto_id == centroCostoId.Value);

                if (proyectoId.HasValue && proyectoId.Value > 0)
                    query = query.Where(m => m.proyecto_id == proyectoId.Value);

                if (jerarquiaId.HasValue && jerarquiaId.Value > 0)
                    query = query.Where(m => m.jerarquia_id == jerarquiaId.Value);

                if (nutricionalId.HasValue && nutricionalId.Value > 0)
                    query = query.Where(m => m.Plato.plannutricional_id == nutricionalId.Value);

                if (soloConStock)
                    query = query.Where(m => (m.cantidad - m.comandas) > 0);

                return query
                    .Select(m => new { m.planta_id, PlantaNombre = m.Planta.nombre })
                    .Distinct()
                    .ToList()
                    .OrderBy(x => x.PlantaNombre)
                    .Select(x => new MenuddComedorDisponibleDto { PlantaId = x.planta_id, PlantaNombre = x.PlantaNombre })
                    .ToList();
            }
        }

        // ================= Helper validaciones =================
        private void ValidarMaestros(
            DataContext ctx,
            int turnoId,
            int platoId,
            int plantaId,
            int centroCostoId,
            int proyectoId,
            int? jerarquiaId)
        {
            if (!ctx.sl_turno.Any(t => t.id == turnoId && !t.deletemark))
                throw new Exception("El turno seleccionado no existe.");

            if (!ctx.sl_plato.Any(p => p.id == platoId && !p.deletemark))
                throw new Exception("El plato seleccionado no existe.");

            if (!ctx.sl_planta.Any(p => p.id == plantaId && !p.deletemark))
                throw new Exception("La planta seleccionada no existe.");

            if (!ctx.sl_centrodecosto.Any(c => c.id == centroCostoId && !c.deletemark))
                throw new Exception("El centro de costo seleccionado no existe.");

            if (!ctx.sl_proyecto.Any(p => p.id == proyectoId && !p.deletemark))
                throw new Exception("El proyecto seleccionado no existe.");

            if (jerarquiaId.HasValue && jerarquiaId.Value > 0)
            {
                var existeJerarquia = ctx.sl_jerarquia
                    .Any(j => j.id == jerarquiaId.Value && !j.deletemark);

                if (!existeJerarquia)
                    throw new Exception("La jerarquía seleccionada no existe.");
            }
        }

        /// <summary>Comprueba si ya existe un menú del día activo con la misma combinación: fecha, turno, plato, planta, centro de costo, proyecto y jerarquía. No puede guardarse dos veces el mismo menú para el mismo turno.</summary>
        /// <param name="excluirMenuddId">Si se indica, se excluye este id (para validar en actualización).</param>
        private bool ExisteMenuddDuplicado(
            DataContext ctx,
            DateTime fecha,
            int turnoId,
            int platoId,
            int plantaId,
            int centroCostoId,
            int proyectoId,
            int? jerarquiaId,
            int? excluirMenuddId = null)
        {
            var fechaDate = fecha.Date;
            // Regla: duplicado = misma fecha + turno + plato + planta + centro de costo + proyecto + jerarquía
            var query = ctx.sl_menudd
                .Where(m => !m.deletemark
                    && m.fecha == fechaDate
                    && m.turno_id == turnoId
                    && m.plato_id == platoId
                    && m.planta_id == plantaId
                    && m.centrodecosto_id == centroCostoId
                    && m.proyecto_id == proyectoId
                    && m.jerarquia_id == jerarquiaId);

            if (excluirMenuddId.HasValue && excluirMenuddId.Value > 0)
                query = query.Where(m => m.id != excluirMenuddId.Value);

            return query.Any();
        }

        // ================= IMPRESIÓN =================
        public List<MenuddImpresionDto> ObtenerDatosImpresion(MenuddImpresionRequestDto request)
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
                        Fecha = request.Fecha,
                        Estado = request.Estado,
                        TurnoId = request.TurnoId,
                        PlantaId = request.PlantaId
                    });

                    var query = ctx.sl_menudd
                        .AsNoTracking()
                        .AsQueryable();

                // Aplicar filtro de estado
                if (request.Estado == "Activo")
                    query = query.Where(m => !m.deletemark);
                else if (request.Estado == "Inactivo")
                    query = query.Where(m => m.deletemark);
                // Si es "Todos" o null, no se filtra

                // Normalizar fecha en C# antes de usar en LINQ
                DateTime? fechaFiltro = null;
                if (request.Fecha.HasValue)
                {
                    fechaFiltro = request.Fecha.Value.Date;
                }

                // Aplicar filtro de fecha
                if (fechaFiltro.HasValue)
                    query = query.Where(m => m.fecha == fechaFiltro.Value);

                // Aplicar filtros por IDs (solo los que no requieren JOIN)
                if (request.TurnoId.HasValue && request.TurnoId.Value > 0)
                    query = query.Where(m => m.turno_id == request.TurnoId.Value);

                if (request.JerarquiaId.HasValue && request.JerarquiaId.Value > 0)
                    query = query.Where(m => m.jerarquia_id == request.JerarquiaId.Value);

                if (request.ProyectoId.HasValue && request.ProyectoId.Value > 0)
                    query = query.Where(m => m.proyecto_id == request.ProyectoId.Value);

                if (request.CentroCostoId.HasValue && request.CentroCostoId.Value > 0)
                    query = query.Where(m => m.centrodecosto_id == request.CentroCostoId.Value);

                if (request.PlantaId.HasValue && request.PlantaId.Value > 0)
                    query = query.Where(m => m.planta_id == request.PlantaId.Value);

                // JOINs con todas las tablas relacionadas
                var queryConJoins = (from m in query
                                     join t in ctx.sl_turno on m.turno_id equals t.id
                                     join p in ctx.sl_plato on m.plato_id equals p.id
                                     join pn in ctx.sl_plannutricional on p.plannutricional_id equals pn.id into pnJoin
                                     from pn in pnJoin.DefaultIfEmpty()
                                     join pla in ctx.sl_planta on m.planta_id equals pla.id
                                     join cc in ctx.sl_centrodecosto on m.centrodecosto_id equals cc.id
                                     join proy in ctx.sl_proyecto on m.proyecto_id equals proy.id
                                     join j in ctx.sl_jerarquia on m.jerarquia_id equals j.id into jJoin
                                     from j in jJoin.DefaultIfEmpty()
                                     where !request.PlanNutricionalId.HasValue || request.PlanNutricionalId.Value <= 0 || p.plannutricional_id == request.PlanNutricionalId.Value
                                     orderby m.fecha, m.turno_id, m.plato_id
                                     select new
                                     {
                                         m,
                                         t,
                                         p,
                                         pn,
                                         pla,
                                         cc,
                                         proy,
                                         j
                                     })
                                     .ToList();

                // Construir DTOs solo con las columnas seleccionadas
                var result = queryConJoins.Select(x => new MenuddImpresionDto
                {
                    Plato = request.IncluirPlato ? x.p.descripcion : null,
                    Turno = request.IncluirTurno ? x.t.nombre : null,
                    Proyecto = request.IncluirProyecto ? x.proy.nombre : null,
                    Planta = request.IncluirPlanta ? x.pla.nombre : null,
                    Fecha = request.IncluirFecha ? (DateTime?)x.m.fecha : null,
                    PlanNutricional = request.IncluirPlanNutricional ? (x.pn != null ? x.pn.nombre : null) : null,
                    Jerarquia = request.IncluirJerarquia ? (x.j != null ? x.j.nombre : null) : null,
                    CentroCosto = request.IncluirCentroCosto ? x.cc.nombre : null,
                    Cantidad = request.IncluirCantidad ? (int?)x.m.cantidad : null,
                    Estado = request.IncluirEstado ? (x.m.deletemark ? "Inactivo" : "Activo") : null
                }).ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = result.Count,
                        Fecha = request.Fecha,
                        Estado = request.Estado
                    });

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex, new
                {
                    Fecha = request?.Fecha,
                    Estado = request?.Estado,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }
    }
}
