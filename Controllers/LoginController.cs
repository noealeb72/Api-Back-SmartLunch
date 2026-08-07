using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json;
using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Models;
using smartlunch_api.Models.DTOs;
using smartlunch_api.Services;
using Serilog;
using System;
using System.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Web.Http;
using System.Web.Http.Description;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    ////[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/login")]
    public class LoginController : BaseApiController
    {
        // Servicio de administración de logins (ABM, listado, etc.)
        private readonly IServicioLogin _service;
        private readonly ILoggerService _logger;

        // Constructor con inyección de dependencias
        public LoginController(IServicioLogin service, ILoggerService logger)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        // Constructor por defecto (para compatibilidad, Unity lo usará)
        public LoginController() : this(new ServicioLogin(), new SerilogLoggerService(Serilog.Log.Logger))
        {
        }

        #region ================= ADMIN: LISTA / DETALLE / ABM =================

        /// <summary>
        /// Autentica un usuario mediante username y password
        /// </summary>
        /// <param name="model">Credenciales de autenticación (username y password)</param>
        /// <returns>Token JWT y datos del usuario</returns>
        /// <response code="200">Autenticación exitosa</response>
        /// <response code="400">Datos inválidos</response>
        /// <response code="401">Credenciales inválidas</response>
        /// <response code="429">Demasiados intentos fallidos</response>
        [HttpPost]
        [AllowAnonymous]
        [Route("Autentificar")]
        [RateLimit] // Protección contra fuerza bruta
        [ValidateModel] // Validación automática de ModelState
        public HttpResponseMessage Authenticate([FromBody] LoginRequestDto model)
        {
            var clientIp = GetClientIpAddress();
            
            if (model == null)
            {
                _logger.LogWarning("Intento de login con modelo nulo desde IP: {ClientIp}", clientIp);
                return JsonError("Los datos de autenticación son obligatorios.", HttpStatusCode.BadRequest);
            }

            // Rate limit por usuario (además del por IP que aplica el atributo [RateLimit])
            if (RateLimitAttribute.IsUsernameBlocked(model.Username, out var retryAfterUser))
            {
                _logger.LogWarning("Login bloqueado por intentos fallidos del usuario: {Username} desde IP: {ClientIp}",
                    model.Username, clientIp);
                var resp = Request.CreateResponse((HttpStatusCode)429, new
                {
                    error = "Demasiados intentos fallidos para este usuario. Intente nuevamente más tarde.",
                    retryAfter = retryAfterUser > 0 ? retryAfterUser : 10
                });
                resp.Headers.Add("Retry-After", (retryAfterUser > 0 ? retryAfterUser : 10).ToString());
                return resp;
            }

            _logger.LogInformation("Intento de login para usuario: {Username} desde IP: {ClientIp}", 
                model.Username, clientIp);

            var result = _service.Autenticar(model);

            if (result == null)
            {
                RateLimitAttribute.RecordFailedAttempt(clientIp);
                RateLimitAttribute.RecordFailedAttemptByUsername(model.Username);
                
                _logger.LogWarning("Login fallido para usuario: {Username} desde IP: {ClientIp}", 
                    model.Username, clientIp);
                
                return JsonError("Credenciales inválidas.", HttpStatusCode.Unauthorized);
            }

            RateLimitAttribute.ClearAttempts(clientIp);
            RateLimitAttribute.ClearAttemptsByUsername(model.Username);

            _logger.LogInformation("Login exitoso para usuario: {Username} (ID: {UsuarioId}) desde IP: {ClientIp}", 
                model.Username, result.UsuarioId, clientIp);

            return JsonOk(result);
        }

        /// <summary>
        /// Renueva el JWT usando el RefreshToken recibido en el login. No requiere usuario/contraseña.
        /// Si el refresh token es inválido o expiró, responde 401.
        /// </summary>
        /// <param name="model">Body con RefreshToken (string)</param>
        /// <returns>200: { Token, RefreshToken }. 401: refresh token inválido o expirado.</returns>
        [HttpPost]
        [AllowAnonymous]
        [Route("Refresh")]
        public HttpResponseMessage Refresh([FromBody] RefreshTokenRequestDto model)
        {
            _logger.LogInformation("[RefreshToken] Paso 0: Request POST /api/login/Refresh recibido.");

            if (model == null || string.IsNullOrWhiteSpace(model.RefreshToken))
            {
                _logger.LogWarning("[RefreshToken] Paso 0: Rechazado - body vacío o sin refreshToken.");
                return JsonError("RefreshToken es obligatorio.", HttpStatusCode.BadRequest);
            }

            var tokenTrimmed = model.RefreshToken.Trim();
            var tokenTrace = tokenTrimmed.Length > 8 ? tokenTrimmed.Substring(0, 8) + "…" : "***";
            _logger.LogInformation("[RefreshToken] Paso 1: RefreshToken recibido (trace: {TokenTrace}, longitud: {Length}). Llamando al servicio.", tokenTrace, tokenTrimmed.Length);

            var result = _service.RefreshToken(tokenTrimmed);

            if (result == null)
            {
                _logger.LogWarning("[RefreshToken] Paso 2: Servicio devolvió null - token inválido o expirado. Respondiendo 401.");
                return Request.CreateResponse(HttpStatusCode.Unauthorized, new { error = "Refresh token inválido o expirado. Inicie sesión nuevamente." });
            }

            _logger.LogInformation("[RefreshToken] Paso 3: Éxito. Devolviendo nuevo JWT y nuevo RefreshToken (rotación).");
            return JsonOk(result);
        }

        /// <summary>
        /// Obtiene una lista paginada de logins con filtros opcionales
        /// </summary>
        /// <param name="page">Número de página (por defecto: 1)</param>
        /// <param name="pageSize">Tamaño de página (por defecto: 10)</param>
        /// <param name="search">Texto de búsqueda opcional</param>
        /// <param name="soloActivos">Filtrar solo logins activos (por defecto: true)</param>
        /// <returns>Lista paginada de logins</returns>
        /// <response code="200">Lista obtenida exitosamente</response>
        /// <response code="401">No autorizado</response>
        /// <response code="500">Error interno del servidor</response>
        [AuthorizeWith403ForForbidden(Roles = "Admin")]
        [HttpGet]
        [Route("listarUsuarios")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            string search = null,
            bool soloActivos = true)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var result = _service.ObtenerLista(page, pageSize, search, soloActivos);
                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener logins: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene el detalle de un login por su ID
        /// </summary>
        /// <param name="id">ID del login</param>
        /// <returns>Detalle del login</returns>
        /// <response code="200">Login encontrado</response>
        /// <response code="401">No autorizado</response>
        /// <response code="404">Login no encontrado</response>
        /// <response code="500">Error interno del servidor</response>
        [AuthorizeWith403ForForbidden(Roles = "Admin")]
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                if (id <= 0)
                    return JsonError("El ID debe ser mayor a 0.", HttpStatusCode.BadRequest);

                var dto = _service.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Login no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener el login: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

       


        // POST: api/login/eliminar  (baja lógica)
        [AuthorizeWith403ForForbidden(Roles = "Admin")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage EliminarLogin(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var adminUser = ObtenerNombreUsuario();
                _service.EliminarLogin(id, adminUser);
                return JsonOk(new { message = "Login dado de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // POST: api/login/activar
        [AuthorizeWith403ForForbidden(Roles = "Admin")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage ActivarLogin(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var adminUser = ObtenerNombreUsuario();
                _service.ActivarLogin(id, adminUser);
                return JsonOk(new { message = "Login activado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        #endregion

        #region ================= AUTH: USUARIO / TÓTEM / TARJETA / LEGAJO =================

      

        // POST: api/login/authenticateTotemTarjeta  (lector de tarjeta / llave de acceso) - oculto en Swagger
        [ApiExplorerSettings(IgnoreApi = true)]
        [HttpPost]
        [AllowAnonymous]
        [RateLimit] // Protección contra fuerza bruta por IP
        [Route("AutentificarTotem")]
        public HttpResponseMessage AuthenticateTotemTarjeta([FromBody] string acceso)
        {
            if (string.IsNullOrWhiteSpace(acceso))
                return JsonError("Sin identificación para el usuario.", HttpStatusCode.BadRequest);

            // Rate limit también por la llave de acceso probada (además del de IP que aplica [RateLimit])
            if (RateLimitAttribute.IsUsernameBlocked(acceso, out var retryAfterAcceso))
            {
                var respBloqueo = Request.CreateResponse((HttpStatusCode)429, new
                {
                    error = "Demasiados intentos fallidos para este acceso. Intente nuevamente más tarde.",
                    retryAfter = retryAfterAcceso > 0 ? retryAfterAcceso : 10
                });
                respBloqueo.Headers.Add("Retry-After", (retryAfterAcceso > 0 ? retryAfterAcceso : 10).ToString());
                return respBloqueo;
            }

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var usuario = ctx.sl_usuario
                        .FirstOrDefault(x => x.llave_acceso == acceso && !x.deletemark);

                    if (usuario == null)
                    {
                        RateLimitAttribute.RecordFailedAttemptByUsername(acceso);
                        return UnauthorizedCustom("No se encontró un acceso para ese usuario.");
                    }

                    RateLimitAttribute.ClearAttemptsByUsername(acceso);

                    // 1) Claims con toda la info del usuario
                    var identity = BuildClaimsIdentity(usuario, "JWT");

                    // 2) Generar token JWT usando JwtSecret del web.config
                    var token = GenerateJwtToken(identity);

                    // 3) Devolver token + datos básicos para el tótem
                    var result = new
                    {
                        token,
                        usuario = new
                        {
                            id = usuario.id,
                            nombre = usuario.nombre,
                            apellido = usuario.apellido,
                            planta_id = usuario.planta_id,
                            centro_costo_id = usuario.centrodecosto_id,
                            proyecto_id = usuario.proyecto_id,
                            jerarquia_id = usuario.jerarquia_id
                        }
                    };

                    return JsonOk(result);
                }
            }
            catch
            {
                return JsonError("Error al autenticar al usuario.", HttpStatusCode.InternalServerError);
            }
        }

        // POST: api/login/validateTarjeta   (si querés reutilizar LoginTotem para validar algo más)
        /*[HttpPost]
        [AllowAnonymous]
        [Route("validateTarjeta")]
        public HttpResponseMessage ValidateTarjeta([FromBody] LoginRequestDto model)
        {
            if (model == null ||
                string.IsNullOrWhiteSpace(model.Username) ||
                string.IsNullOrWhiteSpace(model.Password))
            {
                return JsonError("Usuario y contraseña son obligatorios.", HttpStatusCode.BadRequest);
            }

            var result = _authService.LoginTotem(model.Username, model.Password);

            if (result == null)
                return JsonError("Credenciales inválidas.", HttpStatusCode.Unauthorized);

            return JsonOk(result);
        }*/


        [HttpPut]
        [Route("cambiar-clave")]
        [Authorize]
        public HttpResponseMessage CambiarClave([FromBody] LoginCambiarClaveDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            if (string.IsNullOrWhiteSpace(dto.ClaveActual))
                return JsonError("La clave actual es obligatoria.", HttpStatusCode.BadRequest);

            if (string.IsNullOrWhiteSpace(dto.NuevaClave))
                return JsonError("La nueva clave es obligatoria.", HttpStatusCode.BadRequest);

            try
            {
                PasswordUtils.ValidarFortaleza(dto.NuevaClave);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }

            if (dto.UsuarioId <= 0)
                return JsonError("El usuario_id es obligatorio.", HttpStatusCode.BadRequest);

            try
            {
                var usuarioIdClaim = User?.Identity is ClaimsIdentity identity
                    ? identity.FindFirst("usuario")?.Value
                    : null;
                if (string.IsNullOrEmpty(usuarioIdClaim) || !int.TryParse(usuarioIdClaim, out var tokenUsuarioId))
                    return JsonError("Token inválido o sin usuario.", HttpStatusCode.Unauthorized);

                // Solo puede cambiar la clave de su propio usuario (token.usuario_id == usuario_id del body)
                if (tokenUsuarioId != dto.UsuarioId)
                    return JsonError("No puede cambiar la contraseña de otro usuario.", HttpStatusCode.Forbidden);

                string adminUser;
                using (var ctx = new DataContext())
                {
                    var login = ctx.sl_login.FirstOrDefault(l => l.usuario_id == dto.UsuarioId && !l.deletemark);
                    if (login == null)
                        return JsonError("No se encontró el login del usuario.", HttpStatusCode.NotFound);
                    dto.Id = login.id;
                    adminUser = login.username;
                }

                _service.CambiarClave(dto, adminUser);
                return JsonOk(new { message = "La clave se actualizó correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        #endregion

        #region ================= HELPERS PRIVADOS =================

        private UsuarioDto MapToUsuarioDto(sl_usuario u)
        {
            if (u == null) return null;

            return new UsuarioDto
            {
                id = u.id,
                nombre = u.nombre,
                apellido = u.apellido,
                legajo = u.legajo,
                dni = u.dni,
                cuil = u.cuil,
                plannutricional_id = u.plannutricional_id,
                planta_id = u.planta_id,
                centrodecosto_id = u.centrodecosto_id,
                proyecto_id = u.proyecto_id,
                jerarquia_id = u.jerarquia_id,
                pedido = (int)u.pedidos,
                bonificaciones = (int)u.bonificaciones,
                //bonificaciones_invitado = u.bonificaciones_invitado,
                //llave_acceso = u.llave_acceso_num,
            };
        }

        // Arma los claims del usuario para el token JWT del tótem / lector
        private ClaimsIdentity BuildClaimsIdentity(sl_usuario usuario, string authType = "JWT")
        {
            var identity = new ClaimsIdentity(authType);

            identity.AddClaim(new Claim("usuario_id", usuario.id.ToString()));
            //identity.AddClaim(new Claim(ClaimTypes.Name, usuario.nombre + " " + usuario.apellido));

            /*if (usuario.plannutricional_id.HasValue)
                identity.AddClaim(new Claim("plan_nutricional_id", usuario.plannutricional_id.Value.ToString()));

            if (usuario.planta_id.HasValue)
                identity.AddClaim(new Claim("planta_id", usuario.planta_id.Value.ToString()));

            if (usuario.centrodecosto_id.HasValue)
                identity.AddClaim(new Claim("centro_costo_id", usuario.centrodecosto_id.Value.ToString()));

            if (usuario.proyecto_id.HasValue)
                identity.AddClaim(new Claim("proyecto_id", usuario.proyecto_id.Value.ToString()));

            if (usuario.jerarquia_id.HasValue)
                identity.AddClaim(new Claim("jerarquia_id", usuario.jerarquia_id.Value.ToString()));

            if (usuario.bonificaciones.HasValue)
                identity.AddClaim(new Claim("bonificaciones", usuario.bonificaciones.Value.ToString()));

            if (usuario.bonificaciones_invitado.HasValue)
                identity.AddClaim(new Claim("bonificaciones_invitado", usuario.bonificaciones_invitado.Value.ToString()));

            if (!string.IsNullOrWhiteSpace(usuario.llave_acceso_num))
                identity.AddClaim(new Claim("llave_acceso_num", usuario.llave_acceso_num));*/

            return identity;
        }

        protected HttpResponseMessage UnauthorizedCustom(string message)
        {
            var resp = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(JsonConvert.SerializeObject(new
                {
                    success = false,
                    message
                }))
            };
            resp.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            return resp;
        }

        private string GenerateJwtToken(ClaimsIdentity identity)
        {
            var secret = GetJwtSecret();
            var key = Encoding.UTF8.GetBytes(secret);
            var expirationHours = GetJwtExpirationHours();
            if (expirationHours <= 0) expirationHours = 10;

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = identity,
                Issuer = ConfigurationManager.AppSettings["JwtIssuer"] ?? "SmartLunchApi",
                Audience = ConfigurationManager.AppSettings["JwtAudience"] ?? "SmartLunchFront",
                IssuedAt = DateTime.UtcNow,
                NotBefore = DateTime.UtcNow,
                Expires = DateTime.UtcNow.AddHours(expirationHours),
                SigningCredentials = new SigningCredentials(
                    new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha256Signature)
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor);
            return handler.WriteToken(token);
        }

        private string GetJwtSecret()
        {
            var secret = ConfigurationManager.AppSettings["JwtSecret"];

            if (string.IsNullOrWhiteSpace(secret))
                throw new Exception("No se encontró la clave JwtSecret en web.config");

            return secret;
        }

        /// <summary>
        /// Única fuente de duración del JWT. Lee JwtExpirationHours de appSettings (por defecto 10).
        /// </summary>
        private static int GetJwtExpirationHours()
        {
            var value = ConfigurationManager.AppSettings["JwtExpirationHours"];
            return int.TryParse(value, out var h) && h > 0 ? h : 10;
        }

        private string ObtenerNombreUsuario()
        {
            try
            {
                var identity = User?.Identity as ClaimsIdentity;
                if (identity == null || !identity.IsAuthenticated)
                    return "Sistema";

                var name = identity.FindFirst(ClaimTypes.Name)?.Value
                           ?? identity.FindFirst("username")?.Value;

                return string.IsNullOrEmpty(name) ? "Sistema" : name;
            }
            catch
            {
                return "Sistema";
            }
        }

        /// <summary>
        /// Obtiene la IP del cliente desde la petición HTTP. Prioriza la IP real del socket
        /// (no falsificable) por sobre los headers X-Forwarded-For / X-Real-IP, que cualquier
        /// cliente puede mandar con el valor que quiera. Esos headers solo se usan si
        /// "TrustProxyHeaders" está explícitamente en true en appSettings. Debe coincidir con
        /// la misma lógica de Filters/RateLimitAttribute.cs.
        /// </summary>
        private string GetClientIpAddress()
        {
            try
            {
                var request = Request;
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

                    if (!string.IsNullOrWhiteSpace(forwardedIp) && (confiarEnProxy || string.IsNullOrWhiteSpace(ip)))
                    {
                        ip = forwardedIp.Split(',')[0].Trim();
                    }
                }

                return ip ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        /*[HttpPost]
        [Route("admin/reset-root-password")]
        [AllowAnonymous] // o con alguna protección extra
        public IHttpActionResult ResetRootPassword(int id)
        {
            PasswordUtils.ResetearPasswordRoot(id);
            return Ok(new { message = "Password de root cambiada a 123456789" });
        }*/

        #endregion
    }
}
