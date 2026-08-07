using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Text;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioPlanNutricional
    {
        PagedResultDto<PlanNutricionalListadoDto> ObtenerLista(int page, int pageSize, string search, bool activo);
        PlanNutricionalDetalleDto ObtenerPorId(int id);
        PlanNutricionalDetalleDto Crear(PlanNutricionalCreateDto dto, string username);
        void Actualizar(PlanNutricionalUpdateDto dto, string username);
        void Eliminar(int id, string username);
        void Activar(int id, string username);
        List<PlanNutricionalImpresionDto> ObtenerDatosImpresion(PlanNutricionalImpresionRequestDto request);
        PlanNutricionalValidacionDto ValidarCantidadUsuarios(int planNutricionalId);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioPlanNutricional : IServicioPlanNutricional
    {
        private readonly ILoggerService _logger;

        public ServicioPlanNutricional(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el plan nutricional:");
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
                    else if (msg.Contains("not a valid"))
                        msg = $"El valor del campo \"{ve.PropertyName}\" no es válido.";
                    else
                        msg = $"{ve.PropertyName}: {msg}";

                    sb.AppendLine("    • " + msg);
                }
            }
            return new Exception(sb.ToString(), ex);
        }
        // ============ LISTA PAGINADA ============
        public PagedResultDto<PlanNutricionalListadoDto> ObtenerLista(
            int page,
            int pageSize,
            string search,
            bool activo)
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
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de planes nutricionales", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        Activo = activo
                    });

                    var query =
                      ctx.sl_plannutricional
                          .Select(t => new PlanNutricionalListadoDto
                          {
                              Id = t.id,
                              Nombre = t.nombre,
                              Descripcion = t.descripcion,
                              Estado = !t.deletemark,
                              IsDefault = t.is_default
                          });

                    // Filtro activo / inactivo
                    query = activo
                        ? query.Where(x => x.Estado)
                        : query.Where(x => !x.Estado);

                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(x =>
                            (x.Nombre ?? "").ToLower().Contains(s) ||
                            (x.Descripcion ?? "").ToLower().Contains(s));
                    }

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderBy(x => x.Nombre)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    var resultado = new PagedResultDto<PlanNutricionalListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de planes nutricionales", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }

        // ============ DETALLE ============
        public PlanNutricionalDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var dto =
                        ctx.sl_plannutricional
                            .Where(p => p.id == id && !p.deletemark)
                            .Select(p => new PlanNutricionalDetalleDto
                            {
                                Id = p.id,
                                Nombre = p.nombre,
                                Descripcion = p.descripcion,
                                DeleteMark = p.deletemark,
                                IsDefault = p.is_default,
                                Createdate = p.createdate,
                                Createuser = p.createuser,
                                Updatedate = p.updatedate,
                                Updateuser = p.updateuser
                            })
                            .FirstOrDefault();

                    if (dto == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Plan nutricional no encontrado", new { PlanNutricionalId = id });
                        throw new Exception("Plan nutricional no encontrado.");
                    }

                    // ===================== LOGGING: Acceso a plan nutricional =====================
                    _logger?.LogInformation("ObtenerPorId: Plan nutricional obtenido", new
                    {
                        PlanNutricionalId = id,
                        Nombre = dto.Nombre
                    });

                    return dto;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message == "Plan nutricional no encontrado."))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener plan nutricional", ex, new { PlanNutricionalId = id });
                throw;
            }
        }

        // ============ CREAR ============
        public PlanNutricionalDetalleDto Crear(PlanNutricionalCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new Exception("Datos inválidos.");

            var nombre = dto.Nombre?.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                throw new Exception("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                // ===================== LOGGING: Inicio de creación =====================
                _logger?.LogInformation("Crear: Iniciando creación de plan nutricional", new
                {
                    Nombre = nombre,
                    Username = username
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // ===================== VALIDAR NOMBRE ÚNICO (dentro de transacción) =====================
                        var existe = ctx.sl_plannutricional
                            .Any(p => !p.deletemark && p.nombre == nombre);

                        if (existe)
                        {
                            _logger?.LogWarning("Crear: Nombre duplicado", new { Nombre = nombre });
                            throw new Exception("Ya existe un plan nutricional con ese nombre.");
                        }

                        // Truncar campos según StringLength del modelo (con Trim)
                        var nombreTruncado = nombre.Trim().Length > 50 ? nombre.Trim().Substring(0, 50) : nombre.Trim();
                        var descripcionTruncada = !string.IsNullOrEmpty(dto.Descripcion)
                            ? (dto.Descripcion.Trim().Length > 150 ? dto.Descripcion.Trim().Substring(0, 150) : dto.Descripcion.Trim())
                            : dto.Descripcion?.Trim();

                        var entity = new sl_plannutricional
                        {
                            nombre = nombreTruncado,
                            descripcion = descripcionTruncada,
                            deletemark = false,
                            createdate = DateTime.Now,
                            createuser = username
                        };

                        ctx.sl_plannutricional.Add(entity);
                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                        var resultado = new PlanNutricionalDetalleDto
                        {
                            Id = entity.id,
                            Nombre = entity.nombre,
                            Descripcion = entity.descripcion,
                            DeleteMark = entity.deletemark,
                            IsDefault = entity.is_default,
                            Createdate = entity.createdate,
                            Createuser = entity.createuser,
                            Updatedate = entity.updatedate,
                            Updateuser = entity.updateuser
                        };

                        // ===================== LOGGING: Creación exitosa =====================
                        _logger?.LogInformation("Crear: Plan nutricional creado exitosamente", new
                        {
                            PlanNutricionalId = resultado.Id,
                            Nombre = resultado.Nombre
                        });

                        return resultado;
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Crear: Deadlock detectado al crear plan nutricional", ex, new
                        {
                            Nombre = nombre,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Crear: Error de validación al guardar plan nutricional", ex, new
                        {
                            Nombre = nombre,
                            ValidationErrors = HandleValidationException(ex, "crear").Message
                        });
                        throw HandleValidationException(ex, "crear");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Crear: Error al crear plan nutricional", ex, new
                        {
                            Nombre = nombre,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        // ============ ACTUALIZAR ============
        public void Actualizar(PlanNutricionalUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            var nombre = dto.Nombre?.Trim();
            if (string.IsNullOrWhiteSpace(nombre))
                throw new Exception("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                ctx.Configuration.ProxyCreationEnabled = false;

                var entity = ctx.sl_plannutricional
                    .FirstOrDefault(p => p.id == dto.Id && !p.deletemark);

                if (entity == null)
                {
                    _logger?.LogWarning("Actualizar: Plan nutricional no encontrado", new { PlanNutricionalId = dto.Id });
                    throw new Exception("Plan nutricional no encontrado.");
                }

                // Guardar valores anteriores para logging
                var nombreAnterior = entity.nombre;

                // ===================== LOGGING: Inicio de actualización =====================
                _logger?.LogInformation("Actualizar: Iniciando actualización de plan nutricional", new
                {
                    PlanNutricionalId = dto.Id,
                    NombreAnterior = nombreAnterior,
                    NombreNuevo = nombre,
                    Username = username
                });

                using (var tx = ctx.Database.BeginTransaction(System.Data.IsolationLevel.Serializable))
                {
                    try
                    {
                        // ===================== VERIFICAR NOMBRE ÚNICO (dentro de transacción) =====================
                        var existeDuplicado = ctx.sl_plannutricional.Any(p =>
                            p.id != dto.Id &&
                            !p.deletemark &&
                            p.nombre == nombre);

                        if (existeDuplicado)
                        {
                            _logger?.LogWarning("Actualizar: Nombre duplicado", new
                            {
                                PlanNutricionalId = dto.Id,
                                Nombre = nombre
                            });
                            throw new Exception("Ya existe otro plan nutricional con ese nombre.");
                        }

                        // Truncar campos según StringLength del modelo (con Trim)
                        entity.nombre = nombre.Trim().Length > 50 ? nombre.Trim().Substring(0, 50) : nombre.Trim();
                        entity.descripcion = !string.IsNullOrEmpty(dto.Descripcion) 
                            ? (dto.Descripcion.Trim().Length > 150 ? dto.Descripcion.Trim().Substring(0, 150) : dto.Descripcion.Trim())
                            : dto.Descripcion?.Trim();
                        entity.updatedate = DateTime.Now;
                        entity.updateuser = username;

                        ctx.SaveChanges();
                        tx.Commit();

                        // ===================== LOGGING: Actualización exitosa =====================
                        _logger?.LogInformation("Actualizar: Plan nutricional actualizado exitosamente", new
                        {
                            PlanNutricionalId = dto.Id,
                            Nombre = entity.nombre
                        });
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        tx.Rollback();
                        _logger?.LogWarning("Actualizar: Deadlock detectado", ex, new
                        {
                            PlanNutricionalId = dto.Id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                    catch (DbEntityValidationException ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Actualizar: Error de validación al actualizar plan nutricional", ex, new
                        {
                            PlanNutricionalId = dto.Id,
                            ValidationErrors = HandleValidationException(ex, "actualizar").Message
                        });
                        throw HandleValidationException(ex, "actualizar");
                    }
                    catch (Exception ex)
                    {
                        tx.Rollback();
                        _logger?.LogError("Actualizar: Error al actualizar plan nutricional", ex, new
                        {
                            PlanNutricionalId = dto.Id,
                            ExceptionType = ex.GetType().Name
                        });
                        throw;
                    }
                }
            }
        }

        // ============ Validar cantidad de usuarios ============
        public PlanNutricionalValidacionDto ValidarCantidadUsuarios(int planNutricionalId)
        {
            if (planNutricionalId <= 0)
                throw new Exception("El ID del plan nutricional debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var plan = ctx.sl_plannutricional
                        .Where(p => p.id == planNutricionalId)
                        .Select(p => new { p.id, p.nombre })
                        .FirstOrDefault();

                    if (plan == null)
                        throw new Exception("Plan nutricional no encontrado.");

                    var cantidadUsuarios = ctx.sl_usuario
                        .Count(u => u.plannutricional_id == planNutricionalId && !u.deletemark);

                    var puedeDarDeBaja = cantidadUsuarios == 0;
                    var mensaje = puedeDarDeBaja
                        ? "El plan nutricional no tiene usuarios asociados. Puede darse de baja."
                        : string.Format("El plan nutricional tiene {0} usuario(s) asociado(s). Debe reasignar o dar de baja a los usuarios antes de dar de baja el plan nutricional.", cantidadUsuarios);

                    return new PlanNutricionalValidacionDto
                    {
                        PlanNutricionalId = plan.id,
                        PlanNutricionalNombre = plan.nombre,
                        CantidadUsuarios = cantidadUsuarios,
                        PuedeDarDeBaja = puedeDarDeBaja,
                        Mensaje = mensaje
                    };
                }
            }
            catch (Exception ex) when (!ex.Message.Contains("Plan nutricional no encontrado") && !ex.Message.Contains("ID del plan"))
            {
                _logger?.LogError("ValidarCantidadUsuarios: Error al validar plan nutricional", ex, new { PlanNutricionalId = planNutricionalId });
                throw;
            }
        }

        // ============ BAJA LÓGICA ============
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
                    var entity = ctx.sl_plannutricional
                        .FirstOrDefault(p => p.id == id && !p.deletemark);

                    if (entity == null)
                    {
                        _logger?.LogWarning("Eliminar: Plan nutricional no encontrado", new { PlanNutricionalId = id });
                        throw new Exception("Plan nutricional no encontrado.");
                    }

                    // Validar que no tenga usuarios asociados antes de dar de baja
                    var cantidadUsuarios = ctx.sl_usuario.Count(u => u.plannutricional_id == id && !u.deletemark);
                    if (cantidadUsuarios > 0)
                    {
                        _logger?.LogWarning("Eliminar: Plan nutricional con usuarios asociados", new { PlanNutricionalId = id, CantidadUsuarios = cantidadUsuarios });
                        throw new Exception(string.Format("No se puede dar de baja el plan nutricional. Tiene {0} usuario(s) asociado(s). Reasigne o dé de baja a los usuarios antes de continuar.", cantidadUsuarios));
                    }

                    // ===================== LOGGING: Inicio de eliminación =====================
                    _logger?.LogInformation("Eliminar: Iniciando eliminación lógica de plan nutricional", new
                    {
                        PlanNutricionalId = id,
                        Nombre = entity.nombre
                    });

                    entity.deletemark = true;
                    entity.updatedate = DateTime.Now;
                    entity.updateuser = username;

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Eliminación exitosa =====================
                        _logger?.LogInformation("Eliminar: Plan nutricional eliminado exitosamente", new
                        {
                            PlanNutricionalId = id,
                            Nombre = entity.nombre
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Eliminar: Error de validación al eliminar plan nutricional", ex, new
                        {
                            PlanNutricionalId = id,
                            ValidationErrors = HandleValidationException(ex, "eliminar").Message
                        });
                        throw HandleValidationException(ex, "eliminar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Eliminar: Deadlock detectado", ex, new
                        {
                            PlanNutricionalId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plan nutricional no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Eliminar: Error al eliminar plan nutricional", ex, new { PlanNutricionalId = id });
                throw;
            }
        }

        // ============ ACTIVAR (quitar baja) ============
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
                    var entity = ctx.sl_plannutricional
                        .FirstOrDefault(p => p.id == id && p.deletemark);

                    if (entity == null)
                    {
                        _logger?.LogWarning("Activar: Plan nutricional no encontrado o ya está activo", new { PlanNutricionalId = id });
                        throw new Exception("Plan nutricional no encontrado.");
                    }

                    // ===================== LOGGING: Inicio de activación =====================
                    _logger?.LogInformation("Activar: Iniciando activación de plan nutricional", new
                    {
                        PlanNutricionalId = id,
                        Nombre = entity.nombre
                    });

                    entity.deletemark = false;
                    entity.updatedate = DateTime.Now;
                    entity.updateuser = username;

                    try
                    {
                        ctx.SaveChanges();

                        // ===================== LOGGING: Activación exitosa =====================
                        _logger?.LogInformation("Activar: Plan nutricional activado exitosamente", new
                        {
                            PlanNutricionalId = id,
                            Nombre = entity.nombre
                        });
                    }
                    catch (DbEntityValidationException ex)
                    {
                        _logger?.LogError("Activar: Error de validación al activar plan nutricional", ex, new
                        {
                            PlanNutricionalId = id,
                            ValidationErrors = HandleValidationException(ex, "activar").Message
                        });
                        throw HandleValidationException(ex, "activar");
                    }
                    catch (SqlException ex) when (ex.Number == 1205) // Deadlock
                    {
                        _logger?.LogWarning("Activar: Deadlock detectado", ex, new
                        {
                            PlanNutricionalId = id,
                            ErrorNumber = ex.Number
                        });
                        throw new Exception("El sistema está ocupado. Por favor, intente nuevamente en unos momentos.");
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plan nutricional no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
            {
                _logger?.LogError("Activar: Error al activar plan nutricional", ex, new { PlanNutricionalId = id });
                throw;
            }
        }

        // ================= IMPRESIÓN =================
        public List<PlanNutricionalImpresionDto> ObtenerDatosImpresion(PlanNutricionalImpresionRequestDto request)
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
                        IncluirNombre = request.IncluirNombre,
                        IncluirDescripcion = request.IncluirDescripcion,
                        IncluirEstado = request.IncluirEstado
                    });

                    var query = ctx.sl_plannutricional
                        .AsNoTracking()
                        .AsQueryable();

                    // Aplicar filtro de estado
                    if (request.Estado == "Activo")
                        query = query.Where(p => !p.deletemark);
                    else if (request.Estado == "Inactivo")
                        query = query.Where(p => p.deletemark);
                    // Si es "Todos" o null, no se filtra (retorna todos)

                    // Ordenar por nombre
                    var items = query
                        .OrderBy(p => p.nombre)
                        .Select(p => new PlanNutricionalImpresionDto
                        {
                            Nombre = request.IncluirNombre ? p.nombre : null,
                            Descripcion = request.IncluirDescripcion ? p.descripcion : null,
                            Estado = request.IncluirEstado ? (p.deletemark ? "Inactivo" : "Activo") : null
                        })
                        .ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = items.Count,
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
