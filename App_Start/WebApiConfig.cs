using Swashbuckle.Application;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Web.Http;
using System.Web.Http.Cors;
using Serilog;
using smartlunch_api.Services;
using smartlunch_api.Service;
using smartlunch_api.Handlers;

namespace smartlunch_api
{
    public static class WebApiConfig
    {
        public static void Register(HttpConfiguration config)
        {
            // =======================================================================================
            // Configuración de Logging Estructurado (Serilog)
            // =======================================================================================
            ConfigureSerilog();

            // Rutas con atributos
            config.MapHttpAttributeRoutes();

            // Ruta por defecto
            config.Routes.MapHttpRoute(
                name: "DefaultApi",
                routeTemplate: "api/{controller}/{id}",
                defaults: new { id = RouteParameter.Optional }
            );

            // CORS - Configuración desde Web.config
            var allowedOrigins = ConfigurationManager.AppSettings["CorsAllowedOrigins"];
            
            // Si no está configurado, usar solo localhost para desarrollo
            if (string.IsNullOrWhiteSpace(allowedOrigins))
            {
                allowedOrigins = "http://localhost:8000,http://localhost:4200";
            }

            var allowedHeaders = ConfigurationManager.AppSettings["CorsAllowedHeaders"] 
                ?? "Content-Type,Authorization";
            
            var allowedMethods = ConfigurationManager.AppSettings["CorsAllowedMethods"] 
                ?? "GET,POST,PUT,DELETE,OPTIONS";

            var cors = new EnableCorsAttribute(
                origins: allowedOrigins,
                headers: allowedHeaders,
                methods: allowedMethods
            );
            
            config.EnableCors(cors);

            // =======================================================================================
            // RequestId: correlación de peticiones y logs (X-Request-Id en respuesta + Serilog)
            // =======================================================================================
            config.MessageHandlers.Add(new Handlers.RequestIdHandler());

            // =======================================================================================
            // MessageHandler para interceptar respuestas 401 y devolver mensajes descriptivos
            // =======================================================================================
            config.MessageHandlers.Add(new UnauthorizedResponseHandler());

            // =======================================================================================
            // Filtro Global de Excepciones
            // =======================================================================================
            var loggerService = new SerilogLoggerService(Log.Logger);
            config.Filters.Add(new Filters.GlobalExceptionFilterAttribute(loggerService));

            // =======================================================================================
            // Filtro Global de Validación (Opcional - se puede aplicar por acción también)
            // =======================================================================================
            // Descomentar la siguiente línea si quieres validación automática en TODAS las acciones
            // config.Filters.Add(new Filters.ValidateModelAttribute());
        }

        private static void ConfigureSerilog()
        {
            // Obtener configuración desde Web.config
            var logPath = ConfigurationManager.AppSettings["LogPath"] ?? "~/App_Data/Logs";
            var logLevel = ConfigurationManager.AppSettings["LogLevel"] ?? "Information";
            
            // Resolver ruta física
            if (logPath.StartsWith("~/"))
            {
                logPath = System.Web.Hosting.HostingEnvironment.MapPath(logPath);
            }

            // Crear directorio si no existe
            if (!string.IsNullOrEmpty(logPath) && !Directory.Exists(logPath))
            {
                Directory.CreateDirectory(logPath);
            }

            // Configurar Serilog
            var loggerConfig = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
                .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .Enrich.WithProperty("Application", "SmartLunchAPI")
                .Enrich.WithProperty("Environment", IsDevelopmentEnvironment() ? "Development" : "Production");

            // Configurar nivel de log
            switch (logLevel.ToLower())
            {
                case "debug":
                    loggerConfig.MinimumLevel.Debug();
                    break;
                case "information":
                    loggerConfig.MinimumLevel.Information();
                    break;
                case "warning":
                    loggerConfig.MinimumLevel.Warning();
                    break;
                case "error":
                    loggerConfig.MinimumLevel.Error();
                    break;
                default:
                    loggerConfig.MinimumLevel.Information();
                    break;
            }

            // Agregar sinks (destinos de log)
            if (!string.IsNullOrEmpty(logPath))
            {
                // Archivo con rotación diaria
                loggerConfig.WriteTo.File(
                    Path.Combine(logPath, "smartlunch-api-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 30,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [RequestId:{RequestId}] {Message:lj}{NewLine}{Exception}",
                    shared: true
                );
            }

            // Console (solo en desarrollo)
            if (IsDevelopmentEnvironment())
            {
                loggerConfig.WriteTo.Console(
                    outputTemplate: "{Timestamp:HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}"
                );
            }

            // Crear y asignar logger global
            Log.Logger = loggerConfig.CreateLogger();

            // Log inicial
            Log.Information("Serilog configurado correctamente. LogPath: {LogPath}, LogLevel: {LogLevel}", logPath, logLevel);
        }

        /// <summary>
        /// Determina si la aplicación está ejecutándose en un entorno de desarrollo.
        /// En .NET Framework, verificamos si está en modo DEBUG o si el host es localhost.
        /// </summary>
        private static bool IsDevelopmentEnvironment()
        {
#if DEBUG
            return true;
#else
            // En producción, verificar si estamos en localhost (para desarrollo local)
            try
            {
                var httpContext = System.Web.HttpContext.Current;
                if (httpContext != null && httpContext.Request != null)
                {
                    var host = httpContext.Request.Url?.Host?.ToLower();
                    return host == "localhost" || host == "127.0.0.1";
                }
            }
            catch
            {
                // Si no hay HttpContext, asumimos producción
            }
            return false;
#endif
        }
    }
}
