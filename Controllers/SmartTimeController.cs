using smartlunch_api.App_Start;
using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Configuration;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;

namespace smartlunch_api.Controllers
{
    [Authorize]
    [RoutePrefix("api/smartime")]
    public class SmartTimeController : ApiController
    {
        private readonly ServicioClienteSmartTimeDatoLaboral _cliente =
            new ServicioClienteSmartTimeDatoLaboral();
        private readonly ServicioSmartTimeUsuario _servicioSmartTimeUsuario =
            new ServicioSmartTimeUsuario();

        /// <summary>
        /// Indica si la integración smarTime está activa según Web.config (key "smarTime").
        /// Si la clave no existe, devuelve false. Si existe, devuelve el valor interpretado como booleano.
        /// </summary>
        [HttpGet]
        [Route("config/smarTime")]
        public IHttpActionResult GetSmarTimeActivo()
        {
            var value = ConfigurationManager.AppSettings["smarTime"];
            var activo = !string.IsNullOrWhiteSpace(value) &&
                         "true".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
            return Ok(new { smarTime = activo });
        }

        /// <summary>
        /// Indica si el usuario smarTime (username "smarTime") existe en la base de datos (sl_login, sin deletemark).
        /// Consulta la BD; devuelve true si existe, false si no.
        /// </summary>
        [HttpGet]
        [Route("config/usuario-existe")]
        public IHttpActionResult GetUsuarioSmarTimeExiste()
        {
            var existe = DatabaseSeeder.ExisteUsuarioSmartTime();
            return Ok(new { existe });
        }

        /// <summary>
        /// Crea el usuario smarTime (sl_usuario + sl_login) con los mismos datos que usa el seeder.
        /// Solo funciona si en Web.config está smarTime = true. Si el usuario ya existe, no hace nada y devuelve creado: false.
        /// Body opcional: { "password": "..." } (mínimo 6 caracteres). Si no se envía, se usa la contraseña por defecto del seeder.
        /// </summary>
        [Authorize(Roles = "Admin")]
        [HttpPost]
        [Route("config/crear-usuario")]
        public IHttpActionResult CrearUsuarioSmarTime([FromBody] CrearUsuarioSmarTimeRequest model)
        {
            var value = ConfigurationManager.AppSettings["smarTime"];
            var smarTimeActivo = !string.IsNullOrWhiteSpace(value) &&
                                 "true".Equals(value.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!smarTimeActivo)
                return BadRequest("La integración smarTime debe estar habilitada en config (smarTime = true) para crear el usuario.");

            var password = model?.Password?.Trim();
            if (!string.IsNullOrEmpty(password) && password.Length < 6)
                return BadRequest("La contraseña debe tener al menos 6 caracteres.");

            var creado = DatabaseSeeder.EnsureSmartTimeLoginExists(null, string.IsNullOrEmpty(password) ? null : password);
            if (creado)
                return Content(HttpStatusCode.Created, new { creado = true, mensaje = "Usuario smarTime creado correctamente." });
            return Ok(new { creado = false, mensaje = "El usuario smarTime ya existía." });
        }

        [HttpGet]
        [Route("dato-laboral/{legajo:int}")]
        public async Task<IHttpActionResult> GetDatoLaboral(int legajo)
        {
            try
            {
                var dto = await _cliente.ObtenerDatoLaboralAsync(legajo);
                return Ok(dto);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.BadGateway, new
                {
                    message = "Error consultando SmartTime DatoLaboral",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Crea un usuario desde smarTime (sl_usuario + sl_login). Usa defaults de catálogo y jerarquía Gerencia (4). Solo Admin o Gerencia.
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("usuarios")]
        public IHttpActionResult CrearUsuario([FromBody] SmartTimeUsuarioCrearDto dto)
        {
            if (dto == null)
                return BadRequest("El cuerpo de la solicitud es obligatorio.");

            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var resultado = _servicioSmartTimeUsuario.CrearUsuario(dto);
                return Content(HttpStatusCode.Created, resultado);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    message = "Error al crear usuario desde smarTime",
                    detail = ex.Message
                });
            }
        }

        /// <summary>
        /// Obtiene un usuario smarTime por legajo. Solo Admin o Gerencia.
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("usuarios/{legajo:int}")]
        public IHttpActionResult ObtenerUsuarioPorLegajo(int legajo)
        {
            var resultado = _servicioSmartTimeUsuario.ObtenerPorLegajo(legajo);
            if (resultado == null)
                return NotFound();
            return Ok(resultado);
        }

        /// <summary>
        /// Lista usuarios creados por smarTime (paginado). Query: page, pageSize, search (opcional), soloActivos (true = activos, false = inactivos). Solo Admin o Gerencia.
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("usuarios")]
        public IHttpActionResult ListarUsuarios(int page = 1, int pageSize = 10, string search = null, bool soloActivos = true)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            try
            {
                var resultado = _servicioSmartTimeUsuario.ListarUsuarios(page, pageSize, search, soloActivos);
                return Ok(resultado);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { message = "Error al listar usuarios smarTime", detail = ex.Message });
            }
        }

        /// <summary>
        /// Actualiza un usuario smarTime por legajo. Solo usuarios con origen smarTime. Solo Admin o Gerencia.
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("usuarios/{legajo:int}")]
        public IHttpActionResult ActualizarUsuario(int legajo, [FromBody] SmartTimeUsuarioActualizarDto dto)
        {
            if (dto == null)
                return BadRequest("El cuerpo de la solicitud es obligatorio.");
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                _servicioSmartTimeUsuario.ActualizarPorLegajo(legajo, dto);
                return Ok(new { message = "Usuario actualizado correctamente." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { message = "Error al actualizar usuario smarTime", detail = ex.Message });
            }
        }

        /// <summary>
        /// Da de baja (deletemark) un usuario smarTime y sus logins por legajo. Solo usuarios creados por smarTime. Solo Admin o Gerencia.
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpDelete]
        [Route("usuarios/{legajo:int}")]
        public IHttpActionResult DarDeBajaUsuario(int legajo)
        {
            try
            {
                _servicioSmartTimeUsuario.DarDeBajaPorLegajo(legajo);
                return Ok(new { message = "Usuario dado de baja correctamente." });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(ex.Message);
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new { message = "Error al dar de baja usuario smarTime", detail = ex.Message });
            }
        }
    }

    /// <summary>
    /// Request para POST /api/smartime/config/crear-usuario. Contraseña opcional (mínimo 6 caracteres si se envía).
    /// </summary>
    public class CrearUsuarioSmarTimeRequest
    {
        public string Password { get; set; }
    }
}
