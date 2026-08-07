using System;
using System.Configuration;
using System.Data.SqlClient;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Configuration;
using System.Web.Http;
using smartlunch_api.App_Start;

namespace smartlunch_api.Controllers
{
    /// <summary>
    /// Configuración inicial: crear usuario administrador por defecto (solo contraseña; usuario = "admin").
    /// Abrir en el navegador: http://localhost:puerto/Default.html o http://localhost:puerto/api/Setup
    /// </summary>
    [AllowAnonymous]
    [RoutePrefix("api/Setup")]
    public class SetupController : ApiController
    {
        /// <summary>
        /// Devuelve la página HTML para configurar la contraseña del administrador.
        /// </summary>
        [HttpGet]
        [Route("")]
        public HttpResponseMessage GetPagina()
        {
            var html = GetHtmlSetup();
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(html, Encoding.UTF8, "text/html")
            };
            return resp;
        }

        /// <summary>
        /// Crea o actualiza el usuario administrador con la contraseña indicada.
        /// Solo permite configurar si aún no hay admin con contraseña real (o tiene placeholder).
        /// </summary>
        /// <param name="model">Objeto con propiedad Password.</param>
        [HttpPost]
        [Route("CrearAdmin")]
        public HttpResponseMessage CrearAdmin([FromBody] SetupCrearAdminRequest model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.Password))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    mensaje = "La contraseña es obligatoria."
                });
            }

            var ok = DatabaseSeeder.CrearOActualizarAdmin(model.Password.Trim(), out var mensaje);
            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                ok,
                mensaje
            });
        }

        /// <summary>
        /// Indica si la base de datos existe y si el administrador ya está configurado.
        /// El front solo muestra la configuración inicial cuando baseDeDatosExiste es false; si la BD existe, redirige a Swagger.
        /// </summary>
        [HttpGet]
        [Route("Estado")]
        public HttpResponseMessage Estado()
        {
            var csEstado = ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
            var databaseName = string.IsNullOrEmpty(csEstado) ? "smartlunch" : new SqlConnectionStringBuilder(csEstado).InitialCatalog.Trim();
            if (string.IsNullOrEmpty(databaseName)) databaseName = "smartlunch";
            var baseDeDatosExiste = DatabaseInitializer.DatabaseExists(databaseName);
            var yaConfigurado = DatabaseSeeder.AdminYaConfigurado();
            var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            var redirectUrl = baseUrl.TrimEnd('/') + "/swagger/ui/index";
            var smarTimeHabilitado = "true".Equals((ConfigurationManager.AppSettings["smarTime"] ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                yaConfigurado,
                baseDeDatosExiste,
                usuarioAdmin = DatabaseSeeder.AdminUsername,
                databaseNameDefault = databaseName,
                smarTimeHabilitado,
                redirectUrl
            });
        }

        /// <summary>
        /// Setup completo: si la BD no existe, la crea, ejecuta el script, seeders y crea el admin con la contraseña indicada.
        /// Si la BD ya existe, no hace nada. Siempre devuelve redirectUrl a Swagger usando la URL del servidor.
        /// El nombre de la base de datos se toma solo de Web.config (no se puede cambiar desde la página).
        /// </summary>
        [HttpPost]
        [Route("Completo")]
        public HttpResponseMessage Completo([FromBody] SetupCompletoRequest model)
        {
            var baseUrl = Request.RequestUri.GetLeftPart(UriPartial.Authority);
            var redirectUrl = baseUrl.TrimEnd('/') + "/swagger/ui/index";

            if (model == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new { ok = false, mensaje = "Datos obligatorios.", redirectUrl });
            }

            if (string.IsNullOrWhiteSpace(model.Password) || model.Password.Length < 6)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    mensaje = "La contraseña del administrador debe tener al menos 6 caracteres.",
                    redirectUrl
                });
            }

            var smarTimeHabilitado = "true".Equals((ConfigurationManager.AppSettings["smarTime"] ?? "").Trim(), StringComparison.OrdinalIgnoreCase);
            if (smarTimeHabilitado && string.IsNullOrWhiteSpace(model.SmarTimePassword))
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    mensaje = "La integración SmartTime está habilitada. Ingresá la contraseña para el usuario smarTime (mínimo 6 caracteres).",
                    redirectUrl
                });
            }
            if (smarTimeHabilitado && model.SmarTimePassword != null && model.SmarTimePassword.Length < 6)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest, new
                {
                    ok = false,
                    mensaje = "La contraseña del usuario SmartTime debe tener al menos 6 caracteres.",
                    redirectUrl
                });
            }

            var connectionString = ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
            var databaseName = string.IsNullOrEmpty(connectionString) ? "" : new SqlConnectionStringBuilder(connectionString).InitialCatalog.Trim();
            if (string.IsNullOrEmpty(databaseName))
                databaseName = "smartlunch";
            if (string.IsNullOrEmpty(connectionString))
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    ok = false,
                    mensaje = "No hay cadena de conexión configurada (DataContext).",
                    redirectUrl
                });
            }

            // Si la base de datos ya existe, asegurar usuario SmartTime (smarTime) en sl_login y redirigir a Swagger
            if (DatabaseInitializer.DatabaseExists(databaseName))
            {
                var builderExistente = new SqlConnectionStringBuilder(connectionString) { InitialCatalog = databaseName };
                DatabaseSeeder.EnsureSmartTimeLoginExists(builderExistente.ConnectionString);
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    ok = true,
                    mensaje = "La base de datos ya existe. Redirigiendo a Swagger…",
                    redirectUrl
                });
            }

            var builder = new SqlConnectionStringBuilder(connectionString);
            builder.InitialCatalog = databaseName;
            var connectionStringNueva = builder.ConnectionString;

            // 1) Crear BD y ejecutar script de tablas
            var errorSchema = DatabaseInitializer.EnsureDatabaseAndSchema(databaseName);
            if (errorSchema != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    ok = false,
                    mensaje = "Error al crear la base de datos: " + errorSchema,
                    redirectUrl
                });
            }

            // 2) Seed (catálogos + usuario admin sin login aún — el script de tablas ya deja el login
            // admin con salt/hash placeholder; SeedIfEmpty no lo toca porque los catálogos ya existen)
            DatabaseSeeder.SeedIfEmpty(connectionStringNueva);

            // 3) Crear/actualizar login admin con la contraseña indicada. El admin ya existe con salt
            // placeholder (puesto por el script de tablas), así que esto reemplaza ese placeholder por
            // la contraseña real que se tipeó en el formulario.
            var okAdmin = DatabaseSeeder.CrearOActualizarAdmin(model.Password.Trim(), connectionStringNueva, out var mensajeAdmin);
            if (!okAdmin)
            {
                return Request.CreateResponse(HttpStatusCode.OK, new
                {
                    ok = false,
                    mensaje = mensajeAdmin,
                    redirectUrl
                });
            }

            // 3b) Asegurar usuario SmartTime (smarTime) en sl_login, con la clave indicada si smarTime está habilitado
            var smarTimePassword = smarTimeHabilitado && !string.IsNullOrWhiteSpace(model.SmarTimePassword)
                ? model.SmarTimePassword.Trim()
                : null;
            DatabaseSeeder.EnsureSmartTimeLoginExists(connectionStringNueva, smarTimePassword);

            // 4) Actualizar Web.config para que la app use esta BD
            try
            {
                var config = WebConfigurationManager.OpenWebConfiguration("~");
                if (config.AppSettings.Settings["DatabaseName"] != null)
                    config.AppSettings.Settings["DatabaseName"].Value = databaseName;
                else
                    config.AppSettings.Settings.Add("DatabaseName", databaseName);

                var conn = config.ConnectionStrings.ConnectionStrings["DataContext"];
                if (conn != null)
                    conn.ConnectionString = connectionStringNueva;

                config.Save(ConfigurationSaveMode.Modified);
                ConfigurationManager.RefreshSection("appSettings");
                ConfigurationManager.RefreshSection("connectionStrings");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[Setup] No se pudo actualizar Web.config: " + ex.Message);
            }

            return Request.CreateResponse(HttpStatusCode.OK, new
            {
                ok = true,
                mensaje = "Base de datos creada correctamente. Redirigiendo a Swagger…",
                redirectUrl
            });
        }

        private static string GetHtmlSetup()
        {
            return @"<!DOCTYPE html>
<html lang=""es"">
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1"" />
    <title>Configuración inicial - SmartLunch</title>
    <style>
        * { box-sizing: border-box; }
        body { font-family: 'Segoe UI', Tahoma, sans-serif; margin: 0; padding: 2rem; background: #f0f2f5; min-height: 100vh; display: flex; align-items: center; justify-content: center; }
        .card { background: #fff; border-radius: 8px; box-shadow: 0 2px 8px rgba(0,0,0,.1); padding: 2rem; max-width: 420px; width: 100%; }
        h1 { margin: 0 0 .5rem 0; font-size: 1.5rem; color: #1a1a1a; }
        .sub { color: #666; font-size: 0.9rem; margin-bottom: 1.5rem; }
        label { display: block; margin-bottom: 0.35rem; font-weight: 600; color: #333; }
        input[type=""password""] { width: 100%; padding: 0.6rem 0.75rem; border: 1px solid #ccc; border-radius: 4px; font-size: 1rem; margin-bottom: 1rem; }
        input[type=""password""]:focus { outline: none; border-color: #1976d2; }
        button { width: 100%; padding: 0.75rem; background: #1976d2; color: #fff; border: none; border-radius: 4px; font-size: 1rem; font-weight: 600; cursor: pointer; }
        button:hover { background: #1565c0; }
        button:disabled { background: #9e9e9e; cursor: not-allowed; }
        .msg { margin-top: 1rem; padding: 0.75rem; border-radius: 4px; font-size: 0.9rem; display: none; }
        .msg.error { background: #ffebee; color: #c62828; display: block; }
        .msg.ok { background: #e8f5e9; color: #2e7d32; display: block; }
        .usuario { background: #e3f2fd; padding: 0.5rem 0.75rem; border-radius: 4px; margin-bottom: 1rem; font-size: 0.9rem; color: #1565c0; }
    </style>
</head>
<body>
    <div class=""card"">
        <h1>Configuración inicial</h1>
        <p class=""sub"">Cree la contraseña del usuario administrador. El nombre de usuario será <strong>admin</strong>.</p>
        <div class=""usuario"">Usuario: <strong>admin</strong></div>
        <form id=""f"">
            <label for=""pwd"">Contraseña (mínimo 6 caracteres)</label>
            <input type=""password"" id=""pwd"" name=""Password"" required minlength=""6"" placeholder=""Ingrese la contraseña"" autocomplete=""new-password"" />
            <label for=""pwd2"">Repetir contraseña</label>
            <input type=""password"" id=""pwd2"" name=""Password2"" required minlength=""6"" placeholder=""Repita la contraseña"" autocomplete=""new-password"" />
            <button type=""submit"" id=""btn"">Crear administrador</button>
        </form>
        <div id=""msg"" class=""msg""></div>
    </div>
    <script>
        (function() {
            var f = document.getElementById('f');
            var pwd = document.getElementById('pwd');
            var pwd2 = document.getElementById('pwd2');
            var msg = document.getElementById('msg');
            var btn = document.getElementById('btn');
            function showMsg(text, isError) {
                msg.textContent = text;
                msg.className = 'msg ' + (isError ? 'error' : 'ok');
                msg.style.display = 'block';
            }
            f.addEventListener('submit', function(e) {
                e.preventDefault();
                if (pwd.value !== pwd2.value) {
                    showMsg('Las contraseñas no coinciden.', true);
                    return;
                }
                if (pwd.value.length < 6) {
                    showMsg('La contraseña debe tener al menos 6 caracteres.', true);
                    return;
                }
                btn.disabled = true;
                msg.style.display = 'none';
                var base = window.location.origin;
                fetch(base + '/api/Setup/CrearAdmin', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ Password: pwd.value })
                })
                .then(function(r) { return r.json(); })
                .then(function(d) {
                    if (d.ok) {
                        showMsg(d.mensaje || 'Administrador creado. Ya puede iniciar sesión.', false);
                        f.reset();
                    } else {
                        showMsg(d.mensaje || 'Error al crear el administrador.', true);
                    }
                })
                .catch(function() {
                    showMsg('Error de conexión con el servidor.', true);
                })
                .finally(function() { btn.disabled = false; });
            });
        })();
    </script>
</body>
</html>";
        }
    }

    /// <summary>
    /// Request para crear/actualizar el admin (solo contraseña).
    /// </summary>
    public class SetupCrearAdminRequest
    {
        public string Password { get; set; }
    }

    /// <summary>
    /// Request para setup completo: contraseña del admin y opcionalmente del usuario SmartTime.
    /// </summary>
    public class SetupCompletoRequest
    {
        /// <summary>Nombre de la base de datos (por defecto smartlunch).</summary>
        public string DatabaseName { get; set; }

        /// <summary>Contraseña del usuario administrador (admin). Obligatoria.</summary>
        public string Password { get; set; }

        /// <summary>Contraseña del usuario SmartTime (smarTime). Obligatoria si smarTime está habilitado en Web.config.</summary>
        public string SmarTimePassword { get; set; }
    }
}
