using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace smartlunch_api.Filters
{
    /// <summary>
    /// Filtro de Rate Limiting para proteger endpoints contra ataques de fuerza bruta.
    /// Usa ConcurrentDictionary para alta concurrencia (150-200+ peticiones simultáneas).
    /// </summary>
    public class RateLimitAttribute : ActionFilterAttribute
    {
        // Por IP
        private static readonly ConcurrentDictionary<string, List<DateTime>> _attempts =
            new ConcurrentDictionary<string, List<DateTime>>();

        // Por usuario (username) — protege contra ataque distribuido contra una misma cuenta
        private static readonly ConcurrentDictionary<string, List<DateTime>> _attemptsByUsername =
            new ConcurrentDictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);

        private static readonly object _cleanupLock = new object();
        private static DateTime _lastCleanup = DateTime.UtcNow;

        private readonly int _maxAttempts;
        private readonly int _windowMinutes;
        private readonly int _blockMinutes;
        private readonly int _cleanupIntervalMinutes;

        public RateLimitAttribute()
        {
            // Leer configuración desde Web.config
            _maxAttempts = int.Parse(ConfigurationManager.AppSettings["RateLimitMaxAttempts"] ?? "5");
            _windowMinutes = int.Parse(ConfigurationManager.AppSettings["RateLimitWindowMinutes"] ?? "15");
            _blockMinutes = int.Parse(ConfigurationManager.AppSettings["RateLimitBlockMinutes"] ?? "30");
            _cleanupIntervalMinutes = int.Parse(ConfigurationManager.AppSettings["RateLimitCleanupIntervalMinutes"] ?? "60");
        }

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            var ip = GetClientIpAddress(actionContext);
            var now = DateTime.UtcNow;

            // Limpieza periódica de datos antiguos (cada X minutos)
            CleanupOldDataIfNeeded(now);

            // Obtener o crear lista de intentos para esta IP
            var attempts = _attempts.GetOrAdd(ip, _ => new List<DateTime>());

            // Lock solo para esta IP específica (permite procesamiento paralelo de otras IPs)
            lock (attempts)
            {
                // Limpiar intentos fuera de la ventana de tiempo
                attempts.RemoveAll(t => (now - t).TotalMinutes > _windowMinutes);

                // Verificar si hay intentos bloqueados recientes
                var recentBlockedAttempts = attempts.Count(a => (now - a).TotalMinutes <= _blockMinutes);
                
                if (recentBlockedAttempts >= _maxAttempts)
                {
                    // Calcular tiempo restante de bloqueo
                    var oldestAttempt = attempts.OrderBy(a => a).FirstOrDefault();
                    var timeRemaining = oldestAttempt != null 
                        ? (int)(_blockMinutes - (now - oldestAttempt).TotalMinutes)
                        : _blockMinutes;

                    actionContext.Response = actionContext.Request.CreateResponse(
                        (HttpStatusCode)429, // TooManyRequests no existe en .NET Framework 4.8.1
                        new 
                        { 
                            error = "Demasiados intentos fallidos. Por favor, intente nuevamente más tarde.",
                            retryAfter = timeRemaining > 0 ? timeRemaining : _blockMinutes
                        });

                    // Agregar header Retry-After (estándar HTTP)
                    actionContext.Response.Headers.Add("Retry-After", timeRemaining.ToString());
                    
                    return;
                }
            }

            base.OnActionExecuting(actionContext);
        }

        /// <summary>
        /// Registra un intento fallido para una IP específica.
        /// Debe llamarse cuando un intento de login falla.
        /// </summary>
        public static void RecordFailedAttempt(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return;

            var now = DateTime.UtcNow;
            var attempts = _attempts.GetOrAdd(ip, _ => new List<DateTime>());

            lock (attempts)
            {
                attempts.Add(now);
                
                // Limpiar intentos antiguos automáticamente
                attempts.RemoveAll(t => (now - t).TotalMinutes > 30); // Ventana más amplia para limpieza
            }
        }

        /// <summary>
        /// Limpia intentos exitosos para una IP (cuando el login es exitoso).
        /// </summary>
        public static void ClearAttempts(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip))
                return;

            _attempts.TryRemove(ip, out _);
        }

        // ---------- Rate limit por usuario (username) ----------

        /// <summary>
        /// Comprueba si el usuario (username) está bloqueado por demasiados intentos fallidos.
        /// Debe llamarse al inicio del login, cuando ya se tiene el username del body.
        /// </summary>
        public static bool IsUsernameBlocked(string username, out int retryAfterMinutes)
        {
            retryAfterMinutes = 0;
            if (string.IsNullOrWhiteSpace(username))
                return false;

            var maxAttempts = int.Parse(ConfigurationManager.AppSettings["RateLimitMaxAttempts"] ?? "5");
            var windowMinutes = int.Parse(ConfigurationManager.AppSettings["RateLimitWindowMinutes"] ?? "15");
            var blockMinutes = int.Parse(ConfigurationManager.AppSettings["RateLimitBlockMinutes"] ?? "10");

            var key = username.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;
            var attempts = _attemptsByUsername.GetOrAdd(key, _ => new List<DateTime>());

            lock (attempts)
            {
                attempts.RemoveAll(t => (now - t).TotalMinutes > windowMinutes);
                var recentCount = attempts.Count(a => (now - a).TotalMinutes <= blockMinutes);

                if (recentCount < maxAttempts)
                    return false;

                var oldestAttempt = attempts.OrderBy(a => a).FirstOrDefault();
                retryAfterMinutes = oldestAttempt != null
                    ? Math.Max(0, (int)(blockMinutes - (now - oldestAttempt).TotalMinutes))
                    : blockMinutes;
                return true;
            }
        }

        /// <summary>
        /// Registra un intento fallido para un usuario (username).
        /// </summary>
        public static void RecordFailedAttemptByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            var blockMinutes = int.Parse(ConfigurationManager.AppSettings["RateLimitBlockMinutes"] ?? "10");
            var key = username.Trim().ToLowerInvariant();
            var now = DateTime.UtcNow;
            var attempts = _attemptsByUsername.GetOrAdd(key, _ => new List<DateTime>());

            lock (attempts)
            {
                attempts.Add(now);
                attempts.RemoveAll(t => (now - t).TotalMinutes > Math.Max(blockMinutes, 30));
            }
        }

        /// <summary>
        /// Limpia intentos fallidos del usuario cuando el login es exitoso.
        /// </summary>
        public static void ClearAttemptsByUsername(string username)
        {
            if (string.IsNullOrWhiteSpace(username))
                return;

            var key = username.Trim().ToLowerInvariant();
            _attemptsByUsername.TryRemove(key, out _);
        }

        /// <summary>
        /// Obtiene la IP del cliente desde la petición HTTP. Prioriza la IP real del socket
        /// (no falsificable) por sobre los headers X-Forwarded-For / X-Real-IP, que cualquier
        /// cliente puede mandar con el valor que quiera. Esos headers solo se usan si
        /// "TrustProxyHeaders" está explícitamente en true en appSettings (para cuando la app
        /// esté detrás de un proxy/load balancer de confianza que los sobrescribe).
        /// </summary>
        private string GetClientIpAddress(HttpActionContext actionContext)
        {
            var request = actionContext.Request;

            string ip = null;

            if (request.Properties.ContainsKey("MS_HttpContext"))
            {
                var httpContext = request.Properties["MS_HttpContext"] as System.Web.HttpContextBase;
                if (httpContext != null)
                {
                    ip = httpContext.Request.UserHostAddress;
                }
            }

            if (string.IsNullOrWhiteSpace(ip))
            {
                ip = request.Properties.ContainsKey("MS_OwinContext")
                    ? (request.Properties["MS_OwinContext"] as Microsoft.Owin.IOwinContext)?.Request?.RemoteIpAddress
                    : null;
            }

            var confiarEnProxy = "true".Equals(
                ConfigurationManager.AppSettings["TrustProxyHeaders"],
                StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(ip) || confiarEnProxy)
            {
                var forwardedIp = request.Headers.Contains("X-Forwarded-For")
                    ? request.Headers.GetValues("X-Forwarded-For").FirstOrDefault()
                    : null;

                if (string.IsNullOrWhiteSpace(forwardedIp))
                {
                    forwardedIp = request.Headers.Contains("X-Real-IP")
                        ? request.Headers.GetValues("X-Real-IP").FirstOrDefault()
                        : null;
                }

                // Solo reemplazar la IP real por el header si confiamos en el proxy, o como
                // último recurso si no pudimos obtener la IP real de ninguna otra forma.
                if (!string.IsNullOrWhiteSpace(forwardedIp) && (confiarEnProxy || string.IsNullOrWhiteSpace(ip)))
                {
                    ip = forwardedIp.Split(',')[0].Trim();
                }
            }

            // Si no se puede obtener, usar un valor por defecto
            return ip ?? "unknown";
        }

        /// <summary>
        /// Limpia datos antiguos periódicamente para evitar crecimiento excesivo de memoria.
        /// </summary>
        private void CleanupOldDataIfNeeded(DateTime now)
        {
            // Solo hacer limpieza cada X minutos (no en cada request)
            if ((now - _lastCleanup).TotalMinutes < _cleanupIntervalMinutes)
                return;

            lock (_cleanupLock)
            {
                // Doble verificación (double-check locking pattern)
                if ((now - _lastCleanup).TotalMinutes < _cleanupIntervalMinutes)
                    return;

                _lastCleanup = now;

                // Limpiar IPs que no han tenido actividad en las últimas 24 horas
                var cutoffTime = now.AddHours(-24);
                var keysToRemove = new List<string>();

                foreach (var kvp in _attempts)
                {
                    lock (kvp.Value)
                    {
                        // Si todos los intentos son más antiguos que 24 horas, eliminar la entrada
                        if (kvp.Value.All(t => t < cutoffTime))
                        {
                            keysToRemove.Add(kvp.Key);
                        }
                    }
                }

                foreach (var key in keysToRemove)
                {
                    _attempts.TryRemove(key, out _);
                }

                // Limpiar usuarios sin actividad reciente (misma ventana 24 h)
                keysToRemove.Clear();
                foreach (var kvp in _attemptsByUsername)
                {
                    lock (kvp.Value)
                    {
                        if (kvp.Value.All(t => t < cutoffTime))
                            keysToRemove.Add(kvp.Key);
                    }
                }
                foreach (var key in keysToRemove)
                {
                    _attemptsByUsername.TryRemove(key, out _);
                }
            }
        }

        /// <summary>
        /// Obtiene estadísticas de rate limiting para una IP (útil para debugging).
        /// </summary>
        public static RateLimitStats GetStats(string ip)
        {
            if (string.IsNullOrWhiteSpace(ip) || !_attempts.TryGetValue(ip, out var attempts))
            {
                return new RateLimitStats { Attempts = 0, IsBlocked = false };
            }

            var now = DateTime.UtcNow;
            lock (attempts)
            {
                var recentAttempts = attempts.Count(a => (now - a).TotalMinutes <= 15);
                var maxAttempts = int.Parse(ConfigurationManager.AppSettings["RateLimitMaxAttempts"] ?? "5");
                
                return new RateLimitStats
                {
                    Attempts = recentAttempts,
                    IsBlocked = recentAttempts >= maxAttempts,
                    LastAttempt = attempts.OrderByDescending(a => a).FirstOrDefault()
                };
            }
        }
    }

    /// <summary>
    /// Estadísticas de rate limiting para una IP.
    /// </summary>
    public class RateLimitStats
    {
        public int Attempts { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? LastAttempt { get; set; }
    }
}

