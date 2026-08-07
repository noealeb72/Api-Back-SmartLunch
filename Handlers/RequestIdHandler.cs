using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Serilog.Context;

namespace smartlunch_api.Handlers
{
    /// <summary>
    /// Asigna un RequestId (GUID) a cada petición, lo expone en la cabecera de respuesta X-Request-Id
    /// y en el contexto de Serilog para correlacionar logs.
    /// </summary>
    public class RequestIdHandler : DelegatingHandler
    {
        public const string RequestIdKey = "X-Request-Id";
        public const string RequestIdPropertyName = "RequestId";

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var requestId = request.Headers.Contains(RequestIdKey)
                ? request.Headers.GetValues(RequestIdKey).FirstOrDefault()?.Trim()
                : null;
            if (string.IsNullOrEmpty(requestId))
                requestId = Guid.NewGuid().ToString("N");

            if (request.Properties != null)
                request.Properties[RequestIdPropertyName] = requestId;

            using (LogContext.PushProperty(RequestIdPropertyName, requestId))
            {
                var response = await base.SendAsync(request, cancellationToken);
                if (response.Headers != null && !response.Headers.Contains(RequestIdKey))
                    response.Headers.Add(RequestIdKey, requestId);
                return response;
            }
        }
    }
}
