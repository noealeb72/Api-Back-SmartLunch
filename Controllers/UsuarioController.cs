using smartlunch_api.App_Start;
using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Models.DTOs;
using smartlunch_api.Services;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    [Authorize]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/usuario")]
    public class UsuarioController : BaseApiController
    {
        private readonly IServicioUsuario _servicioUsuario;

        public UsuarioController(IServicioUsuario servicioUsuario)
        {
            _servicioUsuario = servicioUsuario ?? throw new ArgumentNullException(nameof(servicioUsuario));
        }

        public UsuarioController() : this(new ServicioUsuario())
        {
        }

        /// <summary>
        /// Indica si existe el usuario SmartTime (smarTime) en el sistema. El front puede usar response.data.existe (booleano).
        /// </summary>
        /// <returns>{ "existe": true } o { "existe": false }</returns>
        [HttpGet]
        [Route("smarttime/existe")]
        public HttpResponseMessage SmartTimeExiste()
        {
            try
            {
                var existe = DatabaseSeeder.ExisteUsuarioSmartTime();
                return JsonOk(new { existe });
            }
            catch (Exception ex)
            {
                return JsonError($"Error al verificar usuario SmartTime: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene una lista paginada de usuarios con filtros opcionales
        /// </summary>
        /// <param name="page">Número de página (por defecto: 1)</param>
        /// <param name="pageSize">Tamaño de página (por defecto: 10)</param>
        /// <param name="search">Texto de búsqueda opcional</param>
        /// <param name="plantaId">Filtrar por ID de planta</param>
        /// <param name="centroCostoId">Filtrar por ID de centro de costo</param>
        /// <param name="proyectoId">Filtrar por ID de proyecto</param>
        /// <param name="jerarquiaId">Filtrar por ID de jerarquía</param>
        /// <param name="planNutricionalId">Filtrar por ID de plan nutricional</param>
        /// <param name="activo">Filtrar solo usuarios activos (por defecto: true)</param>
        /// <returns>Lista paginada de usuarios</returns>
        /// <response code="200">Lista obtenida exitosamente</response>
        /// <response code="401">No autorizado</response>
        /// <response code="500">Error interno del servidor</response>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            string search = null,
            int? plantaId = null,
            int? centroCostoId = null,
            int? proyectoId = null,
            int? jerarquiaId = null,
            int? planNutricionalId = null,
            bool activo = true)
        {
            try
            {
                if (page < 1) page = 1;
                if (pageSize < 1 || pageSize > 100) pageSize = 10;

                var result = _servicioUsuario.ObtenerLista(
                    page,
                    pageSize,
                    search,
                    plantaId,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    planNutricionalId,
                    activo
                );

                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener los usuarios: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Buscador simple de usuarios que devuelve solo legajo y nombre
        /// Busca por legajo o nombre, solo a partir de la cuarta letra
        /// </summary>
        /// <param name="texto">Texto a buscar (mínimo 4 caracteres)</param>
        /// <param name="soloActivos">Solo usuarios activos (por defecto: true)</param>
        /// <param name="maxResultados">Cantidad máxima de resultados (por defecto: 20, máximo: 100)</param>
        /// <returns>Lista de usuarios con legajo y nombre</returns>
        /// <response code="200">Lista obtenida exitosamente</response>
        /// <response code="401">No autorizado</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpGet]
        [Route("buscar-simple")]
        public HttpResponseMessage BuscarSimple(
            string texto = null,
            bool soloActivos = true,
            int maxResultados = 20)
        {
            try
            {
                var result = _servicioUsuario.BuscarUsuariosSimple(texto, soloActivos, maxResultados);
                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al buscar usuarios: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Obtiene el detalle de un usuario por su ID
        /// </summary>
        /// <param name="id">ID del usuario</param>
        /// <returns>Detalle del usuario</returns>
        /// <response code="200">Usuario encontrado</response>
        /// <response code="401">No autorizado</response>
        /// <response code="404">Usuario no encontrado</response>
        /// <response code="500">Error interno del servidor</response>
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                if (id <= 0)
                    return JsonError("El ID debe ser mayor a 0.", HttpStatusCode.BadRequest);

                var dto = _servicioUsuario.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Usuario no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener el usuario: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        /// <summary>
        /// Crea un nuevo usuario
        /// </summary>
        /// <param name="dto">Datos del usuario a crear</param>
        /// <returns>Usuario creado</returns>
        /// <response code="201">Usuario creado exitosamente</response>
        /// <response code="400">Datos inválidos o errores de validación</response>
        /// <response code="401">No autorizado</response>
        /// <response code="500">Error interno del servidor</response>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("crear")]
        [ValidateModel] // Validación automática de ModelState
        public HttpResponseMessage CrearUsuario([FromBody] UsuarioCreateDto dto)
        {
            if (dto == null)
                return JsonError("Los datos del usuario son obligatorios.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var creado = _servicioUsuario.CrearUsuario(dto, username);

                return JsonOk(creado, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex, HttpStatusCode.InternalServerError);
            }
        }

        // ============= PUT api/usuario/actualizar =============
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage ActualizarUsuario([FromBody] UsuarioUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioUsuario.ActualizarUsuario(dto, username);

                return JsonOk(new { message = "Usuario actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============= POST api/usuario/eliminar =============
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage EliminarUsuario(int id)
        {
            if (id <= 0)
                return JsonError("Usuario inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioUsuario.EliminarUsuario(id, username);

                return JsonOk(new { message = "Usuario dado de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ============= POST api/usuario/activar =============
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage ActivarUsuario(int id)
        {
            if (id <= 0)
                return JsonError("Usuario inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioUsuario.ActivarUsuario(id, username);

                return JsonOk(new { message = "Usuario activado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // ===== helper usuario logueado =====
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
    }
}
