using System;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE DE LOGGING
    // ============================================
    public interface ILoggerService
    {
        void LogDebug(string message, params object[] args);
        void LogInformation(string message, params object[] args);
        void LogWarning(string message, params object[] args);
        void LogError(string message, Exception exception = null, params object[] args);
        void LogError(Exception exception, string message, params object[] args);
        void LogCritical(string message, Exception exception = null, params object[] args);
        
        // Logging estructurado con propiedades
        void LogDebug(string message, object properties);
        void LogInformation(string message, object properties);
        void LogWarning(string message, object properties);
        void LogError(string message, Exception exception, object properties);
        void LogCritical(string message, Exception exception, object properties);
    }
}

