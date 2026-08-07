using System;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using System.Web.Cors;
using System.Web.Http;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Owin;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Security;
using Microsoft.Owin.Security.Jwt;
using Owin;
using smartlunch_api.Middleware;

[assembly: OwinStartup(typeof(smartlunch_api.Startup))]

namespace smartlunch_api
{
    public class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            HttpConfiguration config = GlobalConfiguration.Configuration;

            // 0) CORS explícito: cabeceras en TODAS las respuestas (incl. 400/500) y respuesta a OPTIONS preflight
            app.UseCorsHeaders();

            // 1) CORS a nivel OWIN (refuerzo; orígenes/métodos/headers desde Web.config)
            app.UseCors(new CorsOptions
            {
                PolicyProvider = new ConfigCorsPolicyProvider()
            });

            // 2) JWT (después de CORS)
            ConfigureJwt(app);

            // 3) Middleware para interceptar respuestas 401 y devolver mensajes descriptivos
            app.UseUnauthorizedResponseHandler();

            // 4) WebApi en el pipeline OWIN
            app.UseWebApi(config);
        }

        private void ConfigureJwt(IAppBuilder app)
        {
            var secret = ConfigurationManager.AppSettings["JwtSecret"];
            if (string.IsNullOrWhiteSpace(secret))
                throw new ConfigurationErrorsException("Falta JwtSecret en appSettings/appSettings.secrets.config");
            
            var issuer = ConfigurationManager.AppSettings["JwtIssuer"] ?? "SmartLunchApi";
            var audience = ConfigurationManager.AppSettings["JwtAudience"] ?? "SmartLunchFront";

            var keyBytes = Encoding.UTF8.GetBytes(secret);
            var signingKey = new SymmetricSecurityKey(keyBytes);

            app.UseJwtBearerAuthentication(new JwtBearerAuthenticationOptions
            {
                AuthenticationMode = AuthenticationMode.Active,

                TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = issuer,

                    ValidateAudience = true,
                    ValidAudience = audience,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = signingKey,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),

                    // Para que [Authorize(Roles = "Admin")] reconozca el claim de rol del JWT
                    RoleClaimType = ClaimTypes.Role
                }
            });
        }
    }

    /// <summary>
    /// Proveedor de política CORS que lee orígenes, métodos y headers desde Web.config.
    /// </summary>
    internal class ConfigCorsPolicyProvider : ICorsPolicyProvider
    {
        public Task<CorsPolicy> GetCorsPolicyAsync(IOwinRequest request)
        {
            var policy = new CorsPolicy();
            policy.AllowAnyOrigin = false;

            var origins = ConfigurationManager.AppSettings["CorsAllowedOrigins"] ?? "";
            foreach (var o in origins.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = o.Trim();
                if (!string.IsNullOrEmpty(t)) policy.Origins.Add(t);
            }

            var methods = ConfigurationManager.AppSettings["CorsAllowedMethods"] ?? "GET,POST,PUT,DELETE,OPTIONS";
            foreach (var m in methods.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = m.Trim();
                if (!string.IsNullOrEmpty(t)) policy.Methods.Add(t);
            }

            var headers = ConfigurationManager.AppSettings["CorsAllowedHeaders"] ?? "Content-Type,Authorization";
            foreach (var h in headers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var t = h.Trim();
                if (!string.IsNullOrEmpty(t)) policy.Headers.Add(t);
            }

            if (policy.Origins.Count == 0)
            {
                policy.Origins.Add("http://localhost:8000");
                policy.Origins.Add("http://localhost:4200");
            }

            return Task.FromResult(policy);
        }
    }
}
