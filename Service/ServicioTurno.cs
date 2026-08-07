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
using smartlunch_api.Models.DTOs;
using static smartlunch_api.Controllers.BaseApiController;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioTurno
    {
        PagedResultDto<TurnoListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado);
        TurnoDetalleDto ObtenerPorId(int id);
        TurnoDetalleDto CrearTurno(TurnoCreateDto dto, string username);
        void ActualizarTurno(TurnoUpdateDto dto, string username);
        void EliminarTurno(int id, string username);
        void ActivarTurno(int id, string username);
        IEnumerable<TurnoComboDto> ObtenerActivosParaCombo();
        IEnumerable<TurnoUpdateDto> ObtenerHorarioCombo();
        List<TurnoImpresionDto> ObtenerDatosImpresion(TurnoImpresionRequestDto request);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioTurno : IServicioTurno
    {
        private readonly ILoggerService _logger;

        public ServicioTurno(ILoggerService logger = null)
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
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el turno:");
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

        // ========== Lista paginada + buscador + activo/inactivo ==========
        public PagedResultDto<TurnoListadoDto> ObtenerLista(
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
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de turnos", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        Activo = activo
                    });

                var query =
                    ctx.sl_turno
                        .Select(t => new TurnoListadoDto
                        {
                            Id = t.id,
                            Nombre = t.nombre,
                            HoraDesde = t.horadesde,
                            HoraHasta = t.horahasta,
                            Activo = !t.deletemark
                        });

                // Filtro activo / inactivo
                query = activo
                    ? query.Where(x => x.Activo)
                    : query.Where(x => !x.Activo);

                // Buscador por nombre
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(x => (x.Nombre ?? "").ToLower().Contains(s));
                }

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderBy(x => x.Nombre)
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

                    return new PagedResultDto<TurnoListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de turnos", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    HasSearch = !string.IsNullOrWhiteSpace(search),
                    Estado = activo
                });
                throw;
            }
        }

        // ========== Detalle por Id ==========
        public TurnoDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del turno debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_turno
                        .Where(t => t.id == id && !t.deletemark)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Turno no encontrado", new { TurnoId = id });
                        throw new Exception("Turno no encontrado.");
                    }

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = new TurnoDetalleDto
                    {
                        Id = entity.id,
                        Nombre = entity.nombre,
                        HoraDesde = entity.horadesde,
                        HoraHasta = entity.horahasta,
                        Activo = !entity.deletemark,
                        CreateDate = entity.createdate,
                        CreateUser = entity.createuser,
                        UpdateDate = entity.updatedate,
                        UpdateUser = entity.updateuser
                    };

                    _logger?.LogInformation("ObtenerPorId: Turno obtenido exitosamente", new
                    {
                        TurnoId = id,
                        Nombre = resultado.Nombre
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Turno no encontrado")))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener turno", ex, new { TurnoId = id });
                throw;
            }
        }

        // ========== Crear turno ==========
       
        public TurnoDetalleDto CrearTurno(TurnoCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new Exception("Datos inválidos.");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre es obligatorio.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            // 1) Validar rango horario coherente
            if (!dto.HoraDesde.HasValue || !dto.HoraHasta.HasValue)
                throw new Exception("Las horas desde y hasta son obligatorias.");

            if (dto.HoraDesde.Value >= dto.HoraHasta.Value)
                throw new Exception("La hora desde debe ser menor que la hora hasta.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de creación =====================
                            _logger?.LogInformation("CrearTurno: Iniciando creación de turno", new
                            {
                                Nombre = dto.Nombre,
                                HoraDesde = dto.HoraDesde,
                                HoraHasta = dto.HoraHasta,
                                Username = username
                            });

                            // 2) Nombre único (dentro de transacción para evitar race conditions)
                            var existe = ctx.sl_turno.Any(t =>
                                t.nombre == dto.Nombre.Trim() && !t.deletemark);

                            if (existe)
                            {
                                _logger?.LogWarning("CrearTurno: Ya existe un turno con ese nombre", new { Nombre = dto.Nombre });
                                throw new Exception("Ya existe un turno con ese nombre.");
                            }

                            // 3) Validar que el rango NO se solape con otros turnos
                            //    Permite turnos pegados (ej: 08:00-12:00 y 12:00-16:00)
                            var finDelDia = new TimeSpan(24, 0, 0);
                            var otrosTurnos = ctx.sl_turno
                                .Where(t => !t.deletemark)
                                .ToList(); // Materializar en memoria para validación compleja

                            var haySolapado = otrosTurnos.Any(t =>
                            {
                                var desdeExistente = t.horadesde ?? TimeSpan.Zero;
                                var hastaExistente = t.horahasta == TimeSpan.Zero ? finDelDia : (t.horahasta ?? finDelDia);
                                return dto.HoraDesde.Value < hastaExistente && dto.HoraHasta.Value > desdeExistente;
                            });

                            if (haySolapado)
                            {
                                _logger?.LogWarning("CrearTurno: El rango horario se superpone con otro turno", new
                                {
                                    HoraDesde = dto.HoraDesde,
                                    HoraHasta = dto.HoraHasta
                                });
                                throw new Exception("El rango horario se superpone con otro turno existente.");
                            }

                            // 4) Crear turno
                            // Truncar nombre según longitud máxima (50 caracteres)
                            var nombreTruncado = dto.Nombre.Trim();
                            if (nombreTruncado.Length > 50)
                                nombreTruncado = nombreTruncado.Substring(0, 50);
                            
                            var entity = new sl_turno
                            {
                                nombre = nombreTruncado,
                                horadesde = dto.HoraDesde,
                                horahasta = dto.HoraHasta,
                                deletemark = false,
                                createdate = DateTime.Now,
                                createuser = username
                            };

                            ctx.sl_turno.Add(entity);
                            
                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "crear");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("CrearTurno: Deadlock detectado", ex, new { Nombre = dto.Nombre });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("CrearTurno: Error al guardar turno", ex, new { Nombre = dto.Nombre });
                                throw;
                            }

                            transaction.Commit();

                            // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                            var resultado = new TurnoDetalleDto
                            {
                                Id = entity.id,
                                Nombre = entity.nombre,
                                HoraDesde = entity.horadesde,
                                HoraHasta = entity.horahasta,
                                Activo = !entity.deletemark,
                                CreateDate = entity.createdate,
                                CreateUser = entity.createuser,
                                UpdateDate = entity.updatedate,
                                UpdateUser = entity.updateuser
                            };

                            _logger?.LogInformation("CrearTurno: Turno creado exitosamente", new
                            {
                                TurnoId = entity.id,
                                Nombre = resultado.Nombre
                            });

                            return resultado;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("se superpone") || ex.Message.Contains("obligatorio") || ex.Message.Contains("menor que"))))
            {
                _logger?.LogError("CrearTurno: Error al crear turno", ex, new
                {
                    Nombre = dto?.Nombre,
                    HoraDesde = dto?.HoraDesde,
                    HoraHasta = dto?.HoraHasta
                });
                throw;
            }
        }


        // ========== Actualizar turno ==========
        public void ActualizarTurno(TurnoUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre es obligatorio.");

            if (!dto.HoraDesde.HasValue || !dto.HoraHasta.HasValue)
                throw new Exception("Las horas desde y hasta son obligatorias.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            ctx.Configuration.LazyLoadingEnabled = false;

                            // ===================== LOGGING: Inicio de actualización =====================
                            _logger?.LogInformation("ActualizarTurno: Iniciando actualización de turno", new
                            {
                                TurnoId = dto.Id,
                                Nombre = dto.Nombre,
                                HoraDesde = dto.HoraDesde,
                                HoraHasta = dto.HoraHasta,
                                Username = username
                            });

                            var entity = ctx.sl_turno.FirstOrDefault(t => t.id == dto.Id && !t.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActualizarTurno: Turno no encontrado", new { TurnoId = dto.Id });
                                throw new Exception("Turno no encontrado.");
                            }

                            // ==========================
                            // Normalización de horas
                            // ==========================
                            var horaDesde = dto.HoraDesde.Value;
                            // 24:00 como límite superior para validaciones (pero se guarda 00:00)
                            var finDelDia = new TimeSpan(24, 0, 0);
                            var horaHasta = dto.HoraHasta.Value == TimeSpan.Zero
                                ? finDelDia
                                : dto.HoraHasta.Value;

                            // 1) Rango coherente
                            if (horaDesde >= horaHasta)
                                throw new Exception("La hora desde debe ser menor que la hora hasta.");

                            // 2) Nombre único (dentro de transacción para evitar race conditions)
                            var existeNombre = ctx.sl_turno.Any(t =>
                                t.id != dto.Id &&
                                t.nombre == dto.Nombre.Trim() &&
                                !t.deletemark);

                            if (existeNombre)
                            {
                                _logger?.LogWarning("ActualizarTurno: Ya existe otro turno con ese nombre", new
                                {
                                    TurnoId = dto.Id,
                                    Nombre = dto.Nombre
                                });
                                throw new Exception("Ya existe otro turno con ese nombre.");
                            }

                            // 3) Validar solapamiento con otros turnos (LINQ to Objects)
                            var otrosTurnos = ctx.sl_turno
                                .Where(t => !t.deletemark && t.id != dto.Id)
                                .ToList();   // A partir de acá es en memoria

                            bool haySolapado = otrosTurnos.Any(t =>
                            {
                                var desdeExistente = t.horadesde ?? TimeSpan.Zero;
                                var hastaExistente = t.horahasta == TimeSpan.Zero
                                    ? finDelDia
                                    : (t.horahasta ?? finDelDia);

                                // Si querés permitir que un turno termine justo cuando otro empieza,
                                // usá < y > en vez de <= y >=
                                return horaDesde < hastaExistente &&
                                       horaHasta > desdeExistente;
                            });

                            if (haySolapado)
                            {
                                _logger?.LogWarning("ActualizarTurno: El rango horario se superpone con otro turno", new
                                {
                                    TurnoId = dto.Id,
                                    HoraDesde = dto.HoraDesde,
                                    HoraHasta = dto.HoraHasta
                                });
                                throw new Exception("El rango horario se superpone con otro turno existente.");
                            }

                            // 4) Actualizar entidad
                            // Truncar nombre según longitud máxima (50 caracteres)
                            var nombreTruncado = dto.Nombre.Trim();
                            if (nombreTruncado.Length > 50)
                                nombreTruncado = nombreTruncado.Substring(0, 50);
                            
                            entity.nombre = nombreTruncado;
                            entity.horadesde = dto.HoraDesde;
                            entity.horahasta = dto.HoraHasta;   // se guarda 00:00 si viene 00:00
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
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActualizarTurno: Deadlock detectado", ex, new { TurnoId = dto.Id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("ActualizarTurno: Error al guardar turno", ex, new { TurnoId = dto.Id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("ActualizarTurno: Turno actualizado exitosamente", new
                            {
                                TurnoId = dto.Id,
                                Nombre = entity.nombre
                            });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("se superpone") || ex.Message.Contains("obligatorio") || ex.Message.Contains("menor que") || ex.Message.Contains("no encontrado"))))
            {
                _logger?.LogError("ActualizarTurno: Error al actualizar turno", ex, new
                {
                    TurnoId = dto?.Id,
                    Nombre = dto?.Nombre
                });
                throw;
            }
        }



        // ========== Baja lógica ==========
        public void EliminarTurno(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del turno debe ser mayor a 0.");

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
                            _logger?.LogInformation("EliminarTurno: Iniciando eliminación lógica de turno", new
                            {
                                TurnoId = id,
                                Username = username
                            });

                            var entity = ctx.sl_turno.FirstOrDefault(t => t.id == id && !t.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("EliminarTurno: Turno no encontrado", new { TurnoId = id });
                                throw new Exception("Turno no encontrado.");
                            }

                            entity.deletemark = true;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("EliminarTurno: Deadlock detectado", ex, new { TurnoId = id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "eliminar");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("EliminarTurno: Error al guardar eliminación", ex, new { TurnoId = id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("EliminarTurno: Turno eliminado exitosamente", new
                            {
                                TurnoId = id,
                                Nombre = entity.nombre
                            });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("no encontrado")))
            {
                _logger?.LogError("EliminarTurno: Error al eliminar turno", ex, new { TurnoId = id });
                throw;
            }
        }

        // ========== Activar (quitar baja lógica) ==========
        public void ActivarTurno(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del turno debe ser mayor a 0.");

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
                            _logger?.LogInformation("ActivarTurno: Iniciando activación de turno", new
                            {
                                TurnoId = id,
                                Username = username
                            });

                            var entity = ctx.sl_turno.FirstOrDefault(t => t.id == id && t.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActivarTurno: Turno no encontrado", new { TurnoId = id });
                                throw new Exception("Turno no encontrado.");
                            }

                            entity.deletemark = false;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = username;

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActivarTurno: Deadlock detectado", ex, new { TurnoId = id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "activar");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("ActivarTurno: Error al guardar activación", ex, new { TurnoId = id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("ActivarTurno: Turno activado exitosamente", new
                            {
                                TurnoId = id,
                                Nombre = entity.nombre
                            });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("no encontrado")))
            {
                _logger?.LogError("ActivarTurno: Error al activar turno", ex, new { TurnoId = id });
                throw;
            }
        }

        // ========== Lista simple para combos ==========
        public IEnumerable<TurnoComboDto> ObtenerActivosParaCombo()
        {
            try
            {
                var ahora = DateTime.Now.TimeOfDay;

                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    _logger?.LogInformation("ObtenerActivosParaCombo: Iniciando obtención de turnos activos para combo");

                // 1) Traigo todos los turnos activos
                var turnosDb = ctx.sl_turno
                    .Where(t => !t.deletemark)
                    .ToList(); // desde acá LINQ a objetos

                // 2) Normalizo horahasta (00:00 => 24:00)
                var turnosNorm = turnosDb
                    .Select(t => new
                    {
                        Turno = t,
                        Desde = t.horadesde,
                        Hasta = (t.horahasta == TimeSpan.Zero
                                    ? TimeSpan.FromHours(24)
                                    : t.horahasta)
                    })
                    .ToList();

                // 3) Lista de turnos no finalizados -> PROYECTO A TurnoComboDto
                var turnosCombo = turnosNorm
                    .Where(x => x.Hasta > ahora)
                    .OrderBy(x => x.Desde)
                    .Select(x => new TurnoComboDto
                    {
                        Id = x.Turno.id,
                        Nombre = x.Turno.nombre
                        // si tu DTO combo tiene más campos, los llenás acá
                    })
                    .ToList();   // 👈 List<TurnoComboDto>

                    // NO HACER cast raro, List<TurnoComboDto> ya es IEnumerable<TurnoComboDto>
                    _logger?.LogInformation("ObtenerActivosParaCombo: Turnos obtenidos exitosamente", new
                    {
                        TotalTurnos = turnosCombo.Count
                    });

                    return turnosCombo;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerActivosParaCombo: Error al obtener turnos para combo", ex);
                throw;
            }
        }

        public IEnumerable<TurnoUpdateDto> ObtenerHorarioCombo()
        {
            try
            {
                var ahora = DateTime.Now.TimeOfDay;

                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    _logger?.LogInformation("ObtenerHorarioCombo: Iniciando obtención de horarios para combo");

                // 1) Traigo todos los turnos activos
                var turnosDb = ctx.sl_turno
                    .Where(t => !t.deletemark)
                    .ToList(); // desde acá LINQ a objetos

                // 2) Normalizo horahasta (00:00 => 24:00)
                var turnosNorm = turnosDb
                    .Select(t => new
                    {
                        Turno = t,
                        Desde = t.horadesde,
                        Hasta = (t.horahasta == TimeSpan.Zero
                                    ? TimeSpan.FromHours(24)
                                    : t.horahasta)
                    })
                    .ToList();

                // 3) Lista de turnos no finalizados -> PROYECTO A TurnoComboDto
                var turnosCombo = turnosNorm
                    .Where(x => x.Hasta > ahora)
                    .OrderBy(x => x.Desde)
                    .Select(x => new TurnoUpdateDto
                    {
                        Id = x.Turno.id,
                        Nombre = x.Turno.nombre,
                        HoraDesde = x.Turno.horadesde,
                        HoraHasta = x.Turno.horahasta,
                        // si tu DTO combo tiene más campos, los llenás acá
                    })
                    .ToList();   // 👈 List<TurnoComboDto>

                    // NO HACER cast raro, List<TurnoComboDto> ya es IEnumerable<TurnoComboDto>
                    _logger?.LogInformation("ObtenerHorarioCombo: Horarios obtenidos exitosamente", new
                    {
                        TotalHorarios = turnosCombo.Count
                    });

                    return turnosCombo;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerHorarioCombo: Error al obtener horarios para combo", ex);
                throw;
            }
        }

        // ========== Obtener datos para impresión ==========
        public List<TurnoImpresionDto> ObtenerDatosImpresion(TurnoImpresionRequestDto request)
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    _logger?.LogInformation("ObtenerDatosImpresion: Iniciando obtención de datos para impresión", new
                    {
                        IncluirNombre = request?.IncluirNombre ?? false,
                        IncluirHoraDesde = request?.IncluirHoraDesde ?? false,
                        IncluirHoraHasta = request?.IncluirHoraHasta ?? false,
                        IncluirEstado = request?.IncluirEstado ?? false,
                        Estado = request?.Estado
                    });

                    var query = ctx.sl_turno.AsQueryable();

                    // Filtrar por estado
                    if (!string.IsNullOrWhiteSpace(request?.Estado) && request.Estado != "Todos")
                    {
                        if (request.Estado == "Activo")
                            query = query.Where(t => !t.deletemark);
                        else if (request.Estado == "Inactivo")
                            query = query.Where(t => t.deletemark);
                    }

                    var turnos = query
                        .OrderBy(t => t.nombre)
                        .ToList();

                    var resultados = new List<TurnoImpresionDto>();

                    foreach (var turno in turnos)
                    {
                        var dto = new TurnoImpresionDto();

                        if (request?.IncluirNombre == true)
                            dto.Nombre = turno.nombre;

                        if (request?.IncluirHoraDesde == true && turno.horadesde.HasValue)
                            dto.HoraDesde = turno.horadesde.Value.ToString(@"hh\:mm");

                        if (request?.IncluirHoraHasta == true && turno.horahasta.HasValue)
                        {
                            if (turno.horahasta.Value == TimeSpan.Zero)
                                dto.HoraHasta = "24:00";
                            else
                                dto.HoraHasta = turno.horahasta.Value.ToString(@"hh\:mm");
                        }

                        if (request?.IncluirEstado == true)
                            dto.Estado = !turno.deletemark ? "Activo" : "Inactivo";

                        resultados.Add(dto);
                    }

                    var totalItems = resultados.Count;
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = totalItems
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex);
                throw;
            }
        }

    }
}
