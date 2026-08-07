using smartlunch_api.Filters;
using smartlunch_api.Models;
using System;
using System.Data;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Http.Cors;
using Newtonsoft.Json;

namespace smartlunch_api.Controllers
{
    ////[EnableCors(origins: "*", headers: "*", methods: "*")]
    [Authorize]
    [RoutePrefix("api/catalogos")]
    public class CatalogosController : BaseApiController
    {
        /// <summary>
        /// Establece un único registro como predeterminado (is_default = true) para el catálogo indicado.
        /// Solo puede haber un default por tipo. Valores de tipo: planta, centrodecosto, proyecto, plannutricional, jerarquia.
        /// </summary>
        [AuthorizeWith403ForForbidden(Roles = "Admin,Gerencia")]
        [HttpPut]
        [Route("set-default")]
        public HttpResponseMessage SetDefault([FromBody] SetDefaultCatalogoRequest model)
        {
            if (model == null || model.Id <= 0)
                return JsonError("Id es obligatorio y debe ser mayor a 0.", HttpStatusCode.BadRequest);

            var tipo = (model.Tipo ?? "").Trim().ToLowerInvariant();
            var tiposValidos = new[] { "planta", "centrodecosto", "proyecto", "plannutricional", "jerarquia" };
            if (!tiposValidos.Contains(tipo))
                return JsonError("Tipo debe ser uno de: planta, centrodecosto, proyecto, plannutricional, jerarquia.", HttpStatusCode.BadRequest);

            try
            {
                using (var ctx = new DataContext())
                using (var tx = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                {
                    if (tipo == "planta")
                    {
                        var entity = ctx.sl_planta.FirstOrDefault(p => p.id == model.Id && !p.deletemark);
                        if (entity == null)
                            return JsonError("Planta no encontrada o inactiva.", HttpStatusCode.NotFound);
                        foreach (var p in ctx.sl_planta)
                            p.is_default = false;
                        entity.is_default = true;
                    }
                    else if (tipo == "centrodecosto")
                    {
                        var entity = ctx.sl_centrodecosto.FirstOrDefault(c => c.id == model.Id && !c.deletemark);
                        if (entity == null)
                            return JsonError("Centro de costo no encontrado o inactivo.", HttpStatusCode.NotFound);
                        foreach (var c in ctx.sl_centrodecosto)
                            c.is_default = false;
                        entity.is_default = true;
                    }
                    else if (tipo == "proyecto")
                    {
                        var entity = ctx.sl_proyecto.FirstOrDefault(p => p.id == model.Id && !p.deletemark);
                        if (entity == null)
                            return JsonError("Proyecto no encontrado o inactivo.", HttpStatusCode.NotFound);
                        foreach (var p in ctx.sl_proyecto)
                            p.is_default = false;
                        entity.is_default = true;
                    }
                    else if (tipo == "plannutricional")
                    {
                        var entity = ctx.sl_plannutricional.FirstOrDefault(p => p.id == model.Id && !p.deletemark);
                        if (entity == null)
                            return JsonError("Plan nutricional no encontrado o inactivo.", HttpStatusCode.NotFound);
                        foreach (var p in ctx.sl_plannutricional)
                            p.is_default = false;
                        entity.is_default = true;
                    }
                    else if (tipo == "jerarquia")
                    {
                        var entity = ctx.sl_jerarquia.FirstOrDefault(j => j.id == model.Id && !j.deletemark);
                        if (entity == null)
                            return JsonError("Jerarquía no encontrada o inactiva.", HttpStatusCode.NotFound);
                        foreach (var j in ctx.sl_jerarquia)
                            j.is_default = false;
                        entity.is_default = true;
                    }

                    ctx.SaveChanges();
                    tx.Commit();
                }

                return JsonOk(new { message = "Predeterminado actualizado correctamente.", tipo, id = model.Id });
            }
            catch (Exception ex)
            {
                return JsonError(ex, HttpStatusCode.InternalServerError);
            }
        }

        [HttpGet]
        [Route("jerarquias")]
        public HttpResponseMessage GetJerarquias()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var data = ctx.sl_jerarquia
                        .Where(j => j.deletemark != true)
                        .OrderBy(j => j.nombre)
                        .Select(j => new
                        {
                            j.id,
                            j.nombre,
                            j.descripcion,
                            j.is_default
                        })
                        .ToList();

                    return JsonOk(data);
                }
            }
            catch
            {
                return JsonError("Error al obtener las jerarquías.", HttpStatusCode.InternalServerError);
            }
        }

        // ================
        // Plantas
        // ================
        [HttpGet]
        [Route("plantas")]
        public HttpResponseMessage GetPlantas()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var data = ctx.sl_planta
                        .Where(p => p.deletemark != true)
                        .OrderBy(p => p.nombre)
                        .Select(p => new
                        {
                            p.id,
                            p.nombre,
                            p.descripcion,
                            p.is_default
                        })
                        .ToList();

                    return JsonOk(data);
                }
            }
            catch
            {
                return JsonError("Error al obtener las plantas.", HttpStatusCode.InternalServerError);
            }
        }

        // ================
        // Centros de Costo
        // ================
        [HttpGet]
        [Route("centrosdecosto")]
        public HttpResponseMessage GetCentrosDeCosto()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var data = ctx.sl_centrodecosto
                        .Where(c => c.deletemark != true)
                        .OrderBy(c => c.nombre)
                        .Select(c => new
                        {
                            c.id,
                            c.nombre,
                            c.descripcion,
                            c.is_default
                        })
                        .ToList();

                    return JsonOk(data);
                }
            }
            catch
            {
                return JsonError("Error al obtener los centros de costo.", HttpStatusCode.InternalServerError);
            }
        }

        // ================
        // Proyectos
        // ================
        [HttpGet]
        [Route("proyectos")]
        public HttpResponseMessage GetProyectos()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var data = ctx.sl_proyecto
                        .Where(p => p.deletemark != true)
                        .OrderBy(p => p.nombre)
                        .Select(p => new
                        {
                            p.id,
                            p.nombre,
                            p.descripcion,
                            p.is_default
                        })
                        .ToList();

                    return JsonOk(data);
                }
            }
            catch
            {
                return JsonError("Error al obtener los proyectos.", HttpStatusCode.InternalServerError);
            }
        }

        // ================
        // Planes Nutricionales
        // ================
        [HttpGet]
        [Route("planesnutricionales")]
        public HttpResponseMessage GetPlanesNutricionales()
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var data = ctx.sl_plannutricional
                        .Where(p => p.deletemark != true)
                        .OrderBy(p => p.nombre)
                        .Select(p => new
                        {
                            p.id,
                            p.nombre,
                            p.descripcion,
                            p.is_default
                        })
                        .ToList();

                    return JsonOk(data);
                }
            }
            catch
            {
                return JsonError("Error al obtener los planes nutricionales.", HttpStatusCode.InternalServerError);
            }
        }
    }

    /// <summary>
    /// Request para PUT /api/catalogos/set-default. Tipo: planta | centrodecosto | proyecto | plannutricional | jerarquia.
    /// </summary>
    public class SetDefaultCatalogoRequest
    {
        public string Tipo { get; set; }
        public int Id { get; set; }
    }
}
