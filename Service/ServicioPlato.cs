using smartlunch_api.Dtos;
using smartlunch_api.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Hosting;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioPlato
    {
        PagedResultDto<PlatoListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado);
        PlatoDetalleDto ObtenerPorId(int id);
        PlatoDetalleDto CrearPlato(PlatoCreateDto dto, string username);
        void ActualizarPlato(PlatoUpdateDto dto, string username);
        void EliminarPlato(int id, string username);
        void ActivarPlato(int id, string username);
        IEnumerable<PlatoListadoDto> BuscarPlatos(string texto, bool soloActivos = true, int maxResultados = 20);
        IEnumerable<PlatoBusquedaSimpleDto> BuscarPlatosSimple(string texto, bool soloActivos = true, int maxResultados = 20);
        IEnumerable<PlatoListadoDto> ObtenerPlatosPorPlanNutricional(int planNutricionalId, bool soloActivos = true);
        List<PlatoImpresionDto> ObtenerDatosImpresion(PlatoImpresionRequestDto request);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioPlato : IServicioPlato
    {
        private readonly ILoggerService _logger;

        public ServicioPlato(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el plato:");
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

        // =========================================================
        // Lista paginada de platos con buscador y filtro de estado
        // =========================================================
        public PagedResultDto<PlatoListadoDto> ObtenerLista(
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
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de platos", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        Activo = activo
                    });

                    var query =
                        from p in ctx.sl_plato
                        join pn in ctx.sl_plannutricional
                            on p.plannutricional_id equals pn.id into pnJoin
                        from pn in pnJoin.DefaultIfEmpty()
                        select new PlatoListadoDto
                        {
                            Id = p.id,
                            Codigo = p.codigo,
                            Descripcion = p.descripcion,
                            Costo = p.costo,
                            CostoProveedor = p.costo_proveedor,
                            Plannutricional_id = p.plannutricional_id,
                            Plannutricional_nombre = pn != null ? pn.nombre : null,
                            Foto = p.foto,
                            Deletemark = p.deletemark,
                            Createdate = p.createdate
                        };

                    // Activos / inactivos
                    query = activo
                        ? query.Where(x => !x.Deletemark)
                        : query.Where(x => x.Deletemark);

                    // Buscador por código / descripción / plan nutricional
                    if (!string.IsNullOrWhiteSpace(search))
                    {
                        var s = search.Trim().ToLower();
                        query = query.Where(x =>
                            (x.Codigo ?? "").ToLower().Contains(s) ||
                            (x.Descripcion ?? "").ToLower().Contains(s) ||
                            (x.Plannutricional_nombre ?? "").ToLower().Contains(s)
                        );
                    }

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderByDescending(x => x.Createdate)
                        .ThenBy(x => x.Codigo)
                        .ThenBy(x => x.Descripcion)
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

                    return new PagedResultDto<PlatoListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de platos", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    HasSearch = !string.IsNullOrWhiteSpace(search),
                    Estado = activo
                });
                throw;
            }
        }

        // =========================================================
        // Detalle de un plato por Id
        // =========================================================
        public PlatoDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del plato debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_plato
                        .Include(p => p.plannutricional)
                        .Where(p => p.id == id && !p.deletemark)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Plato no encontrado", new { PlatoId = id });
                        throw new Exception("Plato no encontrado.");
                    }

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = new PlatoDetalleDto
                    {
                        Id = entity.id,
                        Codigo = entity.codigo,
                        Descripcion = entity.descripcion,
                        Ingredientes = entity.ingredientes,
                        Presentacion = entity.presentacion,
                        Plannutricional_id = entity.plannutricional_id,
                        Plannutricional_nombre = entity.plannutricional != null ? entity.plannutricional.nombre : null,
                        Costo = entity.costo,
                        CostoProveedor = entity.costo_proveedor,
                        Foto = entity.foto
                    };

                    _logger?.LogInformation("ObtenerPorId: Plato obtenido exitosamente", new
                    {
                        PlatoId = id,
                        Codigo = resultado.Codigo
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Plato no encontrado")))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener plato", ex, new { PlatoId = id });
                throw;
            }
        }

        // =========================================================
        // Crear plato
        // =========================================================
        public PlatoDetalleDto CrearPlato(PlatoCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "El DTO de creación no puede ser nulo.");

            if (string.IsNullOrWhiteSpace(dto.Codigo))
                throw new Exception("El código es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new Exception("La descripción es obligatoria.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            if (!dto.Plannutricional_id.HasValue || dto.Plannutricional_id.Value <= 0)
                throw new Exception("Debe seleccionar un plan nutricional.");

            if (dto.Costo < 0)
                throw new Exception("El costo no puede ser negativo.");

            if (dto.CostoProveedor < 0)
                throw new Exception("El costo de proveedor no puede ser negativo.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de creación =====================
                            _logger?.LogInformation("CrearPlato: Iniciando creación de plato", new
                            {
                                Codigo = dto.Codigo,
                                PlannutricionalId = dto.Plannutricional_id,
                                Username = username
                            });

                            // Validar plan nutricional
                            var planNutricional = ctx.sl_plannutricional
                                .FirstOrDefault(p => p.id == dto.Plannutricional_id.Value && !p.deletemark);
                            if (planNutricional == null)
                            {
                                _logger?.LogWarning("CrearPlato: Plan nutricional no encontrado", new
                                {
                                    PlanNutricionalId = dto.Plannutricional_id.Value
                                });
                                throw new Exception("El plan nutricional seleccionado no existe.");
                            }

                            // Validación de código único dentro de la transacción
                            var existeCodigo = ctx.sl_plato.Any(p =>
                                p.codigo == dto.Codigo.Trim() && !p.deletemark);
                            if (existeCodigo)
                            {
                                _logger?.LogWarning("CrearPlato: Intento de crear plato con código duplicado", new
                                {
                                    Codigo = dto.Codigo
                                });
                                throw new Exception("Ya existe un plato con ese código.");
                            }

                            // Truncar campos según StringLength del modelo (usando Trim primero)
                            var codigoTruncado = dto.Codigo.Trim();
                            if (codigoTruncado.Length > 150)
                                codigoTruncado = codigoTruncado.Substring(0, 150);

                            var descripcionTruncada = dto.Descripcion.Trim();
                            if (descripcionTruncada.Length > 150)
                                descripcionTruncada = descripcionTruncada.Substring(0, 150);

                            var presentacionTruncada = !string.IsNullOrEmpty(dto.Presentacion)
                                ? dto.Presentacion.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(presentacionTruncada) && presentacionTruncada.Length > 500)
                                presentacionTruncada = presentacionTruncada.Substring(0, 500);

                            var fotoTruncada = !string.IsNullOrEmpty(dto.Foto)
                                ? dto.Foto.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(fotoTruncada) && fotoTruncada.Length > 500)
                                fotoTruncada = fotoTruncada.Substring(0, 500);

                            var entity = new sl_plato
                            {
                                codigo = codigoTruncado,
                                descripcion = descripcionTruncada,
                                costo = dto.Costo,
                                costo_proveedor = dto.CostoProveedor,
                                plannutricional_id = dto.Plannutricional_id.Value,
                                presentacion = presentacionTruncada,
                                ingredientes = dto.Ingredientes,
                                foto = fotoTruncada,
                                deletemark = false,
                                createdate = DateTime.Now,
                                createuser = username
                            };

                            ctx.sl_plato.Add(entity);

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                                var resultado = new PlatoDetalleDto
                                {
                                    Id = entity.id,
                                    Codigo = entity.codigo,
                                    Descripcion = entity.descripcion,
                                    Ingredientes = entity.ingredientes,
                                    Presentacion = entity.presentacion,
                                    Plannutricional_id = entity.plannutricional_id,
                                    Plannutricional_nombre = planNutricional.nombre,
                                    Costo = entity.costo,
                                    CostoProveedor = entity.costo_proveedor,
                                    Foto = entity.foto
                                };

                                // ===================== LOGGING: Creación exitosa =====================
                                _logger?.LogInformation("CrearPlato: Plato creado exitosamente", new
                                {
                                    PlatoId = resultado.Id,
                                    Codigo = resultado.Codigo
                                });

                                return resultado;
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("CrearPlato: Error de validación al guardar plato", ex, new
                                {
                                    Codigo = dto.Codigo,
                                    PlannutricionalId = dto.Plannutricional_id,
                                    ValidationErrors = HandleValidationException(ex, "crear").Message
                                });
                                throw HandleValidationException(ex, "crear");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("CrearPlato: Deadlock detectado, reintentando...", new
                                {
                                    Codigo = dto.Codigo
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("El plan nutricional") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("CrearPlato: Error al crear plato dentro de transacción", ex, new
                            {
                                Codigo = dto?.Codigo,
                                PlannutricionalId = dto?.Plannutricional_id,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("El plan nutricional") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("CrearPlato: Error al crear plato", ex, new
                {
                    Codigo = dto?.Codigo,
                    PlannutricionalId = dto?.Plannutricional_id,
                    Username = username
                });
                throw;
            }
        }



        // =========================================================
        // Actualizar plato
        // =========================================================

        public void ActualizarPlato(PlatoUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new ArgumentNullException(nameof(dto), "El DTO de actualización no puede ser nulo.");

            if (dto.Id <= 0)
                throw new Exception("El ID del plato debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(dto.Codigo))
                throw new Exception("El código es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Descripcion))
                throw new Exception("La descripción es obligatoria.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            if (!dto.Plannutricional_id.HasValue || dto.Plannutricional_id.Value <= 0)
                throw new Exception("Debe seleccionar un plan nutricional.");

            if (dto.Costo < 0)
                throw new Exception("El costo no puede ser negativo.");

            if (dto.CostoProveedor < 0)
                throw new Exception("El costo de proveedor no puede ser negativo.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de actualización =====================
                            _logger?.LogInformation("ActualizarPlato: Iniciando actualización de plato", new
                            {
                                PlatoId = dto.Id,
                                Codigo = dto.Codigo,
                                PlannutricionalId = dto.Plannutricional_id,
                                Username = username
                            });

                            var entity = ctx.sl_plato.FirstOrDefault(p => p.id == dto.Id && !p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActualizarPlato: Plato no encontrado", new { PlatoId = dto.Id });
                                throw new Exception("Plato no encontrado.");
                            }

                            // Validar plan nutricional
                            var planNutricional = ctx.sl_plannutricional
                                .FirstOrDefault(p => p.id == dto.Plannutricional_id.Value && !p.deletemark);
                            if (planNutricional == null)
                            {
                                _logger?.LogWarning("ActualizarPlato: Plan nutricional no encontrado", new
                                {
                                    PlanNutricionalId = dto.Plannutricional_id.Value
                                });
                                throw new Exception("El plan nutricional seleccionado no existe.");
                            }

                            // Validación de código único (excluyendo la misma) dentro de la transacción
                            var existeCodigo = ctx.sl_plato.Any(p =>
                                p.id != dto.Id &&
                                p.codigo == dto.Codigo.Trim() &&
                                !p.deletemark);
                            if (existeCodigo)
                            {
                                _logger?.LogWarning("ActualizarPlato: Intento de actualizar con código duplicado", new
                                {
                                    PlatoId = dto.Id,
                                    Codigo = dto.Codigo
                                });
                                throw new Exception("Ya existe otro plato con ese código.");
                            }

                            // Truncar campos según StringLength del modelo (usando Trim primero)
                            var codigoTruncado = dto.Codigo.Trim();
                            if (codigoTruncado.Length > 150)
                                codigoTruncado = codigoTruncado.Substring(0, 150);

                            var descripcionTruncada = dto.Descripcion.Trim();
                            if (descripcionTruncada.Length > 150)
                                descripcionTruncada = descripcionTruncada.Substring(0, 150);

                            var presentacionTruncada = !string.IsNullOrEmpty(dto.Presentacion)
                                ? dto.Presentacion.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(presentacionTruncada) && presentacionTruncada.Length > 500)
                                presentacionTruncada = presentacionTruncada.Substring(0, 500);

                            var fotoTruncada = !string.IsNullOrEmpty(dto.Foto)
                                ? dto.Foto.Trim()
                                : null;
                            if (!string.IsNullOrEmpty(fotoTruncada) && fotoTruncada.Length > 500)
                                fotoTruncada = fotoTruncada.Substring(0, 500);

                            entity.codigo = codigoTruncado;
                            entity.descripcion = descripcionTruncada;
                            entity.costo = dto.Costo;
                            entity.costo_proveedor = dto.CostoProveedor;
                            entity.plannutricional_id = dto.Plannutricional_id.Value;
                            entity.presentacion = presentacionTruncada;
                            entity.ingredientes = dto.Ingredientes;
                            entity.foto = fotoTruncada;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Actualización exitosa =====================
                                _logger?.LogInformation("ActualizarPlato: Plato actualizado exitosamente", new
                                {
                                    PlatoId = entity.id,
                                    Codigo = entity.codigo
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActualizarPlato: Error de validación al guardar plato", ex, new
                                {
                                    PlatoId = dto.Id,
                                    Codigo = dto.Codigo,
                                    ValidationErrors = HandleValidationException(ex, "actualizar").Message
                                });
                                throw HandleValidationException(ex, "actualizar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("ActualizarPlato: Deadlock detectado, reintentando...", new
                                {
                                    PlatoId = dto.Id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plato no encontrado") || ex.Message.Contains("Ya existe") || ex.Message.Contains("El plan nutricional") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("ActualizarPlato: Error al actualizar plato dentro de transacción", ex, new
                            {
                                PlatoId = dto?.Id,
                                Codigo = dto?.Codigo,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plato no encontrado") || ex.Message.Contains("Ya existe") || ex.Message.Contains("El plan nutricional") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ActualizarPlato: Error al actualizar plato", ex, new
                {
                    PlatoId = dto?.Id,
                    Codigo = dto?.Codigo,
                    Username = username
                });
                throw;
            }
        }

        // =========================================================
        // Baja lógica de plato
        // =========================================================
        public void EliminarPlato(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del plato debe ser mayor a 0.");

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
                            _logger?.LogInformation("EliminarPlato: Iniciando eliminación lógica de plato", new
                            {
                                PlatoId = id,
                                Username = username
                            });

                            var entity = ctx.sl_plato.FirstOrDefault(p => p.id == id && !p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("EliminarPlato: Plato no encontrado", new { PlatoId = id });
                                throw new Exception("Plato no encontrado.");
                            }

                            entity.deletemark = true;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Eliminación exitosa =====================
                                _logger?.LogInformation("EliminarPlato: Plato eliminado exitosamente", new
                                {
                                    PlatoId = id
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("EliminarPlato: Error de validación al eliminar plato", ex, new
                                {
                                    PlatoId = id,
                                    ValidationErrors = HandleValidationException(ex, "eliminar").Message
                                });
                                throw HandleValidationException(ex, "eliminar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("EliminarPlato: Deadlock detectado, reintentando...", new
                                {
                                    PlatoId = id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plato no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("EliminarPlato: Error al eliminar plato dentro de transacción", ex, new
                            {
                                PlatoId = id,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plato no encontrado") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("EliminarPlato: Error al eliminar plato", ex, new
                {
                    PlatoId = id,
                    Username = username
                });
                throw;
            }
        }

        // =========================================================
        // Activar (quitar baja lógica)
        // =========================================================
        public void ActivarPlato(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del plato debe ser mayor a 0.");

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
                            _logger?.LogInformation("ActivarPlato: Iniciando activación de plato", new
                            {
                                PlatoId = id,
                                Username = username
                            });

                            var entity = ctx.sl_plato.FirstOrDefault(p => p.id == id && p.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActivarPlato: Plato no encontrado o ya activo", new { PlatoId = id });
                                throw new Exception("Plato no encontrado.");
                            }

                            entity.deletemark = false;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                                transaction.Commit();

                                // ===================== LOGGING: Activación exitosa =====================
                                _logger?.LogInformation("ActivarPlato: Plato activado exitosamente", new
                                {
                                    PlatoId = id
                                });
                            }
                            catch (DbEntityValidationException ex)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActivarPlato: Error de validación al activar plato", ex, new
                                {
                                    PlatoId = id,
                                    ValidationErrors = HandleValidationException(ex, "activar").Message
                                });
                                throw HandleValidationException(ex, "activar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogWarning("ActivarPlato: Deadlock detectado, reintentando...", new
                                {
                                    PlatoId = id
                                });
                                throw new Exception("El sistema está ocupado. Por favor, intente nuevamente.");
                            }
                        }
                        catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plato no encontrado") || ex.Message.Contains("El sistema está ocupado"))))
                        {
                            transaction.Rollback();
                            _logger?.LogError("ActivarPlato: Error al activar plato dentro de transacción", ex, new
                            {
                                PlatoId = id,
                                ExceptionType = ex.GetType().Name
                            });
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Plato no encontrado") || ex.Message.Contains("El sistema está ocupado") || ex.Message.Contains("obligatorio"))))
            {
                _logger?.LogError("ActivarPlato: Error al activar plato", ex, new
                {
                    PlatoId = id,
                    Username = username
                });
                throw;
            }
        }

        // =========================================================
        // Buscador rápido (para autocomplete / combos)
        // =========================================================
        public IEnumerable<PlatoListadoDto> BuscarPlatos(string texto, bool soloActivos = true, int maxResultados = 20)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (string.IsNullOrWhiteSpace(texto))
                return new List<PlatoListadoDto>();

            // Validar longitud de término de búsqueda
            if (texto.Length > 200)
                throw new Exception("El término de búsqueda no puede exceder 200 caracteres.");

            if (maxResultados <= 0 || maxResultados > 100)
                maxResultados = 20;

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("BuscarPlatos: Iniciando búsqueda de platos", new
                    {
                        Texto = texto,
                        SoloActivos = soloActivos,
                        MaxResultados = maxResultados
                    });

                    var query =
                        from p in ctx.sl_plato
                        join pn in ctx.sl_plannutricional
                            on p.plannutricional_id equals pn.id into pnJoin
                        from pn in pnJoin.DefaultIfEmpty()
                        select new PlatoListadoDto
                        {
                            Id = p.id,
                            Codigo = p.codigo,
                            Descripcion = p.descripcion,
                            Costo = p.costo,
                            CostoProveedor = p.costo_proveedor,
                            Plannutricional_id = p.plannutricional_id,
                            Plannutricional_nombre = pn != null ? pn.nombre : null,
                            Foto = p.foto,
                            Deletemark = p.deletemark
                        };

                    if (soloActivos)
                        query = query.Where(x => !x.Deletemark);

                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        var s = texto.Trim().ToLower();
                        query = query.Where(x =>
                            (x.Codigo ?? "").ToLower().Contains(s) ||
                            (x.Descripcion ?? "").ToLower().Contains(s) ||
                            (x.Plannutricional_nombre ?? "").ToLower().Contains(s)
                        );
                    }

                    var resultados = query
                        .OrderBy(x => x.Codigo)
                        .ThenBy(x => x.Descripcion)
                        .Take(maxResultados)
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("BuscarPlatos: Búsqueda completada exitosamente", new
                    {
                        Texto = texto,
                        ResultadosEncontrados = resultados.Count
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("BuscarPlatos: Error al buscar platos", ex, new
                {
                    Texto = texto,
                    SoloActivos = soloActivos,
                    MaxResultados = maxResultados
                });
                throw;
            }
        }

        // =========================================================
        // Buscador simple: devuelve solo código y nombre
        // Busca solo a partir de la cuarta letra
        // =========================================================
        public IEnumerable<PlatoBusquedaSimpleDto> BuscarPlatosSimple(string texto, bool soloActivos = true, int maxResultados = 20)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (maxResultados <= 0 || maxResultados > 100)
                maxResultados = 20;

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("BuscarPlatosSimple: Iniciando búsqueda simple de platos", new
                    {
                        Texto = texto,
                        SoloActivos = soloActivos,
                        MaxResultados = maxResultados
                    });

                    var query = ctx.sl_plato.AsQueryable();

                    if (soloActivos)
                        query = query.Where(p => !p.deletemark);

                    // Solo buscar si el texto tiene al menos 4 caracteres
                    if (!string.IsNullOrWhiteSpace(texto))
                    {
                        var s = texto.Trim();
                        
                        // Si tiene menos de 4 caracteres, devolver lista vacía
                        if (s.Length < 4)
                        {
                            return new List<PlatoBusquedaSimpleDto>();
                        }
                        
                        var sLower = s.ToLower();
                        query = query.Where(p =>
                            (p.codigo ?? "").ToLower().Contains(sLower) ||
                            (p.descripcion ?? "").ToLower().Contains(sLower)
                        );
                    }
                    else
                    {
                        // Si no hay texto, devolver lista vacía
                        return new List<PlatoBusquedaSimpleDto>();
                    }

                    var resultados = query
                        .OrderBy(p => p.codigo)
                        .ThenBy(p => p.descripcion)
                        .Take(maxResultados)
                        .Select(p => new PlatoBusquedaSimpleDto
                        {
                            Codigo = p.codigo,
                            Nombre = p.descripcion
                        })
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("BuscarPlatosSimple: Búsqueda completada exitosamente", new
                    {
                        Texto = texto,
                        ResultadosEncontrados = resultados.Count
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("BuscarPlatosSimple: Error al buscar platos", ex, new
                {
                    Texto = texto,
                    SoloActivos = soloActivos,
                    MaxResultados = maxResultados
                });
                throw;
            }
        }

        // =========================================================
        // Obtener platos por plan nutricional
        // =========================================================
        public IEnumerable<PlatoListadoDto> ObtenerPlatosPorPlanNutricional(int planNutricionalId, bool soloActivos = true)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (planNutricionalId <= 0)
                throw new Exception("El ID del plan nutricional debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("ObtenerPlatosPorPlanNutricional: Iniciando búsqueda de platos por plan nutricional", new
                    {
                        PlanNutricionalId = planNutricionalId,
                        SoloActivos = soloActivos
                    });

                    var query =
                        from p in ctx.sl_plato
                        join pn in ctx.sl_plannutricional
                            on p.plannutricional_id equals pn.id into pnJoin
                        from pn in pnJoin.DefaultIfEmpty()
                        where p.plannutricional_id == planNutricionalId
                        select new PlatoListadoDto
                        {
                            Id = p.id,
                            Codigo = p.codigo,
                            Descripcion = p.descripcion,
                            Costo = p.costo,
                            CostoProveedor = p.costo_proveedor,
                            Plannutricional_id = p.plannutricional_id,
                            Plannutricional_nombre = pn != null ? pn.nombre : null,
                            Ingredientes = p.ingredientes,
                            Foto = p.foto,
                            Deletemark = p.deletemark
                        };

                    if (soloActivos)
                        query = query.Where(x => !x.Deletemark);

                    var resultados = query
                        .OrderBy(x => x.Codigo)
                        .ThenBy(x => x.Descripcion)
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("ObtenerPlatosPorPlanNutricional: Búsqueda completada exitosamente", new
                    {
                        PlanNutricionalId = planNutricionalId,
                        ResultadosEncontrados = resultados.Count
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerPlatosPorPlanNutricional: Error al obtener platos por plan nutricional", ex, new
                {
                    PlanNutricionalId = planNutricionalId,
                    SoloActivos = soloActivos
                });
                throw;
            }
        }

        // ================= IMPRESIÓN =================
        public List<PlatoImpresionDto> ObtenerDatosImpresion(PlatoImpresionRequestDto request)
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
                        PlanNutricionalId = request.PlanNutricionalId,
                        IncluirCodigo = request.IncluirCodigo,
                        IncluirDescripcion = request.IncluirDescripcion,
                        IncluirPlanNutricional = request.IncluirPlanNutricional,
                        IncluirImporte = request.IncluirImporte,
                        IncluirPresentacion = request.IncluirPresentacion,
                        IncluirIngredientes = request.IncluirIngredientes,
                        IncluirEstado = request.IncluirEstado
                    });

                    var query = ctx.sl_plato
                        .AsNoTracking()
                        .AsQueryable();

                    // Aplicar filtro de estado
                    if (request.Estado == "Activo")
                        query = query.Where(p => !p.deletemark);
                    else if (request.Estado == "Inactivo")
                        query = query.Where(p => p.deletemark);
                    // Si es "Todos" o null, no se filtra

                    // Aplicar filtro de plan nutricional
                    if (request.PlanNutricionalId.HasValue && request.PlanNutricionalId.Value > 0)
                        query = query.Where(p => p.plannutricional_id == request.PlanNutricionalId.Value);

                    // JOIN con plan nutricional
                    var queryConJoins = (from p in query
                                         join pn in ctx.sl_plannutricional on p.plannutricional_id equals pn.id into pnJoin
                                         from pn in pnJoin.DefaultIfEmpty()
                                         orderby p.codigo, p.descripcion
                                         select new
                                         {
                                             p,
                                             pn
                                         })
                                         .ToList();

                    // Construir DTOs solo con las columnas seleccionadas
                    var result = queryConJoins.Select(x => new PlatoImpresionDto
                    {
                        Codigo = request.IncluirCodigo ? x.p.codigo : null,
                        Descripcion = request.IncluirDescripcion ? x.p.descripcion : null,
                        PlanNutricional = request.IncluirPlanNutricional ? (x.pn != null ? x.pn.nombre : null) : null,
                        Importe = request.IncluirImporte ? (decimal?)x.p.costo : null,
                        Presentacion = request.IncluirPresentacion ? x.p.presentacion : null,
                        Ingredientes = request.IncluirIngredientes ? x.p.ingredientes : null,
                        Estado = request.IncluirEstado ? (x.p.deletemark ? "Inactivo" : "Activo") : null
                    }).ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    var totalItems = result.Count;
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = totalItems,
                        Estado = request.Estado,
                        PlanNutricionalId = request.PlanNutricionalId
                    });

                    return result;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex, new
                {
                    Estado = request?.Estado,
                    PlanNutricionalId = request?.PlanNutricionalId,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }

        // =========================================================
        // Helper para guardar imagen del plato (base64 -> archivo)
        // =========================================================
        private string GuardarImagenPlato(string base64, string codigo)
        {
            try
            {
                // Si viene con "data:image/xxx;base64," recorto la cabecera
                var comaIndex = base64.IndexOf(",");
                if (comaIndex >= 0)
                    base64 = base64.Substring(comaIndex + 1);

                var bytes = Convert.FromBase64String(base64);

                var folder = HostingEnvironment.MapPath("~/uploads/platos");
                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                var fileName = $"{codigo}_{DateTime.Now:yyyyMMddHHmmss}.jpg";
                var fullPath = Path.Combine(folder, fileName);

                File.WriteAllBytes(fullPath, bytes);

                // Ruta relativa para guardar en BD
                return $"/uploads/platos/{fileName}";
            }
            catch
            {
                // Si algo falla, devuelvo null; el controller/servicio decide si lo toma o no
                return null;
            }
        }
    }
}
