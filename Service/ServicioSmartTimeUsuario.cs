using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using smartlunch_api.Dtos;
using smartlunch_api.Models;
using smartlunch_api.Utils;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Servicio para operaciones de usuarios desde la integración smarTime.
    /// Crea sl_usuario + sl_login con createuser/origen_datos = "smarTime" y fecha_ultima_sincronizacion,
    /// sin modificar ServicioUsuario ni el flujo existente.
    /// </summary>
    public class ServicioSmartTimeUsuario
    {
        private const string OrigenSmarTime = "smarTime";
        /// <summary>Clave por defecto solo para usuarios creados desde smarTime. No afecta al flujo normal de ServicioUsuario.</summary>
        private const string ClavePorDefectoSmarTime = "12345678";

        /// <summary>
        /// Crea un usuario y su login desde smarTime. Usa defaults de catálogo (is_default, incl. jerarquía con is_default=1) y marca createuser/origen_datos = "smarTime".
        /// </summary>
        /// <param name="dto">Datos del usuario (nombre, apellido, legajo, dni obligatorios; cuil, domicilio y fechaIngreso opcionales).</param>
        /// <returns>DTO con id, legajo, nombre, apellido, username y si requiere cambio de clave.</returns>
        public SmartTimeUsuarioCreadoDto CrearUsuario(SmartTimeUsuarioCrearDto dto)
        {
            if (dto == null)
                throw new ArgumentException("Datos inválidos.", nameof(dto));

            if (string.IsNullOrWhiteSpace(dto.Nombre) || string.IsNullOrWhiteSpace(dto.Apellido))
                throw new ArgumentException("Nombre y apellido son obligatorios.");

            if (dto.Legajo <= 0)
                throw new ArgumentException("El legajo debe ser mayor a 0.");

            if (dto.Dni <= 0)
                throw new ArgumentException("El DNI es obligatorio y debe ser válido.");

            // CUIL opcional en smarTime; si se envía, debe ser válido
            var cuilTrimmed = string.IsNullOrWhiteSpace(dto.Cuil) ? null : dto.Cuil.Trim();
            if (cuilTrimmed != null && !CuilValidator.EsValido(cuilTrimmed))
                throw new ArgumentException("El CUIL no es válido. Debe tener 11 dígitos y dígito verificador correcto.");

            var defaults = ServicioDefaultsCatalogo.Obtener();
            if (defaults == null)
                throw new InvalidOperationException("No se pudieron obtener los valores por defecto de catálogo.");

            using (var ctx = new DataContext())
            {
                using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var existeDuplicado = ctx.sl_usuario
                            .Where(u => !u.deletemark &&
                                (u.dni == dto.Dni ||
                                 u.legajo == dto.Legajo ||
                                 (cuilTrimmed != null && u.cuil == cuilTrimmed)))
                            .Select(u => new { u.dni, u.legajo, u.cuil })
                            .FirstOrDefault();

                        if (existeDuplicado != null)
                        {
                            if (existeDuplicado.dni == dto.Dni)
                                throw new ArgumentException("Ya existe un usuario con el mismo DNI.");
                            if (existeDuplicado.legajo == dto.Legajo)
                                throw new ArgumentException("Ya existe un usuario con el mismo legajo.");
                            if (cuilTrimmed != null && existeDuplicado.cuil == cuilTrimmed)
                                throw new ArgumentException("Ya existe un usuario con el mismo CUIL.");
                        }

                        // En smarTime el username es el DNI; debe ser único. Si ya existe, no crear.
                        var dniComoUsername = dto.Dni.ToString();
                        if (ctx.sl_login.Any(l => l.username == dniComoUsername && !l.deletemark))
                            throw new ArgumentException("Ya existe un usuario con ese DNI como nombre de usuario. El DNI es único para el acceso.");

                        // Usuarios SmartTime se crean con la jerarquía donde sl_jerarquia.is_default = 1.
                        if (defaults.JerarquiaId <= 0)
                            throw new InvalidOperationException("No hay jerarquía por defecto (is_default=1) configurada en sl_jerarquia.");
                        //var jerarquiaExiste = ctx.sl_jerarquia.Any(j => j.id == defaults.JerarquiaId && !j.deletemark);
                        //if (!jerarquiaExiste)
                            //throw new InvalidOperationException("La jerarquía por defecto (is_default=1) no existe o está dada de baja en sl_jerarquia.");

                        // Validar que existan las FKs por defecto
                        var planExiste = ctx.sl_plannutricional.Any(p => p.id == defaults.PlanNutricionalId && !p.deletemark);
                        var plantaExiste = ctx.sl_planta.Any(p => p.id == defaults.PlantaId && !p.deletemark);
                        var centroExiste = ctx.sl_centrodecosto.Any(c => c.id == defaults.CentroCostoId && !c.deletemark);
                        var proyectoExiste = ctx.sl_proyecto.Any(p => p.id == defaults.ProyectoId && !p.deletemark);

                        if (!planExiste)
                            throw new InvalidOperationException("El plan nutricional por defecto no existe.");
                        if (!plantaExiste)
                            throw new InvalidOperationException("La planta por defecto no existe.");
                        if (!centroExiste)
                            throw new InvalidOperationException("El centro de costo por defecto no existe.");
                        if (!proyectoExiste)
                            throw new InvalidOperationException("El proyecto por defecto no existe.");

                        var ahora = DateTime.Now;
                        var entity = new sl_usuario
                        {
                            nombre = dto.Nombre.Trim(),
                            apellido = dto.Apellido.Trim(),
                            legajo = dto.Legajo,
                            dni = dto.Dni,
                            cuil = cuilTrimmed,
                            domicilio = string.IsNullOrWhiteSpace(dto.Domicilio) ? null : dto.Domicilio.Trim(),
                            fechaingreso = dto.FechaIngreso,
                            plannutricional_id = defaults.PlanNutricionalId,
                            planta_id = defaults.PlantaId,
                            centrodecosto_id = defaults.CentroCostoId,
                            proyecto_id = defaults.ProyectoId,
                            jerarquia_id = 3,//defaults.JerarquiaId,//defaults.JerarquiaId,
                            bonificaciones_invitado = 0,
                            pedidos = 0,
                            bonificaciones = 0,
                            createdate = ahora,
                            createuser = OrigenSmarTime,
                            deletemark = false,
                            origen_datos = OrigenSmarTime,
                            fecha_ultima_sincronizacion = ahora
                        };

                        ctx.sl_usuario.Add(entity);

                        try
                        {
                            ctx.SaveChanges();
                        }
                        catch (DbEntityValidationException ex)
                        {
                            throw new Exception(FormatValidationErrors(ex), ex);
                        }
                        catch (SqlException ex) when (ex.Number == 1205)
                        {
                            transaction.Rollback();
                            throw new InvalidOperationException("Error de concurrencia. Por favor, intente nuevamente.", ex);
                        }

                        // Crear login: username = DNI (ya validado como único), contraseña 12345678, debe_cambiar_clave = true
                        var usernameLogin = entity.dni.ToString();
                        if (usernameLogin.Length > 50)
                            usernameLogin = usernameLogin.Substring(0, 50);

                        byte[] salt, hash;
                        PasswordUtils.CreateHash(ClavePorDefectoSmarTime, out salt, out hash);

                        var createuserTruncado = OrigenSmarTime.Length > 50 ? OrigenSmarTime.Substring(0, 50) : OrigenSmarTime;
                        var loginEntity = new sl_login
                        {
                            usuario_id = entity.id,
                            username = usernameLogin,
                            password_salt = salt,
                            password_hash = hash,
                            password_iteraciones = PasswordUtils.IteracionesActuales,
                            activo = true,
                            deletemark = false,
                            debe_cambiar_clave = true,
                            createdate = ahora,
                            createuser = createuserTruncado
                        };

                        ctx.sl_login.Add(loginEntity);

                        try
                        {
                            ctx.SaveChanges();
                        }
                        catch (DbEntityValidationException ex)
                        {
                            throw new Exception(FormatValidationErrors(ex), ex);
                        }
                        catch (SqlException ex) when (ex.Number == 1205)
                        {
                            transaction.Rollback();
                            throw new InvalidOperationException("Error de concurrencia. Por favor, intente nuevamente.", ex);
                        }

                        transaction.Commit();

                        return new SmartTimeUsuarioCreadoDto
                        {
                            Id = entity.id,
                            Legajo = entity.legajo,
                            Nombre = entity.nombre,
                            Apellido = entity.apellido,
                            Username = usernameLogin,
                            RequiereCambioClave = true
                        };
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        /// <summary>
        /// Obtiene un usuario smarTime por legajo. Solo usuarios con origen_datos/createuser = "smarTime". Devuelve null si no existe o no es de smarTime.
        /// </summary>
        public SmartTimeUsuarioListadoDto ObtenerPorLegajo(int legajo)
        {
            if (legajo <= 0)
                return null;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                var entity = ctx.sl_usuario
                    .Include(u => u.Logins)
                    .FirstOrDefault(u => u.legajo == legajo &&
                        (u.origen_datos == OrigenSmarTime || u.createuser == OrigenSmarTime));
                if (entity == null)
                    return null;

                var login = entity.Logins?.FirstOrDefault(l => !l.deletemark);
                return new SmartTimeUsuarioListadoDto
                {
                    Id = entity.id,
                    Legajo = entity.legajo,
                    Nombre = entity.nombre,
                    Apellido = entity.apellido,
                    Dni = entity.dni,
                    Cuil = entity.cuil,
                    Domicilio = entity.domicilio,
                    FechaIngreso = entity.fechaingreso,
                    Username = login?.username,
                    Activo = !entity.deletemark
                };
            }
        }

        /// <summary>
        /// Lista usuarios creados por smarTime (origen_datos o createuser = "smarTime"), con paginación.
        /// </summary>
        /// <param name="soloActivos">True = solo usuarios activos (!deletemark). False = solo usuarios inactivos (dados de baja).</param>
        public PagedResultDto<SmartTimeUsuarioListadoDto> ListarUsuarios(int page = 1, int pageSize = 10, string search = null, bool soloActivos = true)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                var query = ctx.sl_usuario
                    .Where(u => u.deletemark == !soloActivos &&
                        (u.origen_datos == OrigenSmarTime || u.createuser == OrigenSmarTime));

                if (!string.IsNullOrWhiteSpace(search))
                {
                    var term = search.Trim().ToLower();
                    query = query.Where(u =>
                        u.nombre.ToLower().Contains(term) ||
                        u.apellido.ToLower().Contains(term) ||
                        u.legajo.ToString().Contains(term) ||
                        u.dni.ToString().Contains(term) ||
                        (u.cuil != null && u.cuil.ToLower().Contains(term)));
                }

                int totalItems;
                List<SmartTimeUsuarioListadoDto> items;

                if (!soloActivos)
                {
                    // Usuarios dados de baja: agrupar por mismo dato (dni, nombre, apellido) sin legajo y mostrar solo el último (mayor id) de cada grupo.
                    var listadoCompleto = query.Include(u => u.Logins).OrderBy(u => u.apellido).ThenBy(u => u.nombre).ToList();
                    var listadoAgrupado = listadoCompleto
                        .GroupBy(u => new { u.dni, nombre = (u.nombre ?? "").Trim(), apellido = (u.apellido ?? "").Trim() })
                        .Select(g => g.OrderByDescending(x => x.id).First())
                        .OrderBy(u => u.apellido).ThenBy(u => u.nombre)
                        .ToList();
                    totalItems = listadoAgrupado.Count;
                    items = listadoAgrupado
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .Select(u =>
                        {
                            var login = u.Logins?.FirstOrDefault(l => !l.deletemark);
                            return new SmartTimeUsuarioListadoDto
                            {
                                Id = u.id,
                                Legajo = u.legajo,
                                Nombre = u.nombre,
                                Apellido = u.apellido,
                                Dni = u.dni,
                                Cuil = u.cuil,
                                Domicilio = u.domicilio,
                                FechaIngreso = u.fechaingreso,
                                Username = login?.username,
                                Activo = !u.deletemark
                            };
                        })
                        .ToList();
                }
                else
                {
                    totalItems = query.Count();
                    items = query
                        .Include(u => u.Logins)
                        .OrderBy(u => u.apellido).ThenBy(u => u.nombre)
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList()
                        .Select(u =>
                        {
                            var login = u.Logins?.FirstOrDefault(l => !l.deletemark);
                            return new SmartTimeUsuarioListadoDto
                            {
                                Id = u.id,
                                Legajo = u.legajo,
                                Nombre = u.nombre,
                                Apellido = u.apellido,
                                Dni = u.dni,
                                Cuil = u.cuil,
                                Domicilio = u.domicilio,
                                FechaIngreso = u.fechaingreso,
                                Username = login?.username,
                                Activo = !u.deletemark
                            };
                        })
                        .ToList();
                }

                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                return new PagedResultDto<SmartTimeUsuarioListadoDto>
                {
                    page = page,
                    pageSize = pageSize,
                    totalItems = totalItems,
                    totalPages = totalPages,
                    items = items
                };
            }
        }

        /// <summary>
        /// Actualiza un usuario smarTime por legajo. Solo permite editar si createuser/origen_datos = "smarTime".
        /// </summary>
        public void ActualizarPorLegajo(int legajo, SmartTimeUsuarioActualizarDto dto)
        {
            if (dto == null)
                throw new ArgumentException("Datos inválidos.", nameof(dto));
            if (legajo <= 0)
                throw new ArgumentException("El legajo debe ser mayor a 0.");

            // CUIL opcional; si se envía, debe ser válido
            var cuilTrimmed = string.IsNullOrWhiteSpace(dto.Cuil) ? null : dto.Cuil.Trim();
            if (cuilTrimmed != null && !CuilValidator.EsValido(cuilTrimmed))
                throw new ArgumentException("El CUIL no es válido. Debe tener 11 dígitos y dígito verificador correcto.");

            using (var ctx = new DataContext())
            {
                // Buscar por legajo (activo o inactivo) para poder reactivar desde este endpoint
                var entity = ctx.sl_usuario.FirstOrDefault(u => u.legajo == legajo);
                if (entity == null)
                    throw new ArgumentException("Usuario no encontrado.");
                if (entity.origen_datos != OrigenSmarTime && entity.createuser != OrigenSmarTime)
                    throw new InvalidOperationException("Solo se pueden editar usuarios creados por smarTime.");

                // Validar DNI y CUIL solo si están cambiando; si se mantienen iguales no exigir unicidad contra otros (permite solo cambiar activo, etc.)
                if (dto.Dni != entity.dni)
                {
                    var existeDni = ctx.sl_usuario.Any(u => u.id != entity.id && u.dni == dto.Dni && !u.deletemark);
                    if (existeDni)
                        throw new ArgumentException("Ya existe otro usuario con el mismo DNI.");
                }
                if (cuilTrimmed != null || entity.cuil != null)
                {
                    var cuilActual = entity.cuil ?? "";
                    var cuilNuevo = cuilTrimmed ?? "";
                    if (cuilNuevo != cuilActual)
                    {
                        if (cuilTrimmed != null)
                        {
                            var existeCuil = ctx.sl_usuario.Any(u => u.id != entity.id && u.cuil == cuilTrimmed && !u.deletemark);
                            if (existeCuil)
                                throw new ArgumentException("Ya existe otro usuario con el mismo CUIL.");
                        }
                    }
                }

                entity.nombre = dto.Nombre.Trim();
                entity.apellido = dto.Apellido.Trim();
                entity.dni = dto.Dni;
                entity.cuil = cuilTrimmed;
                entity.domicilio = string.IsNullOrWhiteSpace(dto.Domicilio) ? null : dto.Domicilio.Trim();
                entity.fechaingreso = dto.FechaIngreso;
                entity.updatedate = DateTime.Now;
                entity.updateuser = OrigenSmarTime;
                entity.fecha_ultima_sincronizacion = DateTime.Now;

                // Estado activo/inactivo: actualiza sl_usuario.deletemark y sl_login.deletemark
                if (dto.Activo.HasValue)
                {
                    entity.deletemark = !dto.Activo.Value;
                    var logins = ctx.sl_login.Where(l => l.usuario_id == entity.id).ToList();
                    foreach (var login in logins)
                    {
                        login.deletemark = !dto.Activo.Value;
                        login.updatedate = DateTime.Now;
                        login.updateuser = OrigenSmarTime;
                    }
                }

                try
                {
                    ctx.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    throw new Exception(FormatValidationErrors(ex), ex);
                }
            }
        }

        /// <summary>
        /// Da de baja (deletemark) un usuario smarTime y sus logins por legajo. Solo si createuser/origen_datos = "smarTime".
        /// </summary>
        public void DarDeBajaPorLegajo(int legajo)
        {
            if (legajo <= 0)
                throw new ArgumentException("El legajo debe ser mayor a 0.");

            using (var ctx = new DataContext())
            {
                using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    try
                    {
                        var entity = ctx.sl_usuario.FirstOrDefault(u => u.legajo == legajo && !u.deletemark);
                        if (entity == null)
                            throw new ArgumentException("Usuario no encontrado.");
                        if (entity.origen_datos != OrigenSmarTime && entity.createuser != OrigenSmarTime)
                            throw new InvalidOperationException("Solo se puede dar de baja a usuarios creados por smarTime.");

                        entity.deletemark = true;
                        entity.updatedate = DateTime.Now;
                        entity.updateuser = OrigenSmarTime;
                        entity.fecha_ultima_sincronizacion = DateTime.Now;

                        var logins = ctx.sl_login.Where(l => l.usuario_id == entity.id && !l.deletemark).ToList();
                        foreach (var login in logins)
                        {
                            login.deletemark = true;
                            login.updatedate = DateTime.Now;
                            login.updateuser = OrigenSmarTime;
                        }

                        ctx.SaveChanges();
                        transaction.Commit();
                    }
                    catch
                    {
                        transaction.Rollback();
                        throw;
                    }
                }
            }
        }

        private static string FormatValidationErrors(DbEntityValidationException ex)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Errores de validación al crear el usuario:");
            foreach (var eve in ex.EntityValidationErrors)
            {
                foreach (var ve in eve.ValidationErrors)
                    sb.AppendLine($"  • {ve.PropertyName}: {ve.ErrorMessage}");
            }
            return sb.ToString();
        }
    }
}
