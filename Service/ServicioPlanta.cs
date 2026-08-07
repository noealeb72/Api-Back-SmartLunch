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
    public interface IServicioPlanta
    {
        PagedResultDto<PlantaListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado);
        PlantaDetalleDto ObtenerPorId(int id);
        PlantaDetalleDto CrearPlanta(Models.DTOs.PlantaCreateDto dto, string username);
        void ActualizarPlanta(Models.DTOs.PlantaUpdateDto dto, string username);
        void EliminarPlanta(int id, string username);
        void ActivarPlanta(int id, string username);
        IEnumerable<PlantaListadoDto> Buscar(string texto, bool soloActivos = true, int maxResultados = 20);
        List<PlantaImpresionDto> ObtenerDatosImpresion(PlantaImpresionRequestDto request);
        /// <summary>Valida la planta: devuelve la cantidad de usuarios asociados y si puede darse de baja (0 usuarios).</summary>
        PlantaValidacionDto ValidarCantidadUsuarios(int plantaId);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioPlanta : IServicioPlanta
    {
        private readonly ILoggerService _logger;

        public ServicioPlanta(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} la planta:");
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
        // Lista paginada de plantas
        // ============================================
        public PagedResultDto<PlantaListadoDto> ObtenerLista(
            int page,
            int pageSize,
            string search,
            bool estado)
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
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de plantas", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        Estado = estado
                    });

                    var query =
                        from p in ctx.sl_planta
                        select new PlantaListadoDto
                        {
                            Id = p.id,
                            Nombre = p.nombre,
                            Descripcion = p.descripcion,
                            Deletemark = p.deletemark,
                            IsDefault = p.is_default
                        };

                    // Filtro por activo/inactivo
                    query = estado
                        ? query.Where(x => !x.Deletemark)
                        : query.Where(x => x.Deletemark);

                    // Búsqueda opcional por nombre / descripción
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(p =>
                            (p.Nombre ?? "").ToLower().Contains(s) ||
                            (p.Descripcion ?? "").ToLower().Contains(s)
                        );
                    }

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderBy(p => p.Nombre)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("ObtenerLista: Búsqueda completada exitosamente", new
                    {
                        TotalItems = totalItems,
                        TotalPages = totalPages,
                        ItemsReturned = items.Count
                    });

                    return new PagedResultDto<PlantaListadoDto>
                    {
                        page = page,
                        pageSize = pageSize,
                        totalItems = totalItems,
                        totalPages = totalPages,
                        items = items
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerLista: Error al obtener lista de plantas", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    HasSearch = !string.IsNullOrWhiteSpace(search),
                    Estado = estado
                });
                throw;
            }
        }

        // ============================================
        // Obtener detalle por Id
        // ============================================
        public PlantaDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_planta
                        .Where(p => p.id == id && !p.deletemark)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Planta no encontrada", new { PlantaId = id });
                        throw new Exception("Planta no encontrada.");
                    }

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = new PlantaDetalleDto
                    {
                        Id = entity.id,
                        Nombre = entity.nombre,
                        Descripcion = entity.descripcion,
                        Activo = !entity.deletemark,
                        IsDefault = entity.is_default,
                        CreateDate = entity.createdate,
                        CreateUser = entity.createuser,
                        UpdateDate = entity.updatedate,
                        UpdateUser = entity.updateuser
                    };

                    _logger?.LogInformation("ObtenerPorId: Planta obtenida exitosamente", new 
                    { 
                        PlantaId = id,
                        Nombre = resultado.Nombre
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Planta no encontrada")))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener planta", ex, new { PlantaId = id });
                throw;
            }
        }

        // ============================================
        // Crear planta
        // ============================================
        public PlantaDetalleDto CrearPlanta(Models.DTOs.PlantaCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "El DTO de creación no puede ser nulo.");

            if (string.IsNullOrWhiteSpace(dto.nombre))
                throw new Exception("El nombre de la planta es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de creación =====================
                            _logger?.LogInformation("CrearPlanta: Iniciando creación de planta", new
                            {
                                Nombre = dto.nombre,
                                Username = username
                            });

                            // Validación de nombre único dentro de la transacción
                            var existeNombre = ctx.sl_planta.Any(p =>
                                p.nombre == dto.nombre.Trim() && !p.deletemark);

                            if (existeNombre)
                            {
                                _logger?.LogWarning("CrearPlanta: Intento de crear planta con nombre duplicado", new
                                {
                                    Nombre = dto.nombre
                                });
                                throw new Exception("Ya existe una planta con ese nombre.");
                            }

                            // Truncar campos según StringLength del modelo (usando Trim primero)
                            var nombreTruncado = dto.nombre.Trim();
                            if (nombreTruncado.Length > 150)
                                nombreTruncado = nombreTruncado.Substring(0, 150);

                            var descripcionTruncada = !string.IsNullOrEmpty(dto.descripcion)
                                ? dto.descripcion.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(descripcionTruncada) && descripcionTruncada.Length > 300)
                                descripcionTruncada = descripcionTruncada.Substring(0, 300);

                            var entity = new sl_planta
                            {
                                nombre = nombreTruncado,
                                descripcion = descripcionTruncada,
                                createdate = DateTime.Now,
                                createuser = username,
                                deletemark = false
                            };

                            ctx.sl_planta.Add(entity);

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                                var resultado = new PlantaDetalleDto
                                {
                                    Id = entity.id,
                                    Nombre = entity.nombre,
                                    Descripcion = entity.descripcion,
                                    Activo = !entity.deletemark,
                                    IsDefault = entity.is_default,
                                    CreateDate = entity.createdate,
                                    CreateUser = entity.createuser,
                                    UpdateDate = entity.updatedate,
                                    UpdateUser = entity.updateuser
                                };

                                // ===================== LOGGING: Creación exitosa =====================
                                _logger?.LogInformation("CrearPlanta: Planta creada exitosamente", new
                                {
                                    PlantaId = resultado.Id,
                                    Nombre = resultado.Nombre
                                });

                                return resultado;
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                throw HandleValidationException(ex, "crear");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("CrearPlanta: Deadlock detectado, reintentando...", new
                                {
                                    Nombre = dto.nombre
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("CrearPlanta: Error al crear planta", ex, new
                {
                    Nombre = dto?.nombre,
                    Username = username
                });
                throw;
            }
        }

        // ============================================
        // Actualizar planta
        // ============================================
        public void ActualizarPlanta(Models.DTOs.PlantaUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "El DTO de actualización no puede ser nulo.");

            if (dto.id <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(dto.nombre))
                throw new Exception("El nombre de la planta es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de actualización =====================
                            _logger?.LogInformation("ActualizarPlanta: Iniciando actualización de planta", new
                            {
                                PlantaId = dto.id,
                                Nombre = dto.nombre,
                                Username = username
                            });

                            var entity = ctx.sl_planta.FirstOrDefault(p => p.id == dto.id && !p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActualizarPlanta: Planta no encontrada", new { PlantaId = dto.id });
                                throw new Exception("Planta no encontrada.");
                            }

                            // Validación de nombre único (excluyendo la misma) dentro de la transacción
                            var existeNombre = ctx.sl_planta.Any(p =>
                                p.id != dto.id &&
                                p.nombre == dto.nombre.Trim() &&
                                !p.deletemark);

                            if (existeNombre)
                            {
                                _logger?.LogWarning("ActualizarPlanta: Intento de actualizar con nombre duplicado", new
                                {
                                    PlantaId = dto.id,
                                    Nombre = dto.nombre
                                });
                                throw new Exception("Ya existe otra planta con ese nombre.");
                            }

                            // Truncar campos según StringLength del modelo (usando Trim primero)
                            var nombreTruncado = dto.nombre.Trim();
                            if (nombreTruncado.Length > 150)
                                nombreTruncado = nombreTruncado.Substring(0, 150);

                            var descripcionTruncada = !string.IsNullOrEmpty(dto.descripcion)
                                ? dto.descripcion.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(descripcionTruncada) && descripcionTruncada.Length > 300)
                                descripcionTruncada = descripcionTruncada.Substring(0, 300);

                            entity.nombre = nombreTruncado;
                            entity.descripcion = descripcionTruncada;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Actualización exitosa =====================
                                _logger?.LogInformation("ActualizarPlanta: Planta actualizada exitosamente", new
                                {
                                    PlantaId = entity.id,
                                    Nombre = entity.nombre
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                throw HandleValidationException(ex, "actualizar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("ActualizarPlanta: Deadlock detectado, reintentando...", new
                                {
                                    PlantaId = dto.id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Planta no encontrada") || ex.Message.Contains("Ya existe") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Planta no encontrada") || ex.Message.Contains("Ya existe") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ActualizarPlanta: Error al actualizar planta", ex, new
                {
                    PlantaId = dto?.id,
                    Nombre = dto?.nombre,
                    Username = username
                });
                throw;
            }
        }

        // ============================================
        // Validar cantidad de usuarios (solo Admin y Gerencia)
        // ============================================
        public PlantaValidacionDto ValidarCantidadUsuarios(int plantaId)
        {
            if (plantaId <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var planta = ctx.sl_planta
                        .Where(p => p.id == plantaId)
                        .Select(p => new { p.id, p.nombre })
                        .FirstOrDefault();

                    if (planta == null)
                        throw new Exception("Planta no encontrada.");

                    // Usuarios activos (no borrados) asociados a esta planta
                    var cantidadUsuarios = ctx.sl_usuario
                        .Count(u => u.planta_id == plantaId && !u.deletemark);

                    var puedeDarDeBaja = cantidadUsuarios == 0;
                    var mensaje = puedeDarDeBaja
                        ? "La planta no tiene usuarios asociados. Puede darse de baja."
                        : string.Format("La planta tiene {0} usuario(s) asociado(s). Debe reasignar o dar de baja a los usuarios antes de dar de baja la planta.", cantidadUsuarios);

                    return new PlantaValidacionDto
                    {
                        PlantaId = planta.id,
                        PlantaNombre = planta.nombre,
                        CantidadUsuarios = cantidadUsuarios,
                        PuedeDarDeBaja = puedeDarDeBaja,
                        Mensaje = mensaje
                    };
                }
            }
            catch (Exception ex) when (!ex.Message.Contains("Planta no encontrada") && !ex.Message.Contains("ID de la planta"))
            {
                _logger?.LogError("ValidarCantidadUsuarios: Error al validar planta", ex, new { PlantaId = plantaId });
                throw;
            }
        }

        // ============================================
        // Baja lógica
        // ============================================
        public void EliminarPlanta(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de eliminación =====================
                            _logger?.LogInformation("EliminarPlanta: Iniciando eliminación lógica de planta", new
                            {
                                PlantaId = id,
                                Username = username
                            });

                            var entity = ctx.sl_planta.FirstOrDefault(p => p.id == id && !p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("EliminarPlanta: Planta no encontrada", new { PlantaId = id });
                                throw new Exception("Planta no encontrada.");
                            }

                            // Validar que no tenga usuarios asociados antes de dar de baja
                            var cantidadUsuarios = ctx.sl_usuario.Count(u => u.planta_id == id && !u.deletemark);
                            if (cantidadUsuarios > 0)
                            {
                                _logger?.LogWarning("EliminarPlanta: Planta con usuarios asociados", new { PlantaId = id, CantidadUsuarios = cantidadUsuarios });
                                throw new Exception(string.Format("No se puede dar de baja la planta. Tiene {0} usuario(s) asociado(s). Reasigne o dé de baja a los usuarios antes de continuar.", cantidadUsuarios));
                            }

                            entity.deletemark = true;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Eliminación exitosa =====================
                                _logger?.LogInformation("EliminarPlanta: Planta eliminada exitosamente", new
                                {
                                    PlantaId = id
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                throw HandleValidationException(ex, "eliminar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("EliminarPlanta: Deadlock detectado, reintentando...", new
                                {
                                    PlantaId = id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Planta no encontrada") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Planta no encontrada") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("EliminarPlanta: Error al eliminar planta", ex, new
                {
                    PlantaId = id,
                    Username = username
                });
                throw;
            }
        }

        // ============================================
        // Activar (quitar baja lógica)
        // ============================================
        public void ActivarPlanta(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de activación =====================
                            _logger?.LogInformation("ActivarPlanta: Iniciando activación de planta", new
                            {
                                PlantaId = id,
                                Username = username
                            });

                            var entity = ctx.sl_planta.FirstOrDefault(p => p.id == id && p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActivarPlanta: Planta no encontrada o ya activa", new { PlantaId = id });
                                throw new Exception("Planta no encontrada.");
                            }

                            entity.deletemark = false;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Activación exitosa =====================
                                _logger?.LogInformation("ActivarPlanta: Planta activada exitosamente", new
                                {
                                    PlantaId = id
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                throw HandleValidationException(ex, "activar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("ActivarPlanta: Deadlock detectado, reintentando...", new
                                {
                                    PlantaId = id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Planta no encontrada") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Planta no encontrada") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ActivarPlanta: Error al activar planta", ex, new
                {
                    PlantaId = id,
                    Username = username
                });
                throw;
            }
        }

        // ============================================
        // Buscador liviano (para combos / autocomplete)
        // ============================================
        public IEnumerable<PlantaListadoDto> Buscar(
            string termino,
            bool activo,
            int maxResultados)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (string.IsNullOrWhiteSpace(termino))
                return new List<PlantaListadoDto>();

            // Validar longitud de término de búsqueda
            if (termino.Length > 200)
                throw new Exception("El término de búsqueda no puede exceder 200 caracteres.");

            if (maxResultados <= 0 || maxResultados > 100)
                maxResultados = 20;

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("Buscar: Iniciando búsqueda de plantas", new
                    {
                        Termino = termino,
                        Activo = activo,
                        MaxResultados = maxResultados
                    });

                    var texto = termino.Trim().ToLower();

                    var query =
                        from p in ctx.sl_planta
                        select new PlantaListadoDto
                        {
                            Id = p.id,
                            Nombre = p.nombre,
                            Descripcion = p.descripcion,
                            Deletemark = p.deletemark,
                            IsDefault = p.is_default
                        };

                    query = activo
                        ? query.Where(x => !x.Deletemark)
                        : query.Where(x => x.Deletemark);

                    query = query.Where(x =>
                        (x.Nombre ?? "").ToLower().Contains(texto) ||
                        (x.Descripcion ?? "").ToLower().Contains(texto)
                    );

                    var resultados = query
                        .OrderBy(x => x.Nombre)
                        .Take(maxResultados)
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("Buscar: Búsqueda completada exitosamente", new
                    {
                        Termino = termino,
                        ResultadosEncontrados = resultados.Count
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("Buscar: Error al buscar plantas", ex, new
                {
                    Termino = termino,
                    Activo = activo,
                    MaxResultados = maxResultados
                });
                throw;
            }
        }

        // ================= IMPRESIÓN =================
        public List<PlantaImpresionDto> ObtenerDatosImpresion(PlantaImpresionRequestDto request)
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

                    var query = ctx.sl_planta
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
                        .Select(p => new PlantaImpresionDto
                        {
                            Nombre = request.IncluirNombre ? p.nombre : null,
                            Descripcion = request.IncluirDescripcion ? p.descripcion : null,
                            Estado = request.IncluirEstado ? (p.deletemark ? "Inactivo" : "Activo") : null
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
