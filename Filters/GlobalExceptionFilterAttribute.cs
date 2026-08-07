using System;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Web.Http.Filters;
using smartlunch_api.Services;

namespace smartlunch_api.Filters
{
    /// <summary>
    /// Filtro global para capturar y registrar todas las excepciones no manejadas.
    /// En producción no se incluye StackTrace en los logs (configurable con IncludeStackTraceInLogs).
    /// </summary>
    public class GlobalExceptionFilterAttribute : ExceptionFilterAttribute
    {
        private readonly ILoggerService _logger;

        public GlobalExceptionFilterAttribute(ILoggerService logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public override void OnException(HttpActionExecutedContext context)
        {
            var request = context.Request;
            var exception = context.Exception;

            // Obtener información del request
            var controllerName = context.ActionContext?.ControllerContext?.ControllerDescriptor?.ControllerName ?? "Unknown";
            var actionName = context.ActionContext?.ActionDescriptor?.ActionName ?? "Unknown";
            var method = request.Method?.Method ?? "Unknown";
            var uri = request.RequestUri?.ToString() ?? "Unknown";
            var userAgent = request.Headers?.UserAgent?.ToString() ?? "Unknown";
            var clientIp = GetClientIpAddress(request);

            // Incluir StackTrace en logs solo en DEV o si el flag está en true. En producción usar IncludeStackTraceInLogs = false.
            var flag = ConfigurationManager.AppSettings["IncludeStackTraceInLogs"]?.Trim() ?? "";
            var includeStackTrace = !"false".Equals(flag, StringComparison.OrdinalIgnoreCase) &&
                (IsDevelopmentEnvironment() || "true".Equals(flag, StringComparison.OrdinalIgnoreCase));

            var logData = new
            {
                Controller = controllerName,
                Action = actionName,
                Method = method,
                Uri = uri,
                UserAgent = userAgent,
                ClientIp = clientIp,
                ExceptionType = exception.GetType().Name,
                ExceptionMessage = exception.Message,
                StackTrace = includeStackTrace ? exception.StackTrace : null
            };

            _logger.LogError(
                exception,
                "Excepción no manejada en {Controller}.{Action}",
                controllerName,
                actionName,
                logData
            );

            // Conflicto / duplicado (ej. "Ya existe un usuario con el mismo DNI") -> 409 Conflict
            bool esConflicto = exception.Message.Contains("Ya existe");
            if (esConflicto)
            {
                var errorResponse = new
                {
                    success = false,
                    error = exception.Message,
                    message = exception.Message
                };

                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.Conflict,
                    errorResponse
                );
                return;
            }

            // Error de validación de negocio (mensajes en castellano) -> 400 Bad Request
            bool esErrorValidacion = exception.Message.Contains("superpone") ||
                                    exception.Message.Contains("obligatorio") ||
                                    exception.Message.Contains("debe ser") ||
                                    exception.Message.Contains("no encontrado") ||
                                    exception.Message.Contains("no puede") ||
                                    exception.Message.Contains("inválido") ||
                                    exception.Message.Contains("Datos inválidos") ||
                                    exception.Message.Contains("rango horario");

            if (esErrorValidacion)
            {
                var errorResponse = new
                {
                    success = false,
                    error = exception.Message,
                    message = exception.Message
                };

                context.Response = context.Request.CreateResponse(
                    HttpStatusCode.BadRequest,
                    errorResponse
                );
                return;
            }

            // Crear respuesta de error para errores inesperados.
            // Detalle (mensaje, exceptionType, stackTrace) solo en compilación DEBUG; en Release nunca se envía al cliente.
            var includeDetailsInResponse = IncludeErrorDetailsInResponse;
            var errorResponseInterno = new
            {
                success = false,
                error = "Ha ocurrido un error interno en el servidor.",
                message = includeDetailsInResponse
                    ? exception.Message
                    : "Por favor, contacte al administrador del sistema.",
                details = includeDetailsInResponse
                    ? new
                    {
                        exceptionType = exception.GetType().Name,
                        message = exception.Message,
                        stackTrace = exception.StackTrace
                    }
                    : (object)null
            };

            context.Response = context.Request.CreateResponse(
                HttpStatusCode.InternalServerError,
                errorResponseInterno
            );
        }

        private string GetClientIpAddress(HttpRequestMessage request)
        {
            if (request.Properties.ContainsKey("MS_HttpContext"))
            {
                return ((System.Web.HttpContextWrapper)request.Properties["MS_HttpContext"]).Request.UserHostAddress;
            }
            if (System.Web.HttpContext.Current != null)
            {
                return System.Web.HttpContext.Current.Request.UserHostAddress;
            }
            return "Unknown";
        }

        /// <summary>
        /// Solo true en compilación DEBUG. Usado para decidir si incluir detalles de excepción en la respuesta 500 al cliente.
        /// En Release nunca se envían exceptionType, message ni stackTrace al cliente.
        /// </summary>
        private static readonly bool IncludeErrorDetailsInResponse =
#if DEBUG
            true;
#else
            false;
#endif

        /// <summary>
        /// True en DEBUG o cuando el host es localhost (desarrollo local). Usado para logs y otros comportamientos de desarrollo.
        /// No se usa para enviar detalles al cliente en 500; eso depende solo de IncludeErrorDetailsInResponse.
        /// </summary>
        private bool IsDevelopmentEnvironment()
        {
#if DEBUG
            return true;
#else
            try
            {
                var httpContext = System.Web.HttpContext.Current;
                if (httpContext?.Request?.Url?.Host != null)
                {
                    var host = httpContext.Request.Url.Host.ToLower();
                    return host == "localhost" || host == "127.0.0.1";
                }
            }
            catch { }
            return false;
#endif
        }
    }
}

