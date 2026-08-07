using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using smartlunch_api.App_Start;
using smartlunch_api.Dtos;
using smartlunch_api.Models;
using smartlunch_api.Models.DTOs;
using System.Configuration;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioLogin
    {
        LoginAuthResultDto Autenticar(LoginRequestDto dto);
        /// <summary>Valida el refresh token y devuelve nuevo JWT y nuevo RefreshToken (rotación). Devuelve null si es inválido o expiró.</summary>
        RefreshTokenResponseDto RefreshToken(string refreshToken);
        PagedResultDto<LoginListadoDto> ObtenerLista(int page, int pageSize, string search, bool soloActivos);
        LoginDetalleDto ObtenerPorId(int id);
        LoginDetalleDto CrearLogin(LoginCreateDto dto, string adminUser);
        void ActualizarLogin(LoginUpdateDto dto, string adminUser);
        void EliminarLogin(int id, string adminUser);
        void ActivarLogin(int id, string adminUser);
        void CambiarClave(LoginCambiarClaveDto dto, string adminUser);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioLogin : IServicioLogin
    {
        private readonly string _jwtSecret;
        private readonly string _jwtIssuer;
        private readonly string _jwtAudience;
        private readonly int _jwtRefreshTokenExpirationDays;
        private readonly ILoggerService _logger;

        public ServicioLogin(ILoggerService logger = null)
        {
            _jwtSecret = ConfigurationManager.AppSettings["JwtSecret"];
            _jwtIssuer = ConfigurationManager.AppSettings["JwtIssuer"];
            _jwtAudience = ConfigurationManager.AppSettings["JwtAudience"];
            var refreshDays = ConfigurationManager.AppSettings["JwtRefreshTokenExpirationDays"];
            _jwtRefreshTokenExpirationDays = int.TryParse(refreshDays, out var days) && days > 0 ? days : 7;

            if (string.IsNullOrWhiteSpace(_jwtSecret))
                throw new Exception("Falta la clave JwtSecret en web.config.");

            _logger = logger;
        }

        // ===================== MÉTODOS HELPER =====================

        /// <summary>
        /// Maneja excepciones de validación de Entity Framework y genera mensajes descriptivos
        /// </summary>
        private Exception HandleValidationException(DbEntityValidationException ex, string operacion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el login:");
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
        public PagedResultDto<LoginListadoDto> ObtenerLista(
            int page,
            int pageSize,
            string search,
            bool soloActivos)
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
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de logins", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        SoloActivos = soloActivos
                    });

                var query =
                    from l in ctx.sl_login
                    join u in ctx.sl_usuario on l.usuario_id equals u.id
                    select new LoginListadoDto
                    {
                        Id = l.id,
                        UsuarioId = l.usuario_id,
                        Username = l.username,
                        Activo = l.activo,
                        DeleteMark = l.deletemark,
                        LastLogin = l.last_login,
                        UsuarioNombreCompleto = u.nombre + " " + u.apellido
                    };

                if (soloActivos)
                    query = query.Where(x => x.Activo && !x.DeleteMark);
                else
                    query = query.Where(x => !x.DeleteMark);

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(x =>
                        (x.Username ?? "").ToLower().Contains(s) ||
                        (x.UsuarioNombreCompleto ?? "").ToLower().Contains(s)
                    );
                }

                    var totalItems = query.Count();
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = query
                        .OrderBy(x => x.Username)
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

                    return new PagedResultDto<LoginListadoDto>
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
                _logger?.LogError("ObtenerLista: Error al obtener lista de logins", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    HasSearch = !string.IsNullOrWhiteSpace(search),
                    SoloActivos = soloActivos
                });
                throw;
            }
        }

        // ===================== DETALLE =====================
        public LoginDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del login debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = (from l in ctx.sl_login
                                 join u in ctx.sl_usuario on l.usuario_id equals u.id
                                 where l.id == id && !l.deletemark
                                 select new
                                 {
                                     Login = l,
                                     Usuario = u
                                 }).FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Login no encontrado", new { LoginId = id });
                        throw new Exception("Login no encontrado.");
                    }

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = new LoginDetalleDto
                    {
                        Id = entity.Login.id,
                        UsuarioId = entity.Login.usuario_id,
                        Username = entity.Login.username,
                        Estado = entity.Login.activo,
                        DeleteMark = entity.Login.deletemark,
                        LastLogin = entity.Login.last_login,
                        Createdate = entity.Login.createdate ?? DateTime.Now,
                        Createuser = entity.Login.createuser,
                        Updatedate = entity.Login.updatedate,
                        Updateuser = entity.Login.updateuser,
                        UsuarioNombreCompleto = (entity.Usuario.nombre ?? "") + " " + (entity.Usuario.apellido ?? ""),
                        UsuarioLegajo = entity.Usuario.legajo
                    };

                    _logger?.LogInformation("ObtenerPorId: Login obtenido exitosamente", new
                    {
                        LoginId = id,
                        Username = resultado.Username
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("no encontrado")))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener login", ex, new { LoginId = id });
                throw;
            }
        }
     
        // ===================== CREAR LOGIN =====================
        public LoginDetalleDto CrearLogin(LoginCreateDto dto, string adminUser)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new Exception("Datos inválidos.");

            if (dto.UsuarioId <= 0)
                throw new Exception("UsuarioId obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Username))
                throw new Exception("Username obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Password))
                throw new Exception("Password obligatorio.");

            if (string.IsNullOrWhiteSpace(adminUser))
                throw new Exception("El nombre de usuario administrador es obligatorio.");

            // Validar longitud mínima de password (aunque el DTO lo valida, validamos aquí también)
            if (dto.Password.Length < 6)
                throw new Exception("La contraseña debe tener al menos 6 caracteres.");

            // Validar longitud de username
            if (dto.Username.Length > 50)
                throw new Exception("El username no puede exceder 50 caracteres.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de creación =====================
                            _logger?.LogInformation("CrearLogin: Iniciando creación de login", new
                            {
                                UsuarioId = dto.UsuarioId,
                                Username = dto.Username,
                                AdminUser = adminUser
                            });

                            // Validar que exista el usuario y esté activo
                            var usuario = ctx.sl_usuario.FirstOrDefault(u => u.id == dto.UsuarioId && !u.deletemark);
                            if (usuario == null)
                            {
                                _logger?.LogWarning("CrearLogin: Usuario no encontrado", new { UsuarioId = dto.UsuarioId });
                                throw new Exception("Usuario no encontrado.");
                            }

                            // Username único (dentro de transacción para evitar race conditions)
                            var existeUsername = ctx.sl_login.Any(l =>
                                l.username == dto.Username.Trim() && !l.deletemark);
                            if (existeUsername)
                            {
                                _logger?.LogWarning("CrearLogin: Ya existe ese username", new { Username = dto.Username });
                                throw new Exception("Ya existe ese username.");
                            }

                            // Solo un login por usuario (dentro de transacción)
                            var existeLoginUsuario = ctx.sl_login.Any(l =>
                                l.usuario_id == dto.UsuarioId && !l.deletemark);
                            if (existeLoginUsuario)
                            {
                                _logger?.LogWarning("CrearLogin: El usuario ya tiene un login asociado", new { UsuarioId = dto.UsuarioId });
                                throw new Exception("El usuario ya tiene un login asociado.");
                            }

                            CrearHashPassword(dto.Password, out var salt, out var hash);

                            // Truncar campos según StringLength del modelo
                            var usernameTruncado = dto.Username.Trim();
                            if (usernameTruncado.Length > 50)
                                usernameTruncado = usernameTruncado.Substring(0, 50);

                            var createuserTruncado = adminUser.Trim();
                            if (createuserTruncado.Length > 50)
                                createuserTruncado = createuserTruncado.Substring(0, 50);

                            var entity = new sl_login
                            {
                                usuario_id = dto.UsuarioId,
                                username = usernameTruncado,
                                password_salt = salt,
                                password_hash = hash,
                                password_iteraciones = PasswordUtils.IteracionesActuales,
                                activo = true,
                                deletemark = false,
                                debe_cambiar_clave = false,
                                createdate = DateTime.Now,
                                createuser = createuserTruncado
                            };

                            ctx.sl_login.Add(entity);
                            
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
                                _logger?.LogError("CrearLogin: Deadlock detectado", ex, new { Username = dto.Username });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("CrearLogin: Error al guardar login", ex, new { Username = dto.Username });
                                throw;
                            }

                            transaction.Commit();

                            // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                            var resultado = new LoginDetalleDto
                            {
                                Id = entity.id,
                                UsuarioId = entity.usuario_id,
                                Username = entity.username,
                                Estado = entity.activo,
                                DeleteMark = entity.deletemark,
                                LastLogin = entity.last_login,
                                Createdate = entity.createdate ?? DateTime.Now,
                                Createuser = entity.createuser,
                                Updatedate = entity.updatedate,
                                Updateuser = entity.updateuser,
                                UsuarioNombreCompleto = (usuario.nombre ?? "") + " " + (usuario.apellido ?? ""),
                                UsuarioLegajo = usuario.legajo
                            };

                            _logger?.LogInformation("CrearLogin: Login creado exitosamente", new
                            {
                                LoginId = entity.id,
                                Username = resultado.Username,
                                UsuarioId = resultado.UsuarioId
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
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("obligatorio") || ex.Message.Contains("no encontrado") || ex.Message.Contains("caracteres"))))
            {
                _logger?.LogError("CrearLogin: Error al crear login", ex, new
                {
                    UsuarioId = dto?.UsuarioId,
                    Username = dto?.Username
                });
                throw;
            }
        }

        // ===================== ACTUALIZAR LOGIN =====================
        public void ActualizarLogin(LoginUpdateDto dto, string adminUser)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            if (string.IsNullOrWhiteSpace(adminUser))
                throw new Exception("El nombre de usuario administrador es obligatorio.");

            // Validar longitud de password si se proporciona
            if (!string.IsNullOrWhiteSpace(dto.NuevoPassword) && dto.NuevoPassword.Length < 6)
                throw new Exception("La contraseña debe tener al menos 6 caracteres.");

            // Validar longitud de username si se proporciona
            if (!string.IsNullOrWhiteSpace(dto.Username) && dto.Username.Length > 50)
                throw new Exception("El username no puede exceder 50 caracteres.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de actualización =====================
                            _logger?.LogInformation("ActualizarLogin: Iniciando actualización de login", new
                            {
                                LoginId = dto.Id,
                                Username = dto.Username,
                                Activo = dto.Activo,
                                CambiarPassword = !string.IsNullOrWhiteSpace(dto.NuevoPassword),
                                AdminUser = adminUser
                            });

                            var entity = ctx.sl_login.FirstOrDefault(l => l.id == dto.Id && !l.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActualizarLogin: Login no encontrado", new { LoginId = dto.Id });
                                throw new Exception("Login no encontrado.");
                            }

                            if (!string.IsNullOrWhiteSpace(dto.Username))
                            {
                                // Username único (dentro de transacción para evitar race conditions)
                                var existeUsername = ctx.sl_login.Any(l =>
                                    l.id != dto.Id &&
                                    l.username == dto.Username.Trim() &&
                                    !l.deletemark);

                                if (existeUsername)
                                {
                                    _logger?.LogWarning("ActualizarLogin: Ya existe otro login con ese username", new
                                    {
                                        LoginId = dto.Id,
                                        Username = dto.Username
                                    });
                                    throw new Exception("Ya existe otro login con ese username.");
                                }

                                // Truncar username según StringLength del modelo
                                var usernameTruncado = dto.Username.Trim();
                                if (usernameTruncado.Length > 50)
                                    usernameTruncado = usernameTruncado.Substring(0, 50);
                                entity.username = usernameTruncado;
                            }

                            entity.activo = dto.Activo;

                            if (!string.IsNullOrWhiteSpace(dto.NuevoPassword))
                            {
                                CrearHashPassword(dto.NuevoPassword, out var salt, out var hash);
                                entity.password_salt = salt;
                                entity.password_hash = hash;
                                entity.password_iteraciones = PasswordUtils.IteracionesActuales;
                            }

                            entity.updatedate = DateTime.Now;
                            // Truncar updateuser según StringLength del modelo
                            var updateuserTruncado = adminUser.Trim();
                            if (updateuserTruncado.Length > 50)
                                updateuserTruncado = updateuserTruncado.Substring(0, 50);
                            entity.updateuser = updateuserTruncado;

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
                                _logger?.LogError("ActualizarLogin: Deadlock detectado", ex, new { LoginId = dto.Id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("ActualizarLogin: Error al guardar login", ex, new { LoginId = dto.Id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("ActualizarLogin: Login actualizado exitosamente", new
                            {
                                LoginId = dto.Id,
                                Username = entity.username
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
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("obligatorio") || ex.Message.Contains("no encontrado") || ex.Message.Contains("caracteres"))))
            {
                _logger?.LogError("ActualizarLogin: Error al actualizar login", ex, new
                {
                    LoginId = dto?.Id,
                    Username = dto?.Username
                });
                throw;
            }
        }

        // ===================== BAJA LÓGICA =====================
        public void EliminarLogin(int id, string adminUser)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del login debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(adminUser))
                throw new Exception("El nombre de usuario administrador es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de eliminación =====================
                            _logger?.LogInformation("EliminarLogin: Iniciando eliminación lógica de login", new
                            {
                                LoginId = id,
                                AdminUser = adminUser
                            });

                            var entity = ctx.sl_login.FirstOrDefault(l => l.id == id && !l.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("EliminarLogin: Login no encontrado", new { LoginId = id });
                                throw new Exception("Login no encontrado.");
                            }

                            entity.deletemark = true;
                            entity.activo = false;
                            entity.updatedate = DateTime.Now;
                            var updateuserTruncado = adminUser.Trim();
                            if (updateuserTruncado.Length > 50)
                                updateuserTruncado = updateuserTruncado.Substring(0, 50);
                            entity.updateuser = updateuserTruncado;

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("EliminarLogin: Deadlock detectado", ex, new { LoginId = id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "eliminar");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("EliminarLogin: Error al guardar eliminación", ex, new { LoginId = id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("EliminarLogin: Login eliminado exitosamente", new
                            {
                                LoginId = id,
                                Username = entity.username
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
                _logger?.LogError("EliminarLogin: Error al eliminar login", ex, new { LoginId = id });
                throw;
            }
        }

        // ===================== ACTIVAR =====================
        public void ActivarLogin(int id, string adminUser)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del login debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(adminUser))
                throw new Exception("El nombre de usuario administrador es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de activación =====================
                            _logger?.LogInformation("ActivarLogin: Iniciando activación de login", new
                            {
                                LoginId = id,
                                AdminUser = adminUser
                            });

                            var entity = ctx.sl_login.FirstOrDefault(l => l.id == id && l.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActivarLogin: Login no encontrado", new { LoginId = id });
                                throw new Exception("Login no encontrado.");
                            }

                            entity.deletemark = false;
                            entity.activo = true;
                            entity.updatedate = DateTime.Now;
                            var updateuserTruncado = adminUser.Trim();
                            if (updateuserTruncado.Length > 50)
                                updateuserTruncado = updateuserTruncado.Substring(0, 50);
                            entity.updateuser = updateuserTruncado;

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActivarLogin: Deadlock detectado", ex, new { LoginId = id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "activar");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("ActivarLogin: Error al guardar activación", ex, new { LoginId = id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("ActivarLogin: Login activado exitosamente", new
                            {
                                LoginId = id,
                                Username = entity.username
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
                _logger?.LogError("ActivarLogin: Error al activar login", ex, new { LoginId = id });
                throw;
            }
        }

        // ===================== AUTENTICAR =====================
        public LoginAuthResultDto Autenticar(LoginRequestDto dto)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null ||
                string.IsNullOrWhiteSpace(dto.Username) ||
                string.IsNullOrWhiteSpace(dto.Password))
            {
                _logger?.LogWarning("Autenticar: Intento de autenticación con datos inválidos", new
                {
                    HasDto = dto != null,
                    HasUsername = !string.IsNullOrWhiteSpace(dto?.Username),
                    HasPassword = !string.IsNullOrWhiteSpace(dto?.Password)
                });
                return new LoginAuthResultDto
                {
                    Ok = false,
                    Mensaje = "Usuario o contraseña incorrectos."
                };
            }

            // Validar longitud de username y password
            if (dto.Username.Length > 50)
            {
                _logger?.LogWarning("Autenticar: Username excede longitud máxima", new { UsernameLength = dto.Username.Length });
                return new LoginAuthResultDto
                {
                    Ok = false,
                    Mensaje = "Usuario o contraseña incorrectos."
                };
            }

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de autenticación (sin username por seguridad) =====================
                    _logger?.LogInformation("Autenticar: Iniciando autenticación", new
                    {
                        HasUsername = !string.IsNullOrWhiteSpace(dto.Username)
                    });

                    var data =
                      (from l in ctx.sl_login
                       join u in ctx.sl_usuario on l.usuario_id equals u.id
                       join j in ctx.sl_jerarquia on u.jerarquia_id equals j.id into jerarquiaJoin
                       from j in jerarquiaJoin.DefaultIfEmpty()
                       where l.username == dto.Username && !l.deletemark
                       select new
                       {
                           Login = l,
                           Usuario = u,
                           Jerarquia = j
                       })
                      .FirstOrDefault();

                    if (data == null)
                    {
                        _logger?.LogWarning("Autenticar: Usuario no encontrado", new { Username = dto.Username });
                        return new LoginAuthResultDto
                        {
                            Ok = false,
                            Mensaje = "Usuario o contraseña incorrectos."
                        };
                    }

                    // Validar que el login esté activo
                    if (!data.Login.activo)
                    {
                        _logger?.LogWarning("Autenticar: Login inactivo", new
                        {
                            LoginId = data.Login.id,
                            Username = data.Login.username
                        });
                        // Mismo mensaje que clave incorrecta, para no revelar qué usernames existen
                        return new LoginAuthResultDto
                        {
                            Ok = false,
                            Mensaje = "Usuario o contraseña incorrectos."
                        };
                    }

                    // Validar que el usuario esté activo
                    if (data.Usuario.deletemark)
                    {
                        _logger?.LogWarning("Autenticar: Usuario inactivo", new
                        {
                            UsuarioId = data.Usuario.id,
                            LoginId = data.Login.id
                        });
                        return new LoginAuthResultDto
                        {
                            Ok = false,
                            Mensaje = "Usuario o contraseña incorrectos."
                        };
                    }

                    // Validar que el login tenga salt y hash (no debe ser null ni vacío)
                    if (data.Login.password_salt == null || data.Login.password_salt.Length != 16 ||
                        data.Login.password_hash == null || data.Login.password_hash.Length != 32)
                    {
                        _logger?.LogWarning("Autenticar: Login sin contraseña configurada (salt/hash null o longitud incorrecta)", new
                        {
                            LoginId = data.Login.id,
                            Username = data.Login.username
                        });
                        return new LoginAuthResultDto
                        {
                            Ok = false,
                            Mensaje = "Usuario o contraseña incorrectos."
                        };
                    }

                    // Verifico contraseña
                    if (!VerificarPassword(dto.Password, data.Login.password_salt, data.Login.password_hash, data.Login.password_iteraciones))
                    {
                        _logger?.LogWarning("Autenticar: Contraseña incorrecta", new
                        {
                            LoginId = data.Login.id,
                            Username = data.Login.username
                        });
                        return new LoginAuthResultDto
                        {
                            Ok = false,
                            Mensaje = "Usuario o contraseña incorrectos."
                        };
                    }

                    // JWT debe estar configurado
                    if (string.IsNullOrWhiteSpace(_jwtSecret))
                    {
                        _logger?.LogError("Autenticar: JwtSecret no configurado en appSettings");
                        return new LoginAuthResultDto
                        {
                            Ok = false,
                            Mensaje = "Error de configuración del servidor. Contacte al administrador."
                        };
                    }

                    var token = GenerateJwtToken(data.Login.usuario_id, data.Usuario.jerarquia_id);

                    // Refresh token: único, guardado en BD con expiración larga (días)
                    var refreshTokenValue = Guid.NewGuid().ToString("N");
                    var refreshEntity = new sl_refresh_token
                    {
                        login_id = data.Login.id,
                        token = refreshTokenValue,
                        expires_at = DateTime.UtcNow.AddDays(_jwtRefreshTokenExpirationDays),
                        created_at = DateTime.UtcNow,
                        revoked = false
                    };
                    ctx.sl_refresh_token.Add(refreshEntity);

                    // Actualizo último login
                    data.Login.last_login = DateTime.Now;

                    try
                    {
                        ctx.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogWarning("Autenticar: Error al guardar last_login o refresh token (no crítico)", ex, new { LoginId = data.Login.id });
                    }

                    _logger?.LogInformation("Autenticar: Autenticación exitosa", new
                    {
                        LoginId = data.Login.id,
                        UsuarioId = data.Login.usuario_id,
                        Username = data.Login.username
                    });

                    // Solo exigir cambio de clave cuando smarTime está activo (usuarios creados desde SmartTime)
                    var smarTimeActivo = "true".Equals(
                        (ConfigurationManager.AppSettings["smarTime"] ?? "").Trim(),
                        StringComparison.OrdinalIgnoreCase);
                    var requiereCambioClave = smarTimeActivo && data.Login.debe_cambiar_clave;

                    return new LoginAuthResultDto
                    {
                        Ok = true,
                        Mensaje = "Login correcto.",
                        UsuarioId = data.Login.usuario_id,
                        Username = data.Login.username,
                        NombreCompleto = $"{data.Usuario.nombre} {data.Usuario.apellido}",
                        Jerarquia = data.Jerarquia != null ? data.Jerarquia.nombre : null,
                        Activo = true,
                        Token = token,
                        RefreshToken = refreshTokenValue,
                        RequiereCambioClave = requiereCambioClave
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Autenticar: Error durante autenticación. {Message}", ex.Message);
                // No exponer detalles del error por seguridad
                return new LoginAuthResultDto
                {
                    Ok = false,
                    Mensaje = "Error al procesar la autenticación. Por favor, intente nuevamente."
                };
            }
        }

        /// <summary>
        /// Valida el refresh token, revoca el anterior, genera nuevo JWT y nuevo refresh token (rotación). Devuelve null si inválido o expirado.
        /// </summary>
        public RefreshTokenResponseDto RefreshToken(string refreshToken)
        {
            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                _logger?.LogWarning("[RefreshToken Servicio] Paso 1: Entrada vacía, devolviendo null.");
                return null;
            }

            var trimmed = refreshToken.Trim();
            var trace = trimmed.Length > 8 ? trimmed.Substring(0, 8) + "…" : "***";

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                _logger?.LogInformation("[RefreshToken Servicio] Paso 2: Buscando token en BD (no revocado, no expirado). Trace: {Trace}", trace);
                var rt = ctx.sl_refresh_token
                    .Include(x => x.Login)
                    .FirstOrDefault(x => x.token == trimmed && !x.revoked && x.expires_at > DateTime.UtcNow);

                if (rt == null)
                {
                    _logger?.LogWarning("[RefreshToken Servicio] Paso 2: No encontrado o expirado/revocado. Trace: {Trace}. Devolviendo null.", trace);
                    return null;
                }

                _logger?.LogInformation("[RefreshToken Servicio] Paso 3: Token encontrado. RefreshTokenId={RefreshTokenId}, LoginId={LoginId}, Expira={ExpiresAt}.",
                    rt.id, rt.login_id, rt.expires_at);

                var login = rt.Login;
                if (login == null || login.deletemark || !login.activo)
                {
                    _logger?.LogWarning("[RefreshToken Servicio] Paso 4: Login inválido (null, eliminado o inactivo). LoginId={LoginId}. Devolviendo null.", rt.login_id);
                    return null;
                }

                _logger?.LogInformation("[RefreshToken Servicio] Paso 5: Login válido. Username={Username}. Cargando usuario.", login.username);

                var usuario = ctx.sl_usuario
                    .Include(u => u.jerarquia)
                    .FirstOrDefault(u => u.id == login.usuario_id);
                if (usuario == null || usuario.deletemark)
                {
                    _logger?.LogWarning("[RefreshToken Servicio] Paso 6: Usuario no encontrado o inactivo. UsuarioId={UsuarioId}. Devolviendo null.", login.usuario_id);
                    return null;
                }

                _logger?.LogInformation("[RefreshToken Servicio] Paso 7: Usuario válido. UsuarioId={UsuarioId}. Rotación: revocando token anterior y creando uno nuevo.", usuario.id);

                // Rotación: revocar el refresh token usado y crear uno nuevo
                rt.revoked = true;
                var newRefreshValue = Guid.NewGuid().ToString("N");
                var newRefresh = new sl_refresh_token
                {
                    login_id = login.id,
                    token = newRefreshValue,
                    expires_at = DateTime.UtcNow.AddDays(_jwtRefreshTokenExpirationDays),
                    created_at = DateTime.UtcNow,
                    revoked = false
                };
                ctx.sl_refresh_token.Add(newRefresh);

                try
                {
                    ctx.SaveChanges();
                    _logger?.LogInformation("[RefreshToken Servicio] Paso 8: BD actualizada (token anterior revocado, nuevo guardado). Generando nuevo JWT.");
                }
                catch (Exception ex)
                {
                    _logger?.LogError("[RefreshToken Servicio] Paso 8: Error al guardar en BD. Devolviendo null.", ex, new { LoginId = login.id });
                    return null;
                }

                var newJwt = GenerateJwtToken(usuario.id, usuario.jerarquia_id);
                _logger?.LogInformation("[RefreshToken Servicio] Paso 9: Éxito. Nuevo JWT y RefreshToken generados para UsuarioId={UsuarioId}, LoginId={LoginId}.", usuario.id, login.id);

                return new RefreshTokenResponseDto
                {
                    Token = newJwt,
                    RefreshToken = newRefreshValue
                };
            }
        }

        // ===================== Helpers de password =====================

        private string GenerateJwtToken(int usuario_id, int? jerarquia_id = null)
        {
            var keyBytes = Encoding.UTF8.GetBytes(_jwtSecret);
            var securityKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var role = JerarquiaRolHelper.RolDesdeJerarquia(jerarquia_id);
            var claims = new List<Claim>
            {
                new Claim("usuario", usuario_id.ToString()),
                new Claim(ClaimTypes.Role, role)
            };

            var expirationHours = GetJwtExpirationHours();
            var expiration = DateTime.UtcNow.AddHours(expirationHours);
            _logger?.LogInformation("[JWT] Token generado: expiración {Hours} h, exp (UTC) = {ExpUtc}.", expirationHours, expiration.ToString("O"));
            var tokenDescriptor = new JwtSecurityToken(
                issuer: _jwtIssuer,
                audience: _jwtAudience,
                claims: claims.ToArray(),
                notBefore: DateTime.UtcNow,
                expires: expiration,
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(tokenDescriptor);
        }

        /// <summary>Única fuente de duración del JWT. Lee JwtExpirationHours de appSettings (por defecto 10).</summary>
        private static int GetJwtExpirationHours()
        {
            var value = ConfigurationManager.AppSettings["JwtExpirationHours"];
            return int.TryParse(value, out var h) && h > 0 ? h : 10;
        }

        private void CrearHashPassword(string password, out byte[] salt, out byte[] hash)
        {
            PasswordUtils.CreateHash(password, out salt, out hash);
        }

        private bool VerificarPassword(string password, byte[] salt, byte[] hash, int? iteraciones)
        {
            // Validar que salt y hash no sean null
            if (salt == null || hash == null)
            {
                _logger?.LogWarning("VerificarPassword: Salt o hash son null");
                return false;
            }

            try
            {
                return PasswordUtils.VerificarHash(password, salt, hash, iteraciones);
            }
            catch (Exception ex)
            {
                _logger?.LogError("VerificarPassword: Error al verificar contraseña", ex);
                return false;
            }
        }

        public void CambiarClave(LoginCambiarClaveDto dto, string adminUser)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            if (string.IsNullOrWhiteSpace(dto.NuevaClave))
                throw new Exception("La nueva clave es obligatoria.");

            if (string.IsNullOrWhiteSpace(adminUser))
                throw new Exception("El nombre de usuario administrador es obligatorio.");

            // Misma regla de fortaleza que exige el frontend (evitable llamando a la API directo)
            PasswordUtils.ValidarFortaleza(dto.NuevaClave);

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de cambio de clave =====================
                            _logger?.LogInformation("CambiarClave: Iniciando cambio de contraseña", new
                            {
                                LoginId = dto.Id,
                                ValidarClaveActual = !string.IsNullOrWhiteSpace(dto.ClaveActual),
                                AdminUser = adminUser
                            });

                            var login = ctx.sl_login.FirstOrDefault(l => l.id == dto.Id && !l.deletemark);
                            if (login == null)
                            {
                                _logger?.LogWarning("CambiarClave: Login no encontrado", new { LoginId = dto.Id });
                                throw new Exception("Login no encontrado.");
                            }

                            // Si viene clave actual, la verificamos
                            if (!string.IsNullOrWhiteSpace(dto.ClaveActual))
                            {
                                if (!VerificarPassword(dto.ClaveActual, login.password_salt, login.password_hash, login.password_iteraciones))
                                {
                                    _logger?.LogWarning("CambiarClave: Clave actual incorrecta", new { LoginId = dto.Id });
                                    throw new Exception("La clave actual no es correcta.");
                                }
                            }

                            // Validar que la nueva clave sea diferente de la actual (si se proporcionó)
                            if (!string.IsNullOrWhiteSpace(dto.ClaveActual))
                            {
                                if (dto.ClaveActual == dto.NuevaClave)
                                {
                                    _logger?.LogWarning("CambiarClave: La nueva clave es igual a la actual", new { LoginId = dto.Id });
                                    throw new Exception("La nueva contraseña debe ser diferente de la actual.");
                                }
                            }

                            // Generar nuevo hash + salt
                            CrearHashPassword(dto.NuevaClave, out var salt, out var hash);
                            login.password_salt = salt;
                            login.password_hash = hash;
                            login.password_iteraciones = PasswordUtils.IteracionesActuales;

                            // Solo poner must_change_password en 0 si el usuario fue creado por smarTime; si no, solo cambiar la clave
                            var usuario = ctx.sl_usuario.FirstOrDefault(u => u.id == login.usuario_id);
                            if (usuario != null && string.Equals(usuario.createuser, "smarTime", StringComparison.OrdinalIgnoreCase))
                                login.debe_cambiar_clave = false;

                            login.updatedate = DateTime.Now;
                            var updateuserTruncado = adminUser.Trim();
                            if (updateuserTruncado.Length > 50)
                                updateuserTruncado = updateuserTruncado.Substring(0, 50);
                            login.updateuser = updateuserTruncado;

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("CambiarClave: Deadlock detectado", ex, new { LoginId = dto.Id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "cambiar contraseña");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("CambiarClave: Error al guardar cambio de contraseña", ex, new { LoginId = dto.Id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("CambiarClave: Contraseña cambiada exitosamente", new
                            {
                                LoginId = dto.Id,
                                Username = login.username
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
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("obligatorio") || ex.Message.Contains("no encontrado") || ex.Message.Contains("correcta") || ex.Message.Contains("diferente") || ex.Message.Contains("caracteres"))))
            {
                _logger?.LogError("CambiarClave: Error al cambiar contraseña", ex, new
                {
                    LoginId = dto?.Id
                });
                throw;
            }
        }

    }
}
