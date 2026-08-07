using System;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Security.Cryptography;
using smartlunch_api.Models;
using smartlunch_api.Services;

// Para SqlParameter en INSERT directo a sl_login
using System.Data.SqlClient;

namespace smartlunch_api.App_Start
{
    /// <summary>
    /// Inserta datos iniciales (catálogos + usuario admin) la primera vez que se levanta la aplicación.
    /// Los IDs de catálogo coinciden con los valores por defecto de Web.config (Planta=1, Plan_nutricional=2, etc.).
    /// </summary>
    public static class DatabaseSeeder
    {
        /// <summary>
        /// Usuario del administrador creado en el seed.
        /// </summary>
        public const string AdminUsername = "admin";

        /// <summary>
        /// Contraseña por defecto del login admin al crear el registro en sl_login.
        /// </summary>
        public const string AdminPasswordDefault = "sm@rtLunch!26";

        /// <summary>
        /// Usuario SmartTime creado por el seed (username para integración).
        /// </summary>
        public const string SmartTimeUsername = "smarTime";

        /// <summary>
        /// Contraseña por defecto del usuario smarTime (alfanumérica).
        /// </summary>
        public const string SmartTimePasswordDefault = "Sm@rtT1m326!";

        /// <summary>
        /// Ejecuta el seed solo si los catálogos están vacíos. No sobrescribe datos existentes.
        /// </summary>
        public static void SeedIfEmpty()
        {
            SeedIfEmpty(null);
        }

        /// <summary>
        /// Ejecuta el seed contra la conexión indicada (o la de config si connectionString es null).
        /// </summary>
        public static void SeedIfEmpty(string connectionString)
        {
            try
            {
                using (var ctx = connectionString == null ? new DataContext() : new DataContext(connectionString))
                {
                    if (ctx.sl_plannutricional.Any())
                        return;
                    SeedCatalogos(ctx);
                    SeedAdminUsuario(ctx);
                    SeedLogin(ctx);
                    if (SmarTimeEstaHabilitado())
                        SeedSmartTimeUsuarioYLogin(ctx);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseSeeder] Seed falló: {ex.Message}");
            }
        }

        /// <summary>
        /// Crea o actualiza el usuario administrador con la contraseña indicada.
        /// </summary>
        public static bool CrearOActualizarAdmin(string password, out string mensaje)
        {
            return CrearOActualizarAdmin(password, null, out mensaje);
        }

        /// <summary>
        /// Crea o actualiza el usuario administrador con la contraseña indicada, usando la conexión dada (o config si null).
        /// </summary>
        public static bool CrearOActualizarAdmin(string password, string connectionString, out string mensaje)
        {
            mensaje = "";
            if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
            {
                mensaje = "La contraseña debe tener al menos 6 caracteres.";
                return false;
            }
            if (password.Length > 100)
            {
                mensaje = "La contraseña no puede superar 100 caracteres.";
                return false;
            }
            try
            {
                using (var ctx = connectionString == null ? new DataContext() : new DataContext(connectionString))
                {
                    var adminLogin = ctx.sl_login.FirstOrDefault(l => l.username == AdminUsername && !l.deletemark);
                    byte[] salt, hash;
                    PasswordUtils.CreateHash(password, out salt, out hash);

                    // Ya existe admin: si el salt es todo ceros (script o placeholder), reemplazar con la contraseña que el usuario indicó
                    if (adminLogin != null)
                    {
                        var saltEsPlaceholder = adminLogin.password_salt != null && adminLogin.password_salt.Length == 16 &&
                            adminLogin.password_salt.All(b => b == 0);
                        if (!saltEsPlaceholder)
                        {
                            mensaje = "El administrador ya está configurado. Use la pantalla de login para acceder.";
                            return false;
                        }
                        adminLogin.password_salt = salt;
                        adminLogin.password_hash = hash;
                        adminLogin.password_iteraciones = PasswordUtils.IteracionesActuales;
                        adminLogin.debe_cambiar_clave = false;
                        ctx.SaveChanges();
                        mensaje = "Administrador configurado correctamente. Ya puede iniciar sesión con usuario \"admin\".";
                        return true;
                    }

                    var usuario = ctx.sl_usuario.FirstOrDefault(u => u.legajo == 1 && !u.deletemark);
                    if (usuario == null)
                    {
                        SeedAdminUsuario(ctx);
                        ctx.SaveChanges();
                        usuario = ctx.sl_usuario.FirstOrDefault(u => u.legajo == 1 && !u.deletemark);
                    }
                    if (usuario == null)
                    {
                        mensaje = "No se pudo crear el usuario base. Verifique la base de datos.";
                        return false;
                    }

                    var sql = @"INSERT INTO sl_login (usuario_id, username, password_salt, password_hash, password_iteraciones, activo, createdate, createuser, deletemark, must_change_password)
VALUES (@p0, @p1, @p2, @p3, @p6, 1, @p4, @p5, 0, 0)";
                    var p0 = new SqlParameter("@p0", usuario.id);
                    var p1 = new SqlParameter("@p1", AdminUsername);
                    var p2 = new SqlParameter("@p2", System.Data.SqlDbType.VarBinary, 16) { Value = salt };
                    var p3 = new SqlParameter("@p3", System.Data.SqlDbType.VarBinary, 32) { Value = hash };
                    var p4 = new SqlParameter("@p4", DateTime.Now);
                    var p5 = new SqlParameter("@p5", SeedUser);
                    var p6 = new SqlParameter("@p6", PasswordUtils.IteracionesActuales);
                    ctx.Database.ExecuteSqlCommand(sql, p0, p1, p2, p3, p4, p5, p6);
                    mensaje = "Administrador creado correctamente. Ya puede iniciar sesión con usuario \"admin\".";
                    return true;
                }
            }
            catch (Exception ex)
            {
                mensaje = "Error al configurar el administrador: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Indica si el administrador ya está configurado (existe login admin con contraseña real).
        /// </summary>
        public static bool AdminYaConfigurado()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    var adminLogin = ctx.sl_login.FirstOrDefault(l => l.username == AdminUsername && !l.deletemark);
                    if (adminLogin == null) return false;
                    if (adminLogin.password_salt == null || adminLogin.password_hash == null) return false;
                    if (adminLogin.password_salt.Length != 16 || adminLogin.password_hash.Length != 32) return false;
                    if (adminLogin.password_salt.All(b => b == 0) && adminLogin.password_hash.All(b => b == 0))
                        return false; // placeholder
                    return true;
                }
            }
            catch { return false; }
        }

        /// <summary>
        /// Garantiza que exista un login "admin" para el usuario legajo 1 con contraseña real.
        /// - Si no existe login admin: crea usuario si falta y inserta sl_login por SQL directo.
        /// - Si existe login admin con hash placeholder (salt todo ceros): actualiza con contraseña real.
        /// </summary>
        public static void EnsureAdminLoginExists()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    var adminLogin = ctx.sl_login.FirstOrDefault(l => l.username == AdminUsername && !l.deletemark);
                    var adminPassword = AdminPasswordDefault;
                    byte[] salt, hash;
                    PasswordUtils.CreateHash(adminPassword, out salt, out hash);

                    // Caso 1: existe login admin pero con hash placeholder (salt y hash todo ceros; script antiguo)
                    if (adminLogin != null && adminLogin.password_salt != null && adminLogin.password_salt.Length == 16 &&
                        adminLogin.password_salt.All(b => b == 0) &&
                        adminLogin.password_hash != null && adminLogin.password_hash.Length == 32 &&
                        adminLogin.password_hash.All(b => b == 0))
                    {
                        adminLogin.password_salt = salt;
                        adminLogin.password_hash = hash;
                        adminLogin.password_iteraciones = PasswordUtils.IteracionesActuales;
                        adminLogin.debe_cambiar_clave = false;
                        ctx.SaveChanges();
                        System.Diagnostics.Debug.WriteLine("[DatabaseSeeder] EnsureAdminLoginExists: contraseña placeholder actualizada.");
                        return;
                    }

                    // Caso 2: ya existe login admin con contraseña real
                    if (adminLogin != null)
                        return;

                    // Caso 3: no existe login admin — asegurar usuario legajo 1 y crear login por SQL directo
                    var usuario = ctx.sl_usuario.FirstOrDefault(u => u.legajo == 1 && !u.deletemark);
                    if (usuario == null)
                    {
                        SeedAdminUsuario(ctx);
                        ctx.SaveChanges();
                        usuario = ctx.sl_usuario.FirstOrDefault(u => u.legajo == 1 && !u.deletemark);
                    }
                    if (usuario == null)
                    {
                        System.Diagnostics.Debug.WriteLine("[DatabaseSeeder] EnsureAdminLoginExists: no se pudo obtener usuario legajo 1.");
                        return;
                    }

                    var sql = @"INSERT INTO sl_login (usuario_id, username, password_salt, password_hash, password_iteraciones, activo, createdate, createuser, deletemark, must_change_password)
VALUES (@p0, @p1, @p2, @p3, @p6, 1, @p4, @p5, 0, 0)";
                    var p0 = new SqlParameter("@p0", usuario.id);
                    var p1 = new SqlParameter("@p1", AdminUsername);
                    var p2 = new SqlParameter("@p2", System.Data.SqlDbType.VarBinary, 16) { Value = salt };
                    var p3 = new SqlParameter("@p3", System.Data.SqlDbType.VarBinary, 32) { Value = hash };
                    var p4 = new SqlParameter("@p4", DateTime.Now);
                    var p5 = new SqlParameter("@p5", SeedUser);
                    var p6 = new SqlParameter("@p6", PasswordUtils.IteracionesActuales);

                    ctx.Database.ExecuteSqlCommand(sql, p0, p1, p2, p3, p4, p5, p6);
                    System.Diagnostics.Debug.WriteLine("[DatabaseSeeder] EnsureAdminLoginExists: sl_login creado por SQL directo.");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseSeeder] EnsureAdminLoginExists falló: {ex.Message}");
            }
        }

        private static void SeedCatalogos(DataContext ctx)
        {
            // IDs fijos para coincidir con Web.config: Planta=1, Plan_nutricional=2, Centro_costo=5, Proyecto=1, Jerarquia=3
            var now = DateTime.Now;
            var seedUser = "Seed";

            // 1) Planta (id 1) - is_default=1 para que se use desde la BD si el config está dado de baja
            if (!ctx.sl_planta.Any(p => p.id == 1))
            {
                ctx.Database.ExecuteSqlCommand(
                    "SET IDENTITY_INSERT sl_planta ON; " +
                    "INSERT INTO sl_planta (id, nombre, descripcion, createdate, createuser, deletemark, is_default) " +
                    "VALUES (1, 'Planta Principal', 'Planta por defecto', @p0, @p1, 0, 1); " +
                    "SET IDENTITY_INSERT sl_planta OFF;",
                    now, seedUser);
            }

            // 2) Plan nutricional (id 2)
            if (!ctx.sl_plannutricional.Any(p => p.id == 2))
            {
                ctx.Database.ExecuteSqlCommand(
                    "SET IDENTITY_INSERT sl_plannutricional ON; " +
                    "INSERT INTO sl_plannutricional (id, nombre, descripcion, createdate, createuser, deletemark, is_default) " +
                    "VALUES (2, 'Plan estándar', 'Plan por defecto', @p0, @p1, 0, 1); " +
                    "SET IDENTITY_INSERT sl_plannutricional OFF;",
                    now, seedUser);
            }

            // 3) Centro de costo (id 5, planta_id 1)
            if (!ctx.sl_centrodecosto.Any(c => c.id == 5))
            {
                ctx.Database.ExecuteSqlCommand(
                    "SET IDENTITY_INSERT sl_centrodecosto ON; " +
                    "INSERT INTO sl_centrodecosto (id, planta_id, nombre, descripcion, createdate, createuser, deletemark, is_default) " +
                    "VALUES (5, 1, 'Centro costo principal', 'Centro por defecto', @p0, @p1, 0, 1); " +
                    "SET IDENTITY_INSERT sl_centrodecosto OFF;",
                    now, seedUser);
            }

            // 4) Proyecto (id 1, planta_id 1, centrodecosto_id 5)
            if (!ctx.sl_proyecto.Any(p => p.id == 1))
            {
                ctx.Database.ExecuteSqlCommand(
                    "SET IDENTITY_INSERT sl_proyecto ON; " +
                    "INSERT INTO sl_proyecto (id, planta_id, centrodecosto_id, nombre, descripcion, createdate, createuser, deletemark, is_default) " +
                    "VALUES (1, 1, 5, 'Proyecto principal', 'Proyecto por defecto', @p0, @p1, 0, 1); " +
                    "SET IDENTITY_INSERT sl_proyecto OFF;",
                    now, seedUser);
            }

            // 5) Jerarquías: Admin, Cocina, Comensal (is_default=1 para nuevos usuarios), Gerencia
            var jerarquias = new[] { (1, "Admin", 0), (2, "Cocina", 0), (3, "Comensal", 1), (4, "Gerencia", 0) };
            foreach (var (id, nombre, isDefault) in jerarquias)
            {
                if (!ctx.sl_jerarquia.Any(j => j.id == id))
                {
                    ctx.Database.ExecuteSqlCommand(
                        "SET IDENTITY_INSERT sl_jerarquia ON; " +
                        "INSERT INTO sl_jerarquia (id, nombre, descripcion, bonificacion, createdate, createuser, deletemark, is_default) " +
                        "VALUES (" + id + ", @p0, NULL, 0, @p1, @p2, 0, " + (isDefault) + "); " +
                        "SET IDENTITY_INSERT sl_jerarquia OFF;",
                        nombre, now, seedUser);
                }
            }

            ctx.SaveChanges();
        }

        /// <summary>
        /// Carga el usuario administrador (sl_usuario) si no existe. Relacionado con sl_login en SeedLogin.
        /// </summary>
        private static void SeedAdminUsuario(DataContext ctx)
        {
            if (ctx.sl_usuario.Any(u => u.legajo == 1 && !u.deletemark))
                return;

            var usuario = new sl_usuario
            {
                nombre = "Administrador",
                apellido = "Sistema",
                legajo = 1,
                dni = 11111111,
                cuil = "20-11111111-9",
                domicilio = null,
                plannutricional_id = 2,
                planta_id = 1,
                centrodecosto_id = 5,
                proyecto_id = 1,
                jerarquia_id = 1, // Admin
                bonificaciones_invitado = 0,
                bonificaciones = 0,
                createdate = DateTime.Now,
                createuser = SeedUser,
                deletemark = false,
                pedidos = 0,
                origen_datos = "Seed"
            };
            ctx.sl_usuario.Add(usuario);
            ctx.SaveChanges();
        }

        /// <summary>
        /// Carga un valor por defecto para sl_login y su relación con sl_usuario (usuario admin legajo 1).
        /// Si ya existe login "admin", no inserta.
        /// </summary>
        private static void SeedLogin(DataContext ctx)
        {
            if (ctx.sl_login.Any(l => l.username == AdminUsername && !l.deletemark))
                return;

            var usuario = ctx.sl_usuario.FirstOrDefault(u => u.legajo == 1 && !u.deletemark);
            if (usuario == null)
                return; // SeedAdminUsuario no corrió o no hay usuario admin

            var adminPassword = AdminPasswordDefault;
            byte[] salt, hash;
            PasswordUtils.CreateHash(adminPassword, out salt, out hash);

            var login = new sl_login
            {
                usuario_id = usuario.id,
                username = AdminUsername,
                password_salt = salt,
                password_hash = hash,
                password_iteraciones = PasswordUtils.IteracionesActuales,
                activo = true,
                deletemark = false,
                debe_cambiar_clave = false,
                createdate = DateTime.Now,
                createuser = SeedUser
            };

            ctx.sl_login.Add(login);
            ctx.SaveChanges();
        }

        /// <summary>
        /// Asegura que exista el usuario y login SmartTime (username "smarTime") en sl_login.
        /// Solo crea si en Web.config está add key "smarTime" value "true". Si está false o no existe, no crea.
        /// </summary>
        /// <param name="connectionString">Cadena de conexión (null = la por defecto).</param>
        /// <param name="smarTimePassword">Contraseña para el usuario smarTime. Si null o vacía, se usa la por defecto.</param>
        /// <returns>True si se creó el usuario y login; false si no se creó (config false, ya existía o error).</returns>
        public static bool EnsureSmartTimeLoginExists(string connectionString = null, string smarTimePassword = null)
        {
            if (!SmarTimeEstaHabilitado())
                return false;
            try
            {
                using (var ctx = connectionString == null ? new DataContext() : new DataContext(connectionString))
                {
                    return CrearSmartTimeUsuarioYLoginSiNoExiste(ctx, smarTimePassword);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[DatabaseSeeder] EnsureSmartTimeLoginExists: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Indica si la integración smarTime está habilitada en Web.config (key "smarTime" = "true").
        /// Si la clave no existe o no es "true", devuelve false.
        /// </summary>
        private static bool SmarTimeEstaHabilitado()
        {
            var value = ConfigurationManager.AppSettings["smarTime"];
            return !string.IsNullOrWhiteSpace(value) &&
                   "true".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Indica si existe el usuario/login SmartTime (username "smarTime") en sl_login, sin deletemark.
        /// Usado por GET /api/usuario/smarttime/existe.
        /// </summary>
        public static bool ExisteUsuarioSmartTime()
        {
            try
            {
                using (var ctx = new DataContext())
                    return ctx.sl_login.Any(l => l.username == SmartTimeUsername && !l.deletemark);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Crea el usuario por defecto SmartTime (username "smarTime") con la clave indicada o la por defecto.
        /// </summary>
        private static void SeedSmartTimeUsuarioYLogin(DataContext ctx)
        {
            CrearSmartTimeUsuarioYLoginSiNoExiste(ctx, null);
        }

        /// <returns>True si se creó el usuario y login; false si ya existía.</returns>
        private static bool CrearSmartTimeUsuarioYLoginSiNoExiste(DataContext ctx, string smarTimePassword = null)
        {
            if (ctx.sl_login.Any(l => l.username == SmartTimeUsername && !l.deletemark))
                return false;

            // Usuario sl_usuario para SmartTime (legajo 2 si está libre)
            var legajoSmartTime = 2;
            if (ctx.sl_usuario.Any(u => u.legajo == legajoSmartTime && !u.deletemark))
            {
                var maxLegajo = ctx.sl_usuario.Where(u => !u.deletemark).Select(u => (int?)u.legajo).Max() ?? 1;
                legajoSmartTime = maxLegajo + 1;
            }

            // Usuario smarTime siempre con jerarquía 4 (Gerencia) para poder llamar a los endpoints de SmartTime.
            const int JerarquiaIdGerencia = 4;

            var usuario = ctx.sl_usuario.FirstOrDefault(u => u.legajo == legajoSmartTime && !u.deletemark);
            if (usuario == null)
            {
                usuario = new sl_usuario
                {
                    nombre = "SmartTime",
                    apellido = "Sistema",
                    legajo = legajoSmartTime,
                    dni = 22222222,
                    cuil = "20-22222222-5",
                    plannutricional_id = 2,
                    planta_id = 1,
                    centrodecosto_id = 5,
                    proyecto_id = 1,
                    jerarquia_id = JerarquiaIdGerencia,
                    bonificaciones_invitado = 0,
                    bonificaciones = 0,
                    createdate = DateTime.Now,
                    createuser = SeedUser,
                    deletemark = false,
                    pedidos = 0,
                    origen_datos = "Seed"
                };
                ctx.sl_usuario.Add(usuario);
                ctx.SaveChanges();
            }

            var password = !string.IsNullOrWhiteSpace(smarTimePassword) ? smarTimePassword : SmartTimePasswordDefault;
            byte[] salt, hash;
            PasswordUtils.CreateHash(password, out salt, out hash);

            var login = new sl_login
            {
                usuario_id = usuario.id,
                username = SmartTimeUsername,
                password_salt = salt,
                password_hash = hash,
                password_iteraciones = PasswordUtils.IteracionesActuales,
                activo = true,
                deletemark = false,
                debe_cambiar_clave = false,
                createdate = DateTime.Now,
                createuser = SeedUser
            };
            ctx.sl_login.Add(login);
            ctx.SaveChanges();
            return true;
        }

        /// <summary>
        /// Genera una clave solo alfanumérica (A-Z, a-z, 0-9).
        /// </summary>
        private static string GenerarClaveAlfanumerica(int longitud)
        {
            const string mayus = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string minus = "abcdefghjkmnpqrstuvwxyz";
            const string numeros = "23456789";
            var todos = mayus + minus + numeros;
            var resultado = new char[longitud];
            byte[] bytes;
            using (var rng = RandomNumberGenerator.Create())
            {
                bytes = new byte[longitud * 2];
                rng.GetBytes(bytes);
            }
            for (var i = 0; i < longitud; i++)
                resultado[i] = todos[bytes[i * 2 % bytes.Length] % todos.Length];
            return new string(resultado);
        }

        /// <summary>
        /// Genera una clave aleatoria segura (letras mayúsculas, minúsculas, dígitos y al menos un carácter especial).
        /// </summary>
        private static string GenerarClaveAleatoria(int longitud)
        {
            const string mayus = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string minus = "abcdefghjkmnpqrstuvwxyz";
            const string numeros = "23456789";
            const string especial = "!@#$%";
            var todos = mayus + minus + numeros + especial;
            var resultado = new char[longitud];
            byte[] bytes;
            using (var rng = RandomNumberGenerator.Create())
            {
                bytes = new byte[longitud * 2];
                rng.GetBytes(bytes);
            }
            resultado[0] = mayus[bytes[0] % mayus.Length];
            resultado[1] = minus[bytes[1] % minus.Length];
            resultado[2] = numeros[bytes[2] % numeros.Length];
            resultado[3] = especial[bytes[3] % especial.Length];
            for (var i = 4; i < longitud; i++)
                resultado[i] = todos[bytes[i * 2 % bytes.Length] % todos.Length];
            return new string(resultado);
        }

        private const string SeedUser = "Seed";
    }
}
