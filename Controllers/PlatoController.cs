using smartlunch_api.Dtos;
using smartlunch_api.Filters;
using smartlunch_api.Services;
using System;
using System.Net;
using System.Net.Http;
using System.Security.Claims;
using System.Web.Http;
using System.Web.Http.Cors;
using System.IO;
using System.Web;
using System.Linq;
using System.Configuration;

namespace smartlunch_api.Controllers
{
    [Authorize]
    //[EnableCors(origins: "*", headers: "*", methods: "*")]
    [RoutePrefix("api/plato")]
    public class PlatoController : BaseApiController
    {
        private readonly IServicioPlato _servicioPlato;

        public PlatoController(IServicioPlato servicioPlato)
        {
            _servicioPlato = servicioPlato ?? throw new ArgumentNullException(nameof(servicioPlato));
        }

        public PlatoController() : this(new ServicioPlato())
        {
        }

        // =========================
        // GET api/plato/lista
        // =========================
        [HttpGet]
        [Route("lista")]
        public HttpResponseMessage ObtenerLista(
            int page = 1,
            int pageSize = 10,
            string search = null,
            bool activo = true)
        {
            try
            {
                var result = _servicioPlato.ObtenerLista(page, pageSize, search, activo);
                return JsonOk(result);
            }
            catch
            {
                return JsonError("Error al obtener los platos.", HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // GET api/plato/{id}
        // =========================
        [HttpGet]
        [Route("{id:int}")]
        public HttpResponseMessage ObtenerPorId(int id)
        {
            try
            {
                var dto = _servicioPlato.ObtenerPorId(id);
                if (dto == null)
                    return JsonError("Plato no encontrado.", HttpStatusCode.NotFound);

                return JsonOk(dto);
            }
            catch
            {
                return JsonError("Error al obtener el plato.", HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // GET api/plato/buscar?texto=...
        // (para autocomplete / buscador rápido - devuelve todos los campos)
        // =========================
        [HttpGet]
        [Route("buscar")]
        public HttpResponseMessage Buscar(
            string texto = null,
            bool soloActivos = true,
            int maxResultados = 20)
        {
            try
            {
                var result = _servicioPlato.BuscarPlatos(texto, soloActivos, maxResultados);
                return JsonOk(result);
            }
            catch
            {
                return JsonError("Error al buscar platos.", HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // GET api/plato/buscar-simple?texto=...
        // (buscador simple que devuelve solo código y nombre)
        // =========================
        [HttpGet]
        [Route("buscar-simple")]
        public HttpResponseMessage BuscarSimple(
            string texto = null,
            bool soloActivos = true,
            int maxResultados = 20)
        {
            try
            {
                var result = _servicioPlato.BuscarPlatosSimple(texto, soloActivos, maxResultados);
                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al buscar platos: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // GET api/plato/por-plan-nutricional/{planNutricionalId}
        // =========================
        [HttpGet]
        [Route("por-plan-nutricional/{planNutricionalId}")]
        public HttpResponseMessage ObtenerPorPlanNutricional(
            int planNutricionalId,
            bool soloActivos = true)
        {
            try
            {
                if (planNutricionalId <= 0)
                    return JsonError("El ID del plan nutricional debe ser mayor a 0.", HttpStatusCode.BadRequest);

                var result = _servicioPlato.ObtenerPlatosPorPlanNutricional(planNutricionalId, soloActivos);
                return JsonOk(result);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al obtener los platos: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // POST api/plato/crear
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("crear")]
        public HttpResponseMessage Crear([FromBody] PlatoCreateDto dto)
        {
            if (dto == null)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                var creado = _servicioPlato.CrearPlato(dto, username);

                return JsonOk(creado, HttpStatusCode.Created);
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // =========================
        // PUT api/plato/actualizar
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPut]
        [Route("actualizar")]
        public HttpResponseMessage Actualizar([FromBody] PlatoUpdateDto dto)
        {
            if (dto == null || dto.Id <= 0)
                return JsonError("Datos inválidos.", HttpStatusCode.BadRequest);

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioPlato.ActualizarPlato(dto, username);

                return JsonOk(new { message = "Plato actualizado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // =========================
        // POST api/plato/baja
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("baja")]
        public HttpResponseMessage Eliminar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioPlato.EliminarPlato(id, username);

                return JsonOk(new { message = "Plato de baja correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // =========================
        // POST api/plato/activar
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("activar")]
        public HttpResponseMessage Activar(int id)
        {
            if (id <= 0)
                return JsonError("Id inválido.");

            try
            {
                var username = ObtenerNombreUsuario();
                _servicioPlato.ActivarPlato(id, username);

                return JsonOk(new { message = "Plato activado correctamente." });
            }
            catch (Exception ex)
            {
                return JsonError(ex.Message, HttpStatusCode.BadRequest);
            }
        }

        // =========================
        // GET api/plato/imagen/{nombreArchivo}
        // Sirve las imágenes de los platos
        // =========================
        [AllowAnonymous]
        [HttpGet]
        [Route("imagen/{*nombreArchivo}")]
        public HttpResponseMessage ObtenerImagen(string nombreArchivo)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombreArchivo))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);

                // Quedarse solo con el nombre de archivo, descartando cualquier segmento de ruta
                // (../, \, rutas absolutas). Un StartsWith sobre la ruta combinada no alcanza acá:
                // Path.Combine no resuelve "..", así que ese chequeo se puede evadir con
                // "../../web.config" y terminar leyendo archivos fuera de la carpeta de imágenes.
                var nombreArchivoSeguro = Path.GetFileName(nombreArchivo);
                if (string.IsNullOrWhiteSpace(nombreArchivoSeguro))
                    return new HttpResponseMessage(HttpStatusCode.BadRequest);

                // Obtener ruta de la carpeta de imágenes desde web.config
                var relativeFolder = ConfigurationManager.AppSettings["PlatosImagenesRelativePath"];
                if (string.IsNullOrWhiteSpace(relativeFolder))
                    relativeFolder = "/uploads/platos"; // Fallback solo si no está configurado en web.config

                // Construir ruta física del archivo
                var physicalFolder = HttpContext.Current.Server.MapPath("~" + relativeFolder);
                var filePath = Path.Combine(physicalFolder, nombreArchivoSeguro);

                if (!File.Exists(filePath))
                    return new HttpResponseMessage(HttpStatusCode.NotFound);

                // Leer el archivo
                var fileBytes = File.ReadAllBytes(filePath);
                var extension = Path.GetExtension(nombreArchivoSeguro)?.ToLowerInvariant();

                // Determinar content type
                string contentType = "image/jpeg";
                switch (extension)
                {
                    case ".png":
                        contentType = "image/png";
                        break;
                    case ".gif":
                        contentType = "image/gif";
                        break;
                    case ".webp":
                        contentType = "image/webp";
                        break;
                }

                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(fileBytes)
                };
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
                response.Content.Headers.ContentLength = fileBytes.Length;

                return response;
            }
            catch
            {
                return new HttpResponseMessage(HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // POST api/subir-foto
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpPost]
        [Route("subir-foto")]
        public IHttpActionResult SubirFoto()
        {
            var httpRequest = HttpContext.Current?.Request;
            if (httpRequest == null || httpRequest.Files.Count == 0)
                return BadRequest("No se recibió ningún archivo.");

            var file = httpRequest.Files[0];

            if (file.ContentLength == 0)
                return BadRequest("El archivo está vacío.");

            // Validar extensión
            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            var extensionesPermitidas = new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

            if (!extensionesPermitidas.Contains(ext))
                return BadRequest("Formato de imagen no permitido.");

            // Leer ruta desde web.config (PlatosImagenesRelativePath)
            var relativeFolder = ConfigurationManager.AppSettings["PlatosImagenesRelativePath"];
            if (string.IsNullOrWhiteSpace(relativeFolder))
                relativeFolder = "/uploads/platos"; // Fallback solo si no está configurado en web.config

            var baseUrl = ConfigurationManager.AppSettings["PlatosImagenesBaseUrl"];
            // puede estar vacío; si lo está, devolvemos solo ruta relativa

            // Nombre único
            var nombreLimpio = Path.GetFileNameWithoutExtension(file.FileName);
            var nombreSeguro = string.Join("_", nombreLimpio.Split(Path.GetInvalidFileNameChars()));
            var nombreFinal = $"{nombreSeguro}_{DateTime.Now:yyyyMMddHHmmssfff}{ext}";

            // Ruta relativa que guardamos en BD
            var relativePath = $"{relativeFolder.TrimEnd('/')}/{nombreFinal}";

            // Ruta física en el servidor
            var physicalFolder = HttpContext.Current.Server.MapPath("~" + relativeFolder);
            if (!Directory.Exists(physicalFolder))
                Directory.CreateDirectory(physicalFolder);

            var fullFilePath = Path.Combine(physicalFolder, nombreFinal);
            file.SaveAs(fullFilePath);

            // Devolver solo la ruta relativa como string plano (ej: "/uploads/platos/imagen.jpg")
            // El frontend espera recibir directamente la ruta relativa como texto plano
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(relativePath, System.Text.Encoding.UTF8, "text/plain")
            };
            return ResponseMessage(response);
        }

        // =========================
        // DELETE api/plato/eliminar-foto
        // Elimina un archivo de imagen del servidor
        // =========================
        [AuthorizeWith403ForForbidden(Roles = "Cocina")]
        [HttpDelete]
        [Route("eliminar-foto")]
        public HttpResponseMessage EliminarFoto([FromBody] EliminarFotoDto dto)
        {
            try
            {
                if (dto == null || string.IsNullOrWhiteSpace(dto.Ruta))
                    return JsonError("La ruta de la imagen es obligatoria.", HttpStatusCode.BadRequest);

                // Leer ruta desde web.config (PlatosImagenesRelativePath)
                var relativeFolder = ConfigurationManager.AppSettings["PlatosImagenesRelativePath"];
                if (string.IsNullOrWhiteSpace(relativeFolder))
                    relativeFolder = "/uploads/platos"; // Fallback solo si no está configurado en web.config

                // Validar que la ruta recibida esté dentro de la carpeta permitida (seguridad)
                var rutaNormalizada = dto.Ruta.Trim();
                if (!rutaNormalizada.StartsWith(relativeFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonError("La ruta de la imagen no es válida.", HttpStatusCode.BadRequest);
                }

                // Construir ruta física del archivo
                var physicalFolder = HttpContext.Current.Server.MapPath("~" + relativeFolder);
                var nombreArchivo = Path.GetFileName(rutaNormalizada);
                var filePath = Path.Combine(physicalFolder, nombreArchivo);

                // Validar que el archivo existe y está dentro de la carpeta permitida (seguridad adicional)
                if (!File.Exists(filePath) || !filePath.StartsWith(physicalFolder, StringComparison.OrdinalIgnoreCase))
                {
                    return JsonError("El archivo no existe o la ruta no es válida.", HttpStatusCode.NotFound);
                }

                // Eliminar el archivo
                File.Delete(filePath);

                return JsonOk(new { message = "Imagen eliminada correctamente." });
            }
            catch (UnauthorizedAccessException)
            {
                return JsonError("No se tiene permiso para eliminar el archivo.", HttpStatusCode.Forbidden);
            }
            catch (Exception ex)
            {
                return JsonError($"Error al eliminar la imagen: {ex.Message}", HttpStatusCode.InternalServerError);
            }
        }

        // =========================
        // POST api/plato/impresion
        // =========================
        [HttpPost]
        [Route("impresion")]
        public HttpResponseMessage ObtenerDatosImpresion([FromBody] PlatoImpresionRequestDto request)
        {
            if (request == null)
                return JsonError("La solicitud de impresión no puede ser nula.", HttpStatusCode.BadRequest);

            try
            {
                var datos = _servicioPlato.ObtenerDatosImpresion(request);
                return JsonOk(datos);
            }
            catch (Exception ex)
            {
                string errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" | InnerException: {ex.InnerException.Message}";
                }
                return JsonError($"Error al obtener los datos para impresión: {errorMessage}", HttpStatusCode.InternalServerError);
            }
        }

        // ===== helper para usuario logueado =====
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
