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
    public interface IServicioProyecto
    {
        PagedResultDto<ProyectoListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado);
        ProyectoDetalleDto ObtenerPorId(int id);
        ProyectoDetalleDto CrearProyecto(ProyectoCreateDto dto, string username);
        void ActualizarProyecto(ProyectoUpdateDto dto, string username);
        void EliminarProyecto(int id, string username);
        void ActivarProyecto(int id, string username);
        System.Collections.Generic.IEnumerable<ProyectoComboDto> ObtenerActivosParaCombo(int? plantaId = null);
        // Método sobrecargado con más parámetros (usado internamente)
        System.Collections.Generic.IEnumerable<ProyectoComboDto> ObtenerActivosParaCombo(int? plantaId, int? centroCostoId);
        List<ProyectoImpresionDto> ObtenerDatosImpresion(ProyectoImpresionRequestDto request);
        ProyectoValidacionDto ValidarCantidadUsuarios(int proyectoId);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioProyecto : IServicioProyecto
    {
        private readonly ILoggerService _logger;

        public ServicioProyecto(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el proyecto:");
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

        // Implementación de la interfaz: ObtenerLista(int page, int pageSize, string search, bool estado)
        public PagedResultDto<ProyectoListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado)
        {
            // Usar el método sobrecargado con valores por defecto
            return ObtenerLista(page, pageSize, search, plantaId: null, centroCostoId: null, estado: estado);
        }

        // Método sobrecargado con más parámetros (usado internamente)
        public PagedResultDto<ProyectoListadoDto> ObtenerLista(
            int page,
            int pageSize,
            string search,
            int? plantaId,
            int? centroCostoId,
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
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de proyectos", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        PlantaId = plantaId,
                        CentroCostoId = centroCostoId,
                        Estado = estado
                    });

                    var query =
                        ctx.sl_proyecto
                            .Include(p => p.planta)
                            .Include(p => p.centrodecosto)
                            .Select(p => new ProyectoListadoDto
                            {
                                Id = p.id,
                                Nombre = p.nombre,
                                Descripcion = p.descripcion,

                                PlantaId = p.planta_id,
                                PlantaNombre = p.planta != null ? p.planta.nombre : null,

                                CentroCostoId = p.centrodecosto_id,
                                CentroCostoNombre = p.centrodecosto != null ? p.centrodecosto.nombre : null,

                                Activo = !p.deletemark,
                                IsDefault = p.is_default
                            });

                // Filtros adicionales
                if (plantaId.HasValue && plantaId.Value > 0)
                    query = query.Where(x => x.PlantaId == plantaId.Value);

                if (centroCostoId.HasValue && centroCostoId.Value > 0)
                    query = query.Where(x => x.CentroCostoId == centroCostoId.Value);

                // Activo / inactivo
                query = estado
                    ? query.Where(x => x.Activo)
                    : query.Where(x => !x.Activo);

                // Buscador por nombre / descripción
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
                        .OrderBy(x => x.PlantaNombre)
                        .ThenBy(x => x.CentroCostoNombre)
                        .ThenBy(x => x.Nombre)
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

                    return new PagedResultDto<ProyectoListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de proyectos", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    HasSearch = !string.IsNullOrWhiteSpace(search),
                    PlantaId = plantaId,
                    CentroCostoId = centroCostoId,
                    Estado = estado
                });
                throw;
            }
        }

        // ========== Detalle por Id ==========
        public ProyectoDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_proyecto
                        .Include(p => p.planta)
                        .Include(p => p.centrodecosto)
                        .Where(p => p.id == id && !p.deletemark)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Proyecto no encontrado", new { ProyectoId = id });
                        throw new Exception("Proyecto no encontrado.");
                    }

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = new ProyectoDetalleDto
                    {
                        Id = entity.id,
                        Nombre = entity.nombre,
                        Descripcion = entity.descripcion,
                        PlantaId = entity.planta_id,
                        PlantaNombre = entity.planta != null ? entity.planta.nombre : null,
                        CentroCostoId = entity.centrodecosto_id,
                        CentroCostoNombre = entity.centrodecosto != null ? entity.centrodecosto.nombre : null,
                        Activo = !entity.deletemark,
                        IsDefault = entity.is_default,
                        CreateDate = entity.createdate,
                        CreateUser = entity.createuser,
                        UpdateDate = entity.updatedate,
                        UpdateUser = entity.updateuser
                    };

                    _logger?.LogInformation("ObtenerPorId: Proyecto obtenido exitosamente", new
                    {
                        ProyectoId = id,
                        Nombre = resultado.Nombre
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Proyecto no encontrado")))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener proyecto", ex, new { ProyectoId = id });
                throw;
            }
        }

        // ========== Crear proyecto ==========
        public ProyectoDetalleDto CrearProyecto(ProyectoCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "El DTO de creación no puede ser nulo.");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            if (dto.PlantaId <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (dto.CentroCostoId <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de creación =====================
                            _logger?.LogInformation("CrearProyecto: Iniciando creación de proyecto", new
                            {
                                Nombre = dto.Nombre,
                                PlantaId = dto.PlantaId,
                                CentroCostoId = dto.CentroCostoId,
                                Username = username
                            });

                            // Validar planta
                            var planta = ctx.sl_planta.FirstOrDefault(p => p.id == dto.PlantaId && !p.deletemark);
                            if (planta == null)
                            {
                                _logger?.LogWarning("CrearProyecto: Planta no encontrada", new { PlantaId = dto.PlantaId });
                                throw new Exception("La planta seleccionada no existe.");
                            }

                            // Validar centro de costo
                            var centroCosto = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == dto.CentroCostoId && !c.deletemark);
                            if (centroCosto == null)
                            {
                                _logger?.LogWarning("CrearProyecto: Centro de costo no encontrado", new { CentroCostoId = dto.CentroCostoId });
                                throw new Exception("El centro de costo seleccionado no existe.");
                            }

                            // No permitir nombres duplicados dentro de la misma planta+centroCosto dentro de la transacción
                            var existe = ctx.sl_proyecto.Any(p =>
                                p.nombre == dto.Nombre.Trim() &&
                                p.planta_id == dto.PlantaId &&
                                p.centrodecosto_id == dto.CentroCostoId &&
                                !p.deletemark);

                            if (existe)
                            {
                                _logger?.LogWarning("CrearProyecto: Intento de crear proyecto con nombre duplicado", new
                                {
                                    Nombre = dto.Nombre,
                                    PlantaId = dto.PlantaId,
                                    CentroCostoId = dto.CentroCostoId
                                });
                                throw new Exception("Ya existe un proyecto con ese nombre en esa planta y centro de costo.");
                            }

                            // Truncar campos según StringLength del modelo (usando Trim primero)
                            var nombreTruncado = dto.Nombre.Trim();
                            if (nombreTruncado.Length > 150)
                                nombreTruncado = nombreTruncado.Substring(0, 150);

                            var descripcionTruncada = !string.IsNullOrEmpty(dto.Descripcion)
                                ? dto.Descripcion.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(descripcionTruncada) && descripcionTruncada.Length > 300)
                                descripcionTruncada = descripcionTruncada.Substring(0, 300);

                            var entity = new sl_proyecto
                            {
                                nombre = nombreTruncado,
                                descripcion = descripcionTruncada,
                                planta_id = dto.PlantaId,
                                centrodecosto_id = dto.CentroCostoId,
                                deletemark = false,
                                createdate = DateTime.Now,
                                createuser = username
                            };

                            ctx.sl_proyecto.Add(entity);

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                                var resultado = new ProyectoDetalleDto
                                {
                                    Id = entity.id,
                                    Nombre = entity.nombre,
                                    Descripcion = entity.descripcion,
                                    PlantaId = entity.planta_id,
                                    PlantaNombre = planta.nombre,
                                    CentroCostoId = entity.centrodecosto_id,
                                    CentroCostoNombre = centroCosto.nombre,
                                    Activo = !entity.deletemark,
                                    IsDefault = entity.is_default,
                                    CreateDate = entity.createdate,
                                    CreateUser = entity.createuser,
                                    UpdateDate = entity.updatedate,
                                    UpdateUser = entity.updateuser
                                };

                                // ===================== LOGGING: Creación exitosa =====================
                                _logger?.LogInformation("CrearProyecto: Proyecto creado exitosamente", new
                                {
                                    ProyectoId = resultado.Id,
                                    Nombre = resultado.Nombre
                                });

                                return resultado;
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("CrearProyecto: Error de validación al guardar proyecto", ex, new
                                {
                                    Nombre = dto.Nombre,
                                    PlantaId = dto.PlantaId,
                                    CentroCostoId = dto.CentroCostoId,
                                    ValidationErrors = HandleValidationException(ex, "crear").Message
                                });
                                throw HandleValidationException(ex, "crear");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("CrearProyecto: Deadlock detectado, reintentando...", new
                                {
                                    Nombre = dto.Nombre
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("no existe") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("CrearProyecto: Error al crear proyecto dentro de transacción", ex, new
                            {
                                Nombre = dto?.Nombre,
                                PlantaId = dto?.PlantaId,
                                CentroCostoId = dto?.CentroCostoId,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("no existe") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("CrearProyecto: Error al crear proyecto", ex, new
                {
                    Nombre = dto?.Nombre,
                    PlantaId = dto?.PlantaId,
                    CentroCostoId = dto?.CentroCostoId,
                    Username = username
                });
                throw;
            }
        }

        // ========== Actualizar proyecto ==========
        public void ActualizarProyecto(ProyectoUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "El DTO de actualización no puede ser nulo.");

            if (dto.Id <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            if (dto.PlantaId <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (dto.CentroCostoId <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de actualización =====================
                            _logger?.LogInformation("ActualizarProyecto: Iniciando actualización de proyecto", new
                            {
                                ProyectoId = dto.Id,
                                Nombre = dto.Nombre,
                                PlantaId = dto.PlantaId,
                                CentroCostoId = dto.CentroCostoId,
                                Username = username
                            });

                            var entity = ctx.sl_proyecto.FirstOrDefault(p => p.id == dto.Id && !p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActualizarProyecto: Proyecto no encontrado", new { ProyectoId = dto.Id });
                                throw new Exception("Proyecto no encontrado.");
                            }

                            // Validar planta
                            var planta = ctx.sl_planta.FirstOrDefault(p => p.id == dto.PlantaId && !p.deletemark);
                            if (planta == null)
                            {
                                _logger?.LogWarning("ActualizarProyecto: Planta no encontrada", new { PlantaId = dto.PlantaId });
                                throw new Exception("La planta seleccionada no existe.");
                            }

                            // Validar centro de costo
                            var centroCosto = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == dto.CentroCostoId && !c.deletemark);
                            if (centroCosto == null)
                            {
                                _logger?.LogWarning("ActualizarProyecto: Centro de costo no encontrado", new { CentroCostoId = dto.CentroCostoId });
                                throw new Exception("El centro de costo seleccionado no existe.");
                            }

                            // Validación de nombre único (excluyendo la misma) dentro de la transacción
                            var existe = ctx.sl_proyecto.Any(p =>
                                p.id != dto.Id &&
                                p.nombre == dto.Nombre.Trim() &&
                                p.planta_id == dto.PlantaId &&
                                p.centrodecosto_id == dto.CentroCostoId &&
                                !p.deletemark);

                            if (existe)
                            {
                                _logger?.LogWarning("ActualizarProyecto: Intento de actualizar con nombre duplicado", new
                                {
                                    ProyectoId = dto.Id,
                                    Nombre = dto.Nombre,
                                    PlantaId = dto.PlantaId,
                                    CentroCostoId = dto.CentroCostoId
                                });
                                throw new Exception("Ya existe otro proyecto con ese nombre en esa planta y centro de costo.");
                            }

                            // Truncar campos según StringLength del modelo (usando Trim primero)
                            var nombreTruncado = dto.Nombre.Trim();
                            if (nombreTruncado.Length > 150)
                                nombreTruncado = nombreTruncado.Substring(0, 150);

                            var descripcionTruncada = !string.IsNullOrEmpty(dto.Descripcion)
                                ? dto.Descripcion.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(descripcionTruncada) && descripcionTruncada.Length > 300)
                                descripcionTruncada = descripcionTruncada.Substring(0, 300);

                            entity.nombre = nombreTruncado;
                            entity.descripcion = descripcionTruncada;
                            entity.planta_id = dto.PlantaId;
                            entity.centrodecosto_id = dto.CentroCostoId;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Actualización exitosa =====================
                                _logger?.LogInformation("ActualizarProyecto: Proyecto actualizado exitosamente", new
                                {
                                    ProyectoId = entity.id,
                                    Nombre = entity.nombre
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActualizarProyecto: Error de validación al guardar proyecto", ex, new
                                {
                                    ProyectoId = dto.Id,
                                    Nombre = dto.Nombre,
                                    ValidationErrors = HandleValidationException(ex, "actualizar").Message
                                });
                                throw HandleValidationException(ex, "actualizar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("ActualizarProyecto: Deadlock detectado, reintentando...", new
                                {
                                    ProyectoId = dto.Id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Proyecto no encontrado") || ex.Message.Contains("Ya existe") || ex.Message.Contains("no existe") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("ActualizarProyecto: Error al actualizar proyecto dentro de transacción", ex, new
                            {
                                ProyectoId = dto?.Id,
                                Nombre = dto?.Nombre,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Proyecto no encontrado") || ex.Message.Contains("Ya existe") || ex.Message.Contains("no existe") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ActualizarProyecto: Error al actualizar proyecto", ex, new
                {
                    ProyectoId = dto?.Id,
                    Nombre = dto?.Nombre,
                    Username = username
                });
                throw;
            }
        }

        // ========== Validar cantidad de usuarios ==========
        public ProyectoValidacionDto ValidarCantidadUsuarios(int proyectoId)
        {
            if (proyectoId <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var proyecto = ctx.sl_proyecto
                        .Where(p => p.id == proyectoId)
                        .Select(p => new { p.id, p.nombre })
                        .FirstOrDefault();

                    if (proyecto == null)
                        throw new Exception("Proyecto no encontrado.");

                    var cantidadUsuarios = ctx.sl_usuario
                        .Count(u => u.proyecto_id == proyectoId && !u.deletemark);

                    var puedeDarDeBaja = cantidadUsuarios == 0;
                    var mensaje = puedeDarDeBaja
                        ? "El proyecto no tiene usuarios asociados. Puede darse de baja."
                        : string.Format("El proyecto tiene {0} usuario(s) asociado(s). Debe reasignar o dar de baja a los usuarios antes de dar de baja el proyecto.", cantidadUsuarios);

                    return new ProyectoValidacionDto
                    {
                        ProyectoId = proyecto.id,
                        ProyectoNombre = proyecto.nombre,
                        CantidadUsuarios = cantidadUsuarios,
                        PuedeDarDeBaja = puedeDarDeBaja,
                        Mensaje = mensaje
                    };
                }
            }
            catch (Exception ex) when (!ex.Message.Contains("Proyecto no encontrado") && !ex.Message.Contains("ID del proyecto"))
            {
                _logger?.LogError("ValidarCantidadUsuarios: Error al validar proyecto", ex, new { ProyectoId = proyectoId });
                throw;
            }
        }

        // ========== Baja lógica ==========
        public void EliminarProyecto(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

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
                            _logger?.LogInformation("EliminarProyecto: Iniciando eliminación lógica de proyecto", new
                            {
                                ProyectoId = id,
                                Username = username
                            });

                            var entity = ctx.sl_proyecto.FirstOrDefault(p => p.id == id && !p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("EliminarProyecto: Proyecto no encontrado", new { ProyectoId = id });
                                throw new Exception("Proyecto no encontrado.");
                            }

                            // Validar que no tenga usuarios asociados antes de dar de baja
                            var cantidadUsuarios = ctx.sl_usuario.Count(u => u.proyecto_id == id && !u.deletemark);
                            if (cantidadUsuarios > 0)
                            {
                                _logger?.LogWarning("EliminarProyecto: Proyecto con usuarios asociados", new { ProyectoId = id, CantidadUsuarios = cantidadUsuarios });
                                throw new Exception(string.Format("No se puede dar de baja el proyecto. Tiene {0} usuario(s) asociado(s). Reasigne o dé de baja a los usuarios antes de continuar.", cantidadUsuarios));
                            }

                            entity.deletemark = true;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Eliminación exitosa =====================
                                _logger?.LogInformation("EliminarProyecto: Proyecto eliminado exitosamente", new
                                {
                                    ProyectoId = id
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("EliminarProyecto: Error de validación al eliminar proyecto", ex, new
                                {
                                    ProyectoId = id,
                                    ValidationErrors = HandleValidationException(ex, "eliminar").Message
                                });
                                throw HandleValidationException(ex, "eliminar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("EliminarProyecto: Deadlock detectado, reintentando...", new
                                {
                                    ProyectoId = id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Proyecto no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("EliminarProyecto: Error al eliminar proyecto dentro de transacción", ex, new
                            {
                                ProyectoId = id,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Proyecto no encontrado") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("EliminarProyecto: Error al eliminar proyecto", ex, new
                {
                    ProyectoId = id,
                    Username = username
                });
                throw;
            }
        }

        // ========== Activar (quitar baja lógica) ==========
        public void ActivarProyecto(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

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
                            _logger?.LogInformation("ActivarProyecto: Iniciando activación de proyecto", new
                            {
                                ProyectoId = id,
                                Username = username
                            });

                            var entity = ctx.sl_proyecto.FirstOrDefault(p => p.id == id && p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActivarProyecto: Proyecto no encontrado o ya activo", new { ProyectoId = id });
                                throw new Exception("Proyecto no encontrado.");
                            }

                            entity.deletemark = false;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Activación exitosa =====================
                                _logger?.LogInformation("ActivarProyecto: Proyecto activado exitosamente", new
                                {
                                    ProyectoId = id
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActivarProyecto: Error de validación al activar proyecto", ex, new
                                {
                                    ProyectoId = id,
                                    ValidationErrors = HandleValidationException(ex, "activar").Message
                                });
                                throw HandleValidationException(ex, "activar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("ActivarProyecto: Deadlock detectado, reintentando...", new
                                {
                                    ProyectoId = id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Proyecto no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("ActivarProyecto: Error al activar proyecto dentro de transacción", ex, new
                            {
                                ProyectoId = id,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Proyecto no encontrado") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ActivarProyecto: Error al activar proyecto", ex, new
                {
                    ProyectoId = id,
                    Username = username
                });
                throw;
            }
        }

        // Implementación de la interfaz: ObtenerActivosParaCombo(int? plantaId = null)
        public System.Collections.Generic.IEnumerable<ProyectoComboDto> ObtenerActivosParaCombo(int? plantaId = null)
        {
            // Usar el método sobrecargado con centroCostoId null
            return ObtenerActivosParaCombo(plantaId, centroCostoId: null);
        }

        // Método sobrecargado con más parámetros (usado internamente)
        public System.Collections.Generic.IEnumerable<ProyectoComboDto> ObtenerActivosParaCombo(
            int? plantaId,
            int? centroCostoId)
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("ObtenerActivosParaCombo: Iniciando búsqueda de proyectos activos", new
                    {
                        PlantaId = plantaId,
                        CentroCostoId = centroCostoId
                    });

                    var query =
                        ctx.sl_proyecto
                            .Where(p => !p.deletemark);

                    if (plantaId.HasValue && plantaId.Value > 0)
                        query = query.Where(p => p.planta_id == plantaId.Value);

                    if (centroCostoId.HasValue && centroCostoId.Value > 0)
                        query = query.Where(p => p.centrodecosto_id == centroCostoId.Value);

                    var items = query
                        .OrderBy(p => p.nombre)
                        .Select(p => new ProyectoComboDto
                        {
                            Id = p.id,
                            Nombre = p.nombre
                        })
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("ObtenerActivosParaCombo: Búsqueda completada exitosamente", new
                    {
                        PlantaId = plantaId,
                        CentroCostoId = centroCostoId,
                        ResultadosEncontrados = items.Count
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerActivosParaCombo: Error al obtener proyectos activos", ex, new
                {
                    PlantaId = plantaId,
                    CentroCostoId = centroCostoId
                });
                throw;
            }
        }

        // ================= IMPRESIÓN =================
        public List<ProyectoImpresionDto> ObtenerDatosImpresion(ProyectoImpresionRequestDto request)
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
                        PlantaId = request.PlantaId,
                        CentroCostoId = request.CentroCostoId,
                        IncluirNombre = request.IncluirNombre,
                        IncluirDescripcion = request.IncluirDescripcion,
                        IncluirPlanta = request.IncluirPlanta,
                        IncluirCentroCosto = request.IncluirCentroCosto,
                        IncluirEstado = request.IncluirEstado
                    });

                    var query = ctx.sl_proyecto
                        .Include(p => p.planta)
                        .Include(p => p.centrodecosto)
                        .AsNoTracking()
                        .AsQueryable();

                    // Aplicar filtro de estado
                    if (request.Estado == "Activo")
                        query = query.Where(p => !p.deletemark);
                    else if (request.Estado == "Inactivo")
                        query = query.Where(p => p.deletemark);
                    // Si es "Todos" o null, no se filtra

                    // Aplicar filtro de planta
                    if (request.PlantaId.HasValue && request.PlantaId.Value > 0)
                        query = query.Where(p => p.planta_id == request.PlantaId.Value);

                    // Aplicar filtro de centro de costo
                    if (request.CentroCostoId.HasValue && request.CentroCostoId.Value > 0)
                        query = query.Where(p => p.centrodecosto_id == request.CentroCostoId.Value);

                    // Ordenar por planta, centro de costo y nombre
                    var items = query
                        .OrderBy(p => p.planta != null ? p.planta.nombre : "")
                        .ThenBy(p => p.centrodecosto != null ? p.centrodecosto.nombre : "")
                        .ThenBy(p => p.nombre)
                        .Select(p => new ProyectoImpresionDto
                        {
                            Nombre = request.IncluirNombre ? p.nombre : null,
                            Descripcion = request.IncluirDescripcion ? p.descripcion : null,
                            Planta = request.IncluirPlanta ? (p.planta != null ? p.planta.nombre : null) : null,
                            CentroCosto = request.IncluirCentroCosto ? (p.centrodecosto != null ? p.centrodecosto.nombre : null) : null,
                            Estado = request.IncluirEstado ? (p.deletemark ? "Inactivo" : "Activo") : null
                        })
                        .ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    var totalItems = items.Count;
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = totalItems,
                        Estado = request.Estado,
                        PlantaId = request.PlantaId,
                        CentroCostoId = request.CentroCostoId
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex, new
                {
                    Estado = request?.Estado,
                    PlantaId = request?.PlantaId,
                    CentroCostoId = request?.CentroCostoId,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }
    }
}
