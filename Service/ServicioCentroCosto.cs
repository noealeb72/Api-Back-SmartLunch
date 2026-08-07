using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioCentroDeCosto
    {
        PagedResultDto<CentroDeCostoListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado);
        CentroDeCostoDetalleDto ObtenerPorId(int id);
        CentroDeCostoDetalleDto Crear(CentroDeCostoCreateDto dto, string username);
        void Actualizar(CentroDeCostoUpdateDto dto, string username);
        void Eliminar(int id, string username);
        void Activar(int id, string username);
        List<CentroDeCostoImpresionDto> ObtenerDatosImpresion(CentroDeCostoImpresionRequestDto request);
        CentroDeCostoValidacionDto ValidarCantidadUsuarios(int centroDeCostoId);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioCentroDeCosto : IServicioCentroDeCosto
    {
        private readonly ILoggerService _logger;

        public ServicioCentroDeCosto(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el centro de costo:");
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
        // ===================== LISTA PAGINADA =====================
        public PagedResultDto<CentroDeCostoListadoDto> ObtenerLista(
    int page,
    int pageSize,
    string search,
    bool activo)   // true = activos, false = inactivos (igual que Planta)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            // Validar longitud de búsqueda
            if (!string.IsNullOrWhiteSpace(search) && search.Length > 200)
                throw new Exception("El texto de búsqueda no puede exceder 200 caracteres.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de centros de costo", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        Activo = activo
                    });

                // Igual que en Planta: armamos el query directamente al DTO
                var query =
                    from c in ctx.sl_centrodecosto.Include(c => c.planta)
                    select new CentroDeCostoListadoDto
                    {
                        Id = c.id,
                        PlantaId = c.planta_id,
                        PlantaNombre = c.planta != null ? c.planta.nombre : null,
                        Nombre = c.nombre,
                        Descripcion = c.descripcion,
                        DeleteMark = !c.deletemark,
                        IsDefault = c.is_default
                    };

                // Filtro por activo/inactivo (misma idea que Planta)
                query = activo
                    ? query.Where(x => x.DeleteMark)   // estado = true → activos
                    : query.Where(x => !x.DeleteMark);   // estado = false → inactivos

                // Búsqueda opcional por nombre / descripción / planta
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(x =>
                        (x.Nombre ?? "").ToLower().Contains(s) ||
                        (x.Descripcion ?? "").ToLower().Contains(s) ||
                        (x.PlantaNombre ?? "").ToLower().Contains(s)
                    );
                }

                var totalItems = query.Count();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var items = query
                    .OrderBy(x => x.PlantaNombre)
                    .ThenBy(x => x.Nombre)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                    var resultado = new PagedResultDto<CentroDeCostoListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de centros de costo", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }


        // ===================== DETALLE =====================
        public CentroDeCostoDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var query = ctx.sl_centrodecosto
                        .Include(c => c.planta)
                        .Where(c => c.id == id && !c.deletemark) // Filtrar por deletemark
                        .Select(c => new CentroDeCostoDetalleDto
                        {
                            Id = c.id,
                            PlantaId = c.planta_id,
                            PlantaNombre = c.planta != null ? c.planta.nombre : null,
                            Nombre = c.nombre,
                            TheDescripcion = c.descripcion,
                            DeleteMark = c.deletemark,
                            IsDefault = c.is_default,
                            Createdate = c.createdate,
                            Createuser = c.createuser,
                            Updatedate = c.updatedate,
                            Updateuser = c.updateuser
                        })
                        .FirstOrDefault();

                    if (query == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Centro de costo no encontrado", new { CentroDeCostoId = id });
                        throw new Exception("Centro de costo no encontrado.");
                    }

                    // ===================== LOGGING: Acceso a centro de costo =====================
                    _logger?.LogInformation("ObtenerPorId: Centro de costo obtenido", new
                    {
                        CentroDeCostoId = id,
                        Nombre = query.Nombre,
                        PlantaId = query.PlantaId
                    });

                    return query;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message == "Centro de costo no encontrado."))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener centro de costo", ex, new { CentroDeCostoId = id });
                throw;
            }
        }

        // ===================== CREAR =====================
        public CentroDeCostoDetalleDto Crear(CentroDeCostoCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new Exception("Datos inválidos.");

            if (dto.PlantaId <= 0)
                throw new Exception("PlantaId obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("Nombre obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // Validar planta
                var planta = ctx.sl_planta.FirstOrDefault(p => p.id == dto.PlantaId && !p.deletemark);
                if (planta == null)
                {
                    _logger?.LogWarning("Crear: Planta no encontrada", new { PlantaId = dto.PlantaId });
                    throw new Exception("Planta no encontrada.");
                }

                // ===================== LOGGING: Inicio de creación =====================
                _logger?.LogInformation("Crear: Iniciando creación de centro de costo", new
                {
                    PlantaId = dto.PlantaId,
                    Nombre = dto.Nombre,
                    Username = username
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // ===================== VALIDAR NOMBRE ÚNICO DENTRO DE LA PLANTA (CORREGIDO) =====================
                        // BUG CORREGIDO: c.id == dto.PlantaId → c.planta_id == dto.PlantaId
                        var existe = ctx.sl_centrodecosto.Any(c =>
                            c.planta_id == dto.PlantaId &&  // ✅ CORREGIDO
                            c.nombre == dto.Nombre &&
                            !c.deletemark);

                        if (existe)
                        {
                            _logger?.LogWarning("Crear: Nombre duplicado en planta", new
                            {
                                PlantaId = dto.PlantaId,
                                Nombre = dto.Nombre
                            });
                            throw new Exception("Ya existe un centro de costo con ese nombre en la planta.");
                        }

                        // Truncar campos según StringLength del modelo
                        var nombreTruncado = dto.Nombre.Trim().Length > 150 ? dto.Nombre.Trim().Substring(0, 150) : dto.Nombre.Trim();
                        var descripcionTruncada = !string.IsNullOrEmpty(dto.Descripcion)
                            ? (dto.Descripcion.Length > 300 ? dto.Descripcion.Substring(0, 300) : dto.Descripcion)
                            : dto.Descripcion;

                        var entity = new sl_centrodecosto
                        {
                            planta_id = dto.PlantaId,
                            nombre = nombreTruncado,
                            descripcion = descripcionTruncada,
                            createdate = DateTime.Now,
                            createuser = username,
                            deletemark = false
                        };

                        ctx.sl_centrodecosto.Add(entity);
                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                        var resultado = new CentroDeCostoDetalleDto
                        {
                            Id = entity.id,
                            PlantaId = entity.planta_id,
                            PlantaNombre = planta.nombre,
                            Nombre = entity.nombre,
                            TheDescripcion = entity.descripcion,
                            DeleteMark = entity.deletemark,
                            IsDefault = entity.is_default,
                            Createdate = entity.createdate,
                            Createuser = entity.createuser,
                            Updatedate = entity.updatedate,
                            Updateuser = entity.updateuser
                        };

                        // ===================== LOGGING: Creación exitosa =====================
                        _logger?.LogInformation("Crear: Centro de costo creado exitosamente", new
                        {
                            CentroDeCostoId = resultado.Id,
                            PlantaId = resultado.PlantaId,
                            Nombre = resultado.Nombre
                        });

                        return resultado;
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Crear: Deadlock detectado al crear centro de costo", ex, new
                        {
                            PlantaId = dto.PlantaId,
                            Nombre = dto.Nombre,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Crear: Error de validación al guardar centro de costo", ex, new
                        {
                            PlantaId = dto.PlantaId,
                            Nombre = dto.Nombre,
                            ValidationErrors = HandleValidationException(ex, "crear").Message
                        });
                        throw HandleValidationException(ex, "crear");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Crear: Error al crear centro de costo", ex, new
                        {
                            PlantaId = dto.PlantaId,
                            Nombre = dto.Nombre,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        // ===================== ACTUALIZAR =====================
        public void Actualizar(CentroDeCostoUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            if (dto.PlantaId <= 0)
                throw new Exception("PlantaId obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("Nombre obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var entity = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == dto.Id && !c.deletemark);
                if (entity == null)
                {
                    _logger?.LogWarning("Actualizar: Centro de costo no encontrado", new { CentroDeCostoId = dto.Id });
                    throw new Exception("Centro de costo no encontrado.");
                }

                // Guardar valores anteriores para logging
                var nombreAnterior = entity.nombre;
                var plantaIdAnterior = entity.planta_id;

                // Validar planta
                var planta = ctx.sl_planta.FirstOrDefault(p => p.id == dto.PlantaId && !p.deletemark);
                if (planta == null)
                {
                    _logger?.LogWarning("Actualizar: Planta no encontrada", new { PlantaId = dto.PlantaId });
                    throw new Exception("Planta no encontrada.");
                }

                // ===================== LOGGING: Inicio de actualización =====================
                _logger?.LogInformation("Actualizar: Iniciando actualización de centro de costo", new
                {
                    CentroDeCostoId = dto.Id,
                    NombreAnterior = nombreAnterior,
                    NombreNuevo = dto.Nombre,
                    PlantaIdAnterior = plantaIdAnterior,
                    PlantaIdNuevo = dto.PlantaId
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // ===================== VERIFICAR NOMBRE ÚNICO DENTRO DE LA PLANTA =====================
                        var existe = ctx.sl_centrodecosto.Any(c =>
                            c.id != dto.Id &&
                            c.planta_id == dto.PlantaId &&
                            c.nombre == dto.Nombre &&
                            !c.deletemark);

                        if (existe)
                        {
                            _logger?.LogWarning("Actualizar: Nombre duplicado en planta", new
                            {
                                CentroDeCostoId = dto.Id,
                                PlantaId = dto.PlantaId,
                                Nombre = dto.Nombre
                            });
                            throw new Exception("Ya existe otro centro de costo con ese nombre en la planta.");
                        }

                        // Truncar campos según StringLength del modelo
                        entity.planta_id = dto.PlantaId;
                        entity.nombre = dto.Nombre.Trim().Length > 150 ? dto.Nombre.Trim().Substring(0, 150) : dto.Nombre.Trim();
                        entity.descripcion = !string.IsNullOrEmpty(dto.Descripcion)
                            ? (dto.Descripcion.Length > 300 ? dto.Descripcion.Substring(0, 300) : dto.Descripcion)
                            : dto.Descripcion;
                        entity.updatedate = DateTime.Now;
                        entity.updateuser = username;

                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== LOGGING: Actualización exitosa =====================
                        _logger?.LogInformation("Actualizar: Centro de costo actualizado exitosamente", new
                        {
                            CentroDeCostoId = dto.Id,
                            Nombre = entity.nombre
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Actualizar: Deadlock detectado", ex, new
                        {
                            CentroDeCostoId = dto.Id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Actualizar: Error de validación al actualizar centro de costo", ex, new
                        {
                            CentroDeCostoId = dto.Id,
                            ValidationErrors = HandleValidationException(ex, "actualizar").Message
                        });
                        throw HandleValidationException(ex, "actualizar");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Actualizar: Error al actualizar centro de costo", ex, new
                        {
                            CentroDeCostoId = dto.Id,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        // ===================== VALIDAR CANTIDAD USUARIOS =====================
        public CentroDeCostoValidacionDto ValidarCantidadUsuarios(int centroDeCostoId)
        {
            if (centroDeCostoId <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var centro = ctx.sl_centrodecosto
                        .Where(c => c.id == centroDeCostoId)
                        .Select(c => new { c.id, c.nombre })
                        .FirstOrDefault();

                    if (centro == null)
                        throw new Exception("Centro de costo no encontrado.");

                    var cantidadUsuarios = ctx.sl_usuario
                        .Count(u => u.centrodecosto_id == centroDeCostoId && !u.deletemark);

                    var puedeDarDeBaja = cantidadUsuarios == 0;
                    var mensaje = puedeDarDeBaja
                        ? "El centro de costo no tiene usuarios asociados. Puede darse de baja."
                        : string.Format("El centro de costo tiene {0} usuario(s) asociado(s). Debe reasignar o dar de baja a los usuarios antes de dar de baja el centro de costo.", cantidadUsuarios);

                    return new CentroDeCostoValidacionDto
                    {
                        CentroDeCostoId = centro.id,
                        CentroDeCostoNombre = centro.nombre,
                        CantidadUsuarios = cantidadUsuarios,
                        PuedeDarDeBaja = puedeDarDeBaja,
                        Mensaje = mensaje
                    };
                }
            }
            catch (Exception ex) when (!ex.Message.Contains("Centro de costo no encontrado") && !ex.Message.Contains("ID del centro"))
            {
                _logger?.LogError("ValidarCantidadUsuarios: Error al validar centro de costo", ex, new { CentroDeCostoId = centroDeCostoId });
                throw;
            }
        }

        // ===================== BAJA LÓGICA =====================
        public void Eliminar(int id, string username)
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
                    var entity = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == id && !c.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("Eliminar: Centro de costo no encontrado", new { CentroDeCostoId = id });
                        throw new Exception("Centro de costo no encontrado.");
                    }

                    // Validar que no tenga usuarios asociados antes de dar de baja
                    var cantidadUsuarios = ctx.sl_usuario.Count(u => u.centrodecosto_id == id && !u.deletemark);
                    if (cantidadUsuarios > 0)
                    {
                        _logger?.LogWarning("Eliminar: Centro de costo con usuarios asociados", new { CentroDeCostoId = id, CantidadUsuarios = cantidadUsuarios });
                        throw new Exception(string.Format("No se puede dar de baja el centro de costo. Tiene {0} usuario(s) asociado(s). Reasigne o dé de baja a los usuarios antes de continuar.", cantidadUsuarios));
                    }

                    // ===================== LOGGING: Inicio de eliminación =====================
                    _logger?.LogInformation("Eliminar: Iniciando eliminación lógica de centro de costo", new
                    {
                        CentroDeCostoId = id,
                        Nombre = entity.nombre,
                        PlantaId = entity.planta_id
                    });

                    entity.deletemark = true;
                    entity.updatedate = DateTime.Now;
                    entity.updateuser = username;

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Eliminación exitosa =====================
                        _logger?.LogInformation("Eliminar: Centro de costo eliminado exitosamente", new
                        {
                            CentroDeCostoId = id,
                            Nombre = entity.nombre
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Eliminar: Error de validación al eliminar centro de costo", ex, new
                        {
                            CentroDeCostoId = id,
                            ValidationErrors = HandleValidationException(ex, "eliminar").Message
                        });
                        throw HandleValidationException(ex, "eliminar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Eliminar: Deadlock detectado", ex, new
                        {
                            CentroDeCostoId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Centro de costo no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Eliminar: Error al eliminar centro de costo", ex, new { CentroDeCostoId = id });
                throw;
            }
        }

        // ===================== ACTIVAR =====================
        public void Activar(int id, string username)
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
                    var entity = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == id && c.deletemark);
                    if (entity == null)
                    {
                        _logger?.LogWarning("Activar: Centro de costo no encontrado o ya está activo", new { CentroDeCostoId = id });
                        throw new Exception("Centro de costo no encontrado.");
                    }

                    // ===================== LOGGING: Inicio de activación =====================
                    _logger?.LogInformation("Activar: Iniciando activación de centro de costo", new
                    {
                        CentroDeCostoId = id,
                        Nombre = entity.nombre,
                        PlantaId = entity.planta_id
                    });

                    entity.deletemark = false;
                    entity.updatedate = DateTime.Now;
                    entity.updateuser = username;

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Activación exitosa =====================
                        _logger?.LogInformation("Activar: Centro de costo activado exitosamente", new
                        {
                            CentroDeCostoId = id,
                            Nombre = entity.nombre
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Activar: Error de validación al activar centro de costo", ex, new
                        {
                            CentroDeCostoId = id,
                            ValidationErrors = HandleValidationException(ex, "activar").Message
                        });
                        throw HandleValidationException(ex, "activar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Activar: Deadlock detectado", ex, new
                        {
                            CentroDeCostoId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Centro de costo no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Activar: Error al activar centro de costo", ex, new { CentroDeCostoId = id });
                throw;
            }
        }

        // ================= IMPRESIÓN =================
        public List<CentroDeCostoImpresionDto> ObtenerDatosImpresion(CentroDeCostoImpresionRequestDto request)
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
                        Estado = request.Estado,
                        IncluirPlanta = request.IncluirPlanta,
                        IncluirNombre = request.IncluirNombre,
                        IncluirDescripcion = request.IncluirDescripcion,
                        IncluirEstado = request.IncluirEstado
                    });

                    var query = ctx.sl_centrodecosto
                        .Include(c => c.planta)
                        .AsNoTracking()
                        .AsQueryable();

                    // Aplicar filtro de estado
                    if (request.Estado == "Activo")
                        query = query.Where(c => !c.deletemark);
                    else if (request.Estado == "Inactivo")
                        query = query.Where(c => c.deletemark);
                    // Si es "Todos" o null, no se filtra (retorna todos)

                    // Ordenar por planta y nombre
                    var items = query
                        .OrderBy(c => c.planta.nombre)
                        .ThenBy(c => c.nombre)
                        .Select(c => new CentroDeCostoImpresionDto
                        {
                            Planta = request.IncluirPlanta ? c.planta.nombre : null,
                            Nombre = request.IncluirNombre ? c.nombre : null,
                            Descripcion = request.IncluirDescripcion ? c.descripcion : null,
                            Estado = request.IncluirEstado ? (c.deletemark ? "Inactivo" : "Activo") : null
                        })
                        .ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    var totalItems = items.Count;
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = totalItems,
                        Estado = request.Estado
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex, new
                {
                    Estado = request?.Estado,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }
    }
}
