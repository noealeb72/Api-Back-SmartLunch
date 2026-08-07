using System;
using System.Collections.Generic;
using System.Data.Entity.Validation;
using System.Linq;
using System.Text;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioJerarquia
    {
        PagedResultDto<JerarquiaListadoDto> ObtenerLista(int page, int pageSize, string search, bool estado);
        JerarquiaDetalleDto ObtenerPorId(int id);
        JerarquiaDetalleDto Crear(JerarquiaCreateDto dto, string username);
        void Actualizar(JerarquiaUpdateDto dto, string username);
        void Eliminar(int id, string username);
        void Activar(int id, string username);
        IEnumerable<JerarquiaListadoDto> Buscar(string texto, bool soloActivos = true, int maxResultados = 20);
        List<JerarquiaImpresionDto> ObtenerDatosImpresion(JerarquiaImpresionRequestDto request);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioJerarquia : IServicioJerarquia
    {
        private readonly ILoggerService _logger;

        public ServicioJerarquia(ILoggerService logger = null)
        {
            _logger = logger;
        }

        // ============================================
        // Lista paginada de jerarquías
        // ============================================
        public PagedResultDto<JerarquiaListadoDto> ObtenerLista(
            int page,
            int pageSize,
            string search,
            bool activo)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var query =
                    from j in ctx.sl_jerarquia
                    select new JerarquiaListadoDto
                    {
                        Id = j.id,
                        Nombre = j.nombre,
                        Descripcion = j.descripcion,
                        Deletemark = j.deletemark,
                        Bonificacion = j.bonificacion,
                        IsDefault = j.is_default
                    };

                // Filtro por activo/inactivo
                query = activo
                    ? query.Where(x => !x.Deletemark)
                    : query.Where(x => x.Deletemark);

                // Búsqueda opcional
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(j =>
                        (j.Nombre ?? "").ToLower().Contains(s) ||
                        (j.Descripcion ?? "").ToLower().Contains(s)
                    );
                }

                var totalItems = query.Count();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var items = query
                    .OrderBy(j => j.Nombre)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToList();

                return new PagedResultDto<JerarquiaListadoDto>
                {
                    page = page,
                    pageSize = pageSize,
                    totalItems = totalItems,
                    totalPages = totalPages,
                    items = items
                };
            }
        }

        // ============================================
        // Obtener detalle por Id
        // ============================================
        public JerarquiaDetalleDto ObtenerPorId(int id)
        {
            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var jerarquia =
                    (from j in ctx.sl_jerarquia
                     where j.id == id && j.deletemark != true
                     select new JerarquiaDetalleDto
                     {
                         Id = j.id,
                         Nombre = j.nombre,
                         Descripcion = j.descripcion,
                         Bonificacion = j.bonificacion,
                         IsDefault = j.is_default
                     })
                    .FirstOrDefault();

                return jerarquia;
            }
        }

        // ============================================
        // Crear jerarquía
        // ============================================
        public JerarquiaDetalleDto Crear(JerarquiaCreateDto dto, string username)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre de la jerarquía es obligatorio.");

            using (var ctx = new DataContext())
            {
                var existeNombre = ctx.sl_jerarquia.Any(j =>
                    j.nombre == dto.Nombre && j.deletemark != true);

                if (existeNombre)
                    throw new Exception("Ya existe una jerarquía con ese nombre.");

                // Truncar campos según StringLength del modelo
                var nombreTruncado = dto.Nombre.Length > 150 ? dto.Nombre.Substring(0, 150) : dto.Nombre;
                var descripcionTruncada = !string.IsNullOrEmpty(dto.Descripcion)
                    ? (dto.Descripcion.Length > 150 ? dto.Descripcion.Substring(0, 150) : dto.Descripcion)
                    : dto.Descripcion;

                var entity = new sl_jerarquia
                {
                    nombre = nombreTruncado,
                    descripcion = descripcionTruncada,
                    createdate = DateTime.Now,
                    createuser = username,
                    deletemark = false,
                    bonificacion = dto.Bonificacion
                };

                ctx.sl_jerarquia.Add(entity);
                
                try
                {
                    ctx.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Se produjeron errores de validación al guardar la jerarquía:");
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        sb.AppendLine($"- Entidad \"{eve.Entry.Entity.GetType().Name}\" (estado {eve.Entry.State}):");
                        foreach (var ve in eve.ValidationErrors)
                        {
                            var msg = ve.ErrorMessage ?? string.Empty;
                            if (msg.Contains("is required"))
                                msg = $"El campo \"{ve.PropertyName}\" es obligatorio.";
                            else if (msg.Contains("maximum length"))
                                msg = $"El campo \"{ve.PropertyName}\" supera la longitud máxima permitida.";
                            else
                                msg = $"{ve.PropertyName}: {msg}";
                            sb.AppendLine("    • " + msg);
                        }
                    }
                    throw new Exception(sb.ToString(), ex);
                }

                return new JerarquiaDetalleDto
                {
                    Id = entity.id,
                    Nombre = entity.nombre,
                    Descripcion = entity.descripcion,
                    Bonificacion = entity.bonificacion,
                    IsDefault = entity.is_default
                };
            }
        }

        // ============================================
        // Actualizar jerarquía
        // ============================================
        public void Actualizar(JerarquiaUpdateDto dto, string username)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre))
                throw new Exception("El nombre de la jerarquía es obligatorio.");

            using (var ctx = new DataContext())
            {
                var entity = ctx.sl_jerarquia.FirstOrDefault(j => j.id == dto.Id && j.deletemark != true);
                if (entity == null)
                    throw new Exception("Jerarquía no encontrada.");

                var existeNombre = ctx.sl_jerarquia.Any(j =>
                    j.id != dto.Id &&
                    j.nombre == dto.Nombre &&
                    j.deletemark != true);

                if (existeNombre)
                    throw new Exception("Ya existe otra jerarquía con ese nombre.");

                // Truncar campos según StringLength del modelo
                entity.nombre = dto.Nombre.Length > 150 ? dto.Nombre.Substring(0, 150) : dto.Nombre;
                entity.descripcion = !string.IsNullOrEmpty(dto.Descripcion)
                    ? (dto.Descripcion.Length > 150 ? dto.Descripcion.Substring(0, 150) : dto.Descripcion)
                    : dto.Descripcion;
                entity.updatedate = DateTime.Now;
                entity.updateuser = username;
                entity.bonificacion = dto.Bonificacion;

                try
                {
                    ctx.SaveChanges();
                }
                catch (DbEntityValidationException ex)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Se produjeron errores de validación al guardar la jerarquía:");
                    foreach (var eve in ex.EntityValidationErrors)
                    {
                        sb.AppendLine($"- Entidad \"{eve.Entry.Entity.GetType().Name}\" (estado {eve.Entry.State}):");
                        foreach (var ve in eve.ValidationErrors)
                        {
                            var msg = ve.ErrorMessage ?? string.Empty;
                            if (msg.Contains("is required"))
                                msg = $"El campo \"{ve.PropertyName}\" es obligatorio.";
                            else if (msg.Contains("maximum length"))
                                msg = $"El campo \"{ve.PropertyName}\" supera la longitud máxima permitida.";
                            else
                                msg = $"{ve.PropertyName}: {msg}";
                            sb.AppendLine("    • " + msg);
                        }
                    }
                    throw new Exception(sb.ToString(), ex);
                }
            }
        }

        // ============================================
        // Baja lógica
        // ============================================
        public void Eliminar(int id, string username)
        {
            using (var ctx = new DataContext())
            {
                var entity = ctx.sl_jerarquia.FirstOrDefault(j => j.id == id && j.deletemark != true);
                if (entity == null)
                    throw new Exception("Jerarquía no encontrada.");

                entity.deletemark = true;
                entity.updatedate = DateTime.Now;
                entity.updateuser = username;

                ctx.SaveChanges();
            }
        }

        // ============================================
        // Activar (quitar baja lógica)
        // ============================================
        public void Activar(int id, string username)
        {
            using (var ctx = new DataContext())
            {
                var entity = ctx.sl_jerarquia.FirstOrDefault(j => j.id == id && j.deletemark == true);
                if (entity == null)
                    throw new Exception("Jerarquía no encontrada.");

                entity.deletemark = false;
                entity.updatedate = DateTime.Now;
                entity.updateuser = username;

                ctx.SaveChanges();
            }
        }

        // ============================================
        // Buscador liviano (para combos / autocomplete)
        // ============================================
        public IEnumerable<JerarquiaListadoDto> Buscar(
            string termino,
            bool activo,
            int maxResultados)
        {
            if (string.IsNullOrWhiteSpace(termino))
                return new List<JerarquiaListadoDto>();

            if (maxResultados <= 0 || maxResultados > 100)
                maxResultados = 20;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                var texto = termino.Trim().ToLower();

                var query =
                    from j in ctx.sl_jerarquia
                    select new JerarquiaListadoDto
                    {
                        Id = j.id,
                        Nombre = j.nombre,
                        Descripcion = j.descripcion,
                        Deletemark = j.deletemark,
                        Bonificacion = j.bonificacion,
                        IsDefault = j.is_default
                    };

                query = activo
                    ? query.Where(x => !x.Deletemark)
                    : query.Where(x => x.Deletemark);

                query = query.Where(x =>
                    (x.Nombre ?? "").ToLower().Contains(texto) ||
                    (x.Descripcion ?? "").ToLower().Contains(texto)
                );

                return query
                    .OrderBy(x => x.Nombre)
                    .Take(maxResultados)
                    .ToList();
            }
        }

        // ================= IMPRESIÓN =================
        public List<JerarquiaImpresionDto> ObtenerDatosImpresion(JerarquiaImpresionRequestDto request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request), "La solicitud de impresión no puede ser nula.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;
                    ctx.Configuration.ProxyCreationEnabled = false;

                    // ===================== LOGGING: Inicio de impresión =====================
                    _logger?.LogInformation("ObtenerDatosImpresion: Iniciando obtención de datos para impresión", new
                    {
                        Estado = request.Estado,
                        IncluirNombre = request.IncluirNombre,
                        IncluirDescripcion = request.IncluirDescripcion,
                        IncluirBonificacion = request.IncluirBonificacion,
                        IncluirEstado = request.IncluirEstado
                    });

                    var query = ctx.sl_jerarquia
                        .AsNoTracking()
                        .AsQueryable();

                    // Aplicar filtro de estado
                    if (request.Estado == "Activo")
                        query = query.Where(j => !j.deletemark);
                    else if (request.Estado == "Inactivo")
                        query = query.Where(j => j.deletemark);
                    // Si es "Todos" o null, no se filtra (retorna todos)

                    // Ordenar por nombre
                    var items = query
                        .OrderBy(j => j.nombre)
                        .Select(j => new JerarquiaImpresionDto
                        {
                            Nombre = request.IncluirNombre ? j.nombre : null,
                            Descripcion = request.IncluirDescripcion ? j.descripcion : null,
                            Bonificacion = request.IncluirBonificacion ? (decimal?)j.bonificacion : null,
                            Estado = request.IncluirEstado ? (j.deletemark ? "Inactivo" : "Activo") : null
                        })
                        .ToList();

                    // ===================== LOGGING: Impresión exitosa =====================
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = items.Count,
                        Estado = request.Estado
                    });

                    return items;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex, new
                {
                    Estado = request?.Estado,
                    ExceptionType = ex.GetType().Name
                });
                throw;
            }
        }
    }
}
