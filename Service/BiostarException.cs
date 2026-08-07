using System;

namespace smartlunch_api.Service
{
    /// <summary>
    /// Excepción específica para errores al llamar a Biostar2.
    /// Incluye el StatusCode y el Body devuelto por Biostar.
    /// </summary>
    public class BiostarException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }
        public string Url { get; }

        public BiostarException(string message, int statusCode, string responseBody, string url)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
            Url = url;
        }
    }
}
