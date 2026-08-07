using System;
using Serilog;
using Serilog.Context;
using Serilog.Events;

namespace smartlunch_api.Services
{
    // ============================================
    // IMPLEMENTACIÓN DE LOGGING CON SERILOG
    // ============================================
    public class SerilogLoggerService : ILoggerService
    {
        private readonly ILogger _logger;

        public SerilogLoggerService(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void LogDebug(string message, params object[] args)
        {
            if (_logger.IsEnabled(LogEventLevel.Debug))
            {
                _logger.Debug(message, args);
            }
        }

        public void LogInformation(string message, params object[] args)
        {
            if (_logger.IsEnabled(LogEventLevel.Information))
            {
                _logger.Information(message, args);
            }
        }

        public void LogWarning(string message, params object[] args)
        {
            if (_logger.IsEnabled(LogEventLevel.Warning))
            {
                _logger.Warning(message, args);
            }
        }

        public void LogError(string message, Exception exception = null, params object[] args)
        {
            if (_logger.IsEnabled(LogEventLevel.Error))
            {
                if (exception != null)
                {
                    _logger.Error(exception, message, args);
                }
                else
                {
                    _logger.Error(message, args);
                }
            }
        }

        public void LogError(Exception exception, string message, params object[] args)
        {
            LogError(message, exception, args);
        }

        public void LogCritical(string message, Exception exception = null, params object[] args)
        {
            if (_logger.IsEnabled(LogEventLevel.Fatal))
            {
                if (exception != null)
                {
                    _logger.Fatal(exception, message, args);
                }
                else
                {
                    _logger.Fatal(message, args);
                }
            }
        }

        // ============================================
        // LOGGING ESTRUCTURADO CON PROPIEDADES
        // ============================================

        public void LogDebug(string message, object properties)
        {
            if (_logger.IsEnabled(LogEventLevel.Debug))
            {
                using (LogContext.PushProperty("Properties", properties, true))
                {
                    _logger.Debug(message);
                }
            }
        }

        public void LogInformation(string message, object properties)
        {
            if (_logger.IsEnabled(LogEventLevel.Information))
            {
                using (LogContext.PushProperty("Properties", properties, true))
                {
                    _logger.Information(message);
                }
            }
        }

        public void LogWarning(string message, object properties)
        {
            if (_logger.IsEnabled(LogEventLevel.Warning))
            {
                using (LogContext.PushProperty("Properties", properties, true))
                {
                    _logger.Warning(message);
                }
            }
        }

        public void LogError(string message, Exception exception, object properties)
        {
            if (_logger.IsEnabled(LogEventLevel.Error))
            {
                using (LogContext.PushProperty("Properties", properties, true))
                {
                    if (exception != null)
                    {
                        _logger.Error(exception, message);
                    }
                    else
                    {
                        _logger.Error(message);
                    }
                }
            }
        }

        public void LogCritical(string message, Exception exception, object properties)
        {
            if (_logger.IsEnabled(LogEventLevel.Fatal))
            {
                using (LogContext.PushProperty("Properties", properties, true))
                {
                    if (exception != null)
                    {
                        _logger.Fatal(exception, message);
                    }
                    else
                    {
                        _logger.Fatal(message);
                    }
                }
            }
        }
    }
}

