using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using smartlunch_api.App_Start;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioUsuario
    {
        PagedResultDto<UsuarioListadoDto> ObtenerLista(int page, int pageSize, string search, int? plantaId, int? centroCostoId, int? proyectoId, int? jerarquiaId, int? planNutricionalId, bool estado);
        UsuarioDetalleDto ObtenerPorId(int id);
        UsuarioBaseDto ObtenerLegajo(int id);
        UsuarioBaseDto ObtenerPorLegajo(int legajo);
        UsuarioDetalleDto CrearUsuario(UsuarioCreateDto dto, string username);
        void ActualizarUsuario(UsuarioUpdateDto dto, string username);
        void EliminarUsuario(int id, string username);
        void ActivarUsuario(int id, string username);
        IEnumerable<UsuarioBusquedaSimpleDto> BuscarUsuariosSimple(string texto, bool soloActivos = true, int maxResultados = 20);
        List<UsuarioImpresionDto> ObtenerDatosImpresion(UsuarioImpresionRequestDto request);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioUsuario : IServicioUsuario
    {
        private readonly IServicioLogin _servicioLogin;
        private readonly ILoggerService _logger;

        public ServicioUsuario(IServicioLogin servicioLogin = null, ILoggerService logger = null)
        {
            _servicioLogin = servicioLogin ?? new ServicioLogin();
            _logger = logger;
        }

        // ===================== MÉTODOS HELPER =====================

        /// <summary>
        /// Crea el hash de password usando PBKDF2 con salt aleatorio
        /// </summary>
        private void CrearHashPassword(string password, out byte[] salt, out byte[] hash)
        {
            PasswordUtils.CreateHash(password, out salt, out hash);
        }

        /// <summary>
        /// Maneja excepciones de validación de Entity Framework y genera mensajes descriptivos
        /// </summary>
        private Exception HandleValidationException(DbEntityValidationException ex, string operacion)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Se produjeron errores de validación al {operacion} el usuario:");
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
            return new Exception(sb.ToString(), ex);
        }

        // =========================================================
        // Helper: Normaliza createuser/updateuser a minúsculas
        // =========================================================
        private string NormalizarUsuario(string usuario)
        {
            if (string.IsNullOrWhiteSpace(usuario))
                return usuario;

            var usuarioLower = usuario.ToLowerInvariant();
            // Si es "sistema" o "totem", devolver en minúsculas
            if (usuarioLower == "sistema" || usuarioLower == "totem")
                return usuarioLower;

            // Si contiene "sistema" o "totem" (en cualquier caso), normalizar
            if (usuarioLower.Contains("sistema"))
                return "sistema";
            if (usuarioLower.Contains("totem"))
                return "totem";

            // Para otros valores, devolver tal cual
            return usuario;
        }
        // ============= Lista paginada + filtros + buscador =============
        public PagedResultDto<UsuarioListadoDto> ObtenerLista(
            int page,
            int pageSize,
            string search,
            int? plantaId,
            int? centroCostoId,
            int? proyectoId,
            int? jerarquiaId,
            int? planNutricionalId,
            bool estado)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            // Validar longitud de búsqueda
            if (!string.IsNullOrWhiteSpace(search) && search.Length > 200)
                throw new Exception("El texto de búsqueda no puede exceder 200 caracteres.");

            // Validar IDs de filtros
            if (plantaId.HasValue && plantaId.Value <= 0)
                throw new Exception("El ID de la planta debe ser mayor a 0.");

            if (centroCostoId.HasValue && centroCostoId.Value <= 0)
                throw new Exception("El ID del centro de costo debe ser mayor a 0.");

            if (proyectoId.HasValue && proyectoId.Value <= 0)
                throw new Exception("El ID del proyecto debe ser mayor a 0.");

            if (jerarquiaId.HasValue && jerarquiaId.Value <= 0)
                throw new Exception("El ID de la jerarquía debe ser mayor a 0.");

            if (planNutricionalId.HasValue && planNutricionalId.Value <= 0)
                throw new Exception("El ID del plan nutricional debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    // ===================== LOGGING: Inicio de búsqueda =====================
                    _logger?.LogInformation("ObtenerLista: Iniciando búsqueda de usuarios", new
                    {
                        Page = page,
                        PageSize = pageSize,
                        HasSearch = !string.IsNullOrWhiteSpace(search),
                        PlantaId = plantaId,
                        CentroCostoId = centroCostoId,
                        ProyectoId = proyectoId,
                        JerarquiaId = jerarquiaId,
                        PlanNutricionalId = planNutricionalId,
                        Estado = estado
                    });

                var query =
                    ctx.sl_usuario
                        .Where(u => !ctx.sl_login.Any(l => l.usuario_id == u.id && l.username == DatabaseSeeder.SmartTimeUsername))
                        .Include(u => u.planta)
                        .Include(u => u.centrodecosto)
                        .Include(u => u.proyecto)
                        .Include(u => u.jerarquia)
                        .Include(u => u.plannutricional)
                        .Include(u => u.Logins)
                        .Select(u => new UsuarioListadoDto
                        {
                            Id = u.id,
                            Nombre = u.nombre,
                            Apellido = u.apellido,
                            Legajo = u.legajo,
                            Dni = u.dni,
                            Cuil = u.cuil,
                            Email = u.email,
                            Telefono = u.telefono,

                            PlantaId = u.planta_id,
                            PlantaNombre = u.planta != null ? u.planta.descripcion : null,

                            CentroCostoId = u.centrodecosto_id,
                            CentroCostoNombre = u.centrodecosto != null ? u.centrodecosto.descripcion : null,

                            ProyectoId = u.proyecto_id,
                            ProyectoNombre = u.proyecto != null ? u.proyecto.descripcion : null,

                            JerarquiaId = u.jerarquia_id,
                            JerarquiaNombre = u.jerarquia != null ? u.jerarquia.nombre : null,

                            Plannutricional_id = u.plannutricional_id,
                            PlanNutricionalNombre = u.plannutricional != null ? u.plannutricional.nombre : null,

                            FechaIngreso = u.fechaingreso,

                            Pedidos = (int)u.pedidos,
                            Bonificaciones = (int)u.bonificaciones,
                            BonificacionesInvitado = (int)u.bonificaciones_invitado,

                            Estado = !u.deletemark,

                            Username = ctx.sl_login
                            .Where(l => l.usuario_id == u.id)
                            .OrderByDescending(l => l.createdate)
                            .Select(l => l.username)
                            .FirstOrDefault(),

                            Createdate = u.createdate
                        });

                // Activo / inactivo
                query = estado
                    ? query.Where(x => x.Estado)
                    : query.Where(x => !x.Estado);

                // Buscador general
                if (!string.IsNullOrWhiteSpace(search))
                {
                    var s = search.Trim().ToLower();
                    query = query.Where(x =>
                        (x.Nombre ?? "").ToLower().Contains(s) ||
                        (x.Apellido ?? "").ToLower().Contains(s) ||
                        x.Dni.ToString().Contains(s) ||
                        x.Legajo.ToString().Contains(s) ||
                        (x.Cuil ?? "").ToLower().Contains(s) ||
                        (x.Email ?? "").ToLower().Contains(s) ||
                        (x.Username ?? "").ToLower().Contains(s) ||
                        (x.JerarquiaNombre ?? "").ToLower().Contains(s)
                    );
                }

                // Filtros por maestros
                if (plantaId.HasValue && plantaId.Value > 0)
                    query = query.Where(x => x.PlantaId == plantaId.Value);

                if (centroCostoId.HasValue && centroCostoId.Value > 0)
                    query = query.Where(x => x.CentroCostoId == centroCostoId.Value);

                if (proyectoId.HasValue && proyectoId.Value > 0)
                    query = query.Where(x => x.ProyectoId == proyectoId.Value);

                if (jerarquiaId.HasValue && jerarquiaId.Value > 0)
                    query = query.Where(x => x.JerarquiaId == jerarquiaId.Value);

                if (planNutricionalId.HasValue && planNutricionalId.Value > 0)
                    query = query.Where(x => x.Plannutricional_id == planNutricionalId.Value);

                    // Misma persona (dni, nombre, apellido): mostrar solo el último creado (mayor Id)
                    var listadoCompleto = query
                        .OrderBy(x => x.Username != null && x.Username.ToLower() == "admin" ? 0 : 1)
                        .ThenByDescending(x => x.Createdate ?? DateTime.MinValue)
                        .ToList();
                    var listadoUnico = listadoCompleto
                        .GroupBy(x => new { x.Dni, Nombre = (x.Nombre ?? "").Trim(), Apellido = (x.Apellido ?? "").Trim() })
                        .Select(g => g.OrderByDescending(x => x.Id).First())
                        .OrderBy(x => x.Username != null && x.Username.ToLower() == "admin" ? 0 : 1)
                        .ThenByDescending(x => x.Createdate ?? DateTime.MinValue)
                        .ToList();

                    var totalItems = listadoUnico.Count;
                    var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                    var items = listadoUnico
                        .Skip((page - 1) * pageSize)
                        .Take(pageSize)
                        .ToList();

                    // ===================== LOGGING: Búsqueda exitosa =====================
                    _logger?.LogInformation("ObtenerLista: Búsqueda completada exitosamente", new
                    {
                        TotalItems = totalItems,
                        TotalPages = totalPages,
                        ItemsReturned = items.Count
                    });

                    return new PagedResultDto<UsuarioListadoDto>
                    {
                        page = page,
                        pageSize = pageSize,
                        totalItems = totalItems,
                        totalPages = totalPages,
                        items = items
                    };
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerLista: Error al obtener lista de usuarios", ex, new
                {
                    Page = page,
                    PageSize = pageSize,
                    HasSearch = !string.IsNullOrWhiteSpace(search),
                    Estado = estado
                });
                throw;
            }
        }

        // ============= Detalle por Id =============
        public UsuarioDetalleDto ObtenerPorId(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_usuario
                        .Include(u => u.planta)
                        .Include(u => u.centrodecosto)
                        .Include(u => u.proyecto)
                        .Include(u => u.jerarquia)
                        .Include(u => u.plannutricional)
                        .Include(u => u.Logins)
                        .Where(u => u.id == id && !u.deletemark)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerPorId: Usuario no encontrado", new { UsuarioId = id });
                        throw new Exception("Usuario no encontrado.");
                    }

                    // Obtener el login más reciente
                    var login = entity.Logins
                        .Where(l => !l.deletemark)
                        .OrderByDescending(l => l.createdate)
                        .FirstOrDefault();

                    // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                    var resultado = new UsuarioDetalleDto
                    {
                        Id = entity.id,
                        Nombre = entity.nombre,
                        Apellido = entity.apellido,
                        Legajo = entity.legajo,
                        Dni = entity.dni,
                        Cuil = entity.cuil,
                        Domicilio = entity.domicilio,
                        FechaIngreso = entity.fechaingreso,
                        Contrato = entity.contrato,
                        Plannutricional_id = entity.plannutricional_id,
                        PlanNutricionalNombre = entity.plannutricional != null ? entity.plannutricional.nombre : null,
                        PlantaId = entity.planta_id,
                        PlantaNombre = entity.planta != null ? entity.planta.descripcion : null,
                        CentroCostoId = entity.centrodecosto_id,
                        CentroCostoNombre = entity.centrodecosto != null ? entity.centrodecosto.descripcion : null,
                        ProyectoId = entity.proyecto_id,
                        ProyectoNombre = entity.proyecto != null ? entity.proyecto.descripcion : null,
                        JerarquiaId = entity.jerarquia_id,
                        JerarquiaNombre = entity.jerarquia != null ? entity.jerarquia.nombre : null,
                        BonificacionesInvitado = (int)entity.bonificaciones_invitado,
                        Pedidos = (int)entity.pedidos,
                        Bonificaciones = (int)entity.bonificaciones,
                        Descuento = entity.jerarquia != null ? entity.jerarquia.bonificacion : 0,
                        Email = entity.email,
                        Telefono = entity.telefono,
                        Foto = entity.foto,
                        Username = login != null ? login.username : null,
                        Activo = !entity.deletemark
                    };

                    _logger?.LogInformation("ObtenerPorId: Usuario obtenido exitosamente", new
                    {
                        UsuarioId = id,
                        Legajo = resultado.Legajo,
                        Nombre = $"{resultado.Nombre} {resultado.Apellido}"
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Usuario no encontrado")))
            {
                _logger?.LogError("ObtenerPorId: Error al obtener usuario", ex, new { UsuarioId = id });
                throw;
            }
        }

        //UsuarioBaseDto
        public UsuarioBaseDto ObtenerLegajo(int id)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El legajo debe ser mayor a 0.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    var entity = ctx.sl_usuario
                        .Where(u => u.legajo == id && !u.deletemark)
                        .Include(u => u.planta)
                        .Include(u => u.centrodecosto)
                        .Include(u => u.proyecto)
                        .Include(u => u.jerarquia)
                        .Include(u => u.plannutricional)
                        .FirstOrDefault();

                    if (entity == null)
                    {
                        _logger?.LogWarning("ObtenerLegajo: Usuario no encontrado", new { Legajo = id });
                        throw new Exception("Usuario no encontrado.");
                    }

                    // ===================== CONSTRUIR DTO DIRECTAMENTE =====================
                    var resultado = new UsuarioBaseDto
                    {
                        Id = entity.id,
                        Nombre = entity.nombre,
                        Apellido = entity.apellido,
                        Legajo = entity.legajo,
                        Dni = entity.dni,
                        Plannutricional_id = entity.plannutricional_id,
                        PlanNutricionalNombre = entity.plannutricional != null ? entity.plannutricional.nombre : null,
                        PlantaId = entity.planta_id ?? 0,
                        PlantaNombre = entity.planta != null ? entity.planta.descripcion : null,
                        CentroCostoId = entity.centrodecosto_id ?? 0,
                        CentroCostoNombre = entity.centrodecosto != null ? entity.centrodecosto.nombre : null,
                        ProyectoId = entity.proyecto_id ?? 0,
                        ProyectoNombre = entity.proyecto != null ? entity.proyecto.nombre : null,
                        JerarquiaId = entity.jerarquia_id ?? 0,
                        JerarquiaNombre = entity.jerarquia != null ? entity.jerarquia.nombre : null,
                        BonificacionesInvitado = (int)entity.bonificaciones_invitado,
                        Pedidos = (int)entity.pedidos,
                        Bonificaciones = (int)entity.bonificaciones,
                        Descuento = entity.jerarquia != null ? entity.jerarquia.bonificacion : 0,
                        Activo = !entity.deletemark
                    };

                    _logger?.LogInformation("ObtenerLegajo: Usuario obtenido exitosamente", new
                    {
                        Legajo = id,
                        UsuarioId = resultado.Id
                    });

                    return resultado;
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("Usuario no encontrado")))
            {
                _logger?.LogError("ObtenerLegajo: Error al obtener usuario por legajo", ex, new { Legajo = id });
                throw;
            }
        }

        /// <summary>
        /// Obtiene un usuario por su número de legajo (método con nombre claro)
        /// </summary>
        public UsuarioBaseDto ObtenerPorLegajo(int legajo)
        {
            // Reutiliza la lógica de ObtenerLegajo pero con nombre más claro
            return ObtenerLegajo(legajo);
        }

        public UsuarioDetalleDto ObtenerBase_viejo(int id)
        {
            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var dto =
                    ctx.sl_usuario
                        .Where(u => u.id == id)
                        .Include(u => u.planta)
                        .Include(u => u.centrodecosto)
                        .Include(u => u.proyecto)
                        .Include(u => u.jerarquia)
                        .Include(u => u.plannutricional)
                        .Select(u => new UsuarioDetalleDto
                        {
                            Id = u.id,
                            Nombre = u.nombre,
                            Apellido = u.apellido,
                            Legajo = u.legajo,
                            Dni = u.dni,
                            Cuil = u.cuil,

                            Domicilio = u.domicilio,
                            FechaIngreso = u.fechaingreso,
                            Contrato = u.contrato,

                            Plannutricional_id = u.plannutricional_id,
                            PlanNutricionalNombre = u.plannutricional != null ? u.plannutricional.nombre : null,

                            PlantaId = u.planta_id,
                            PlantaNombre = u.planta != null ? u.planta.descripcion : null,

                            CentroCostoId = u.centrodecosto_id,
                            CentroCostoNombre = u.centrodecosto != null ? u.centrodecosto.descripcion : null,

                            ProyectoId = u.proyecto_id,
                            ProyectoNombre = u.proyecto != null ? u.proyecto.descripcion : null,

                            JerarquiaId = u.jerarquia_id,
                            JerarquiaNombre = u.jerarquia != null ? u.jerarquia.nombre : null,

                            BonificacionesInvitado = (int)u.bonificaciones_invitado,
                            Pedidos = (int)u.pedidos,
                            Bonificaciones = (int)u.bonificaciones,

                            Email = u.email,
                            Telefono = u.telefono,
                            Foto = u.foto,

                            //LlaveAccesoNum = u.llave_acceso,
                            //OrigenDatos = u.origen_datos,
                            //FechaUltimaSincronizacion = u.fecha_ultima_sincronizacion,

                            Activo = !u.deletemark,


                        })
                        .FirstOrDefault();

                return dto;
            }
        }

        // ============= Crear usuario =============
        public UsuarioDetalleDto CrearUsuario(UsuarioCreateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null)
                throw new Exception("Datos inválidos.");

            if (string.IsNullOrWhiteSpace(dto.Nombre) ||
                string.IsNullOrWhiteSpace(dto.Apellido))
                throw new Exception("Nombre y Apellido son obligatorios.");

            if (dto.Dni <= 0)
                throw new Exception("DNI inválido.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de creación =====================
                            _logger?.LogInformation("CrearUsuario: Iniciando creación de usuario", new
                            {
                                Nombre = dto.Nombre,
                                Apellido = dto.Apellido,
                                Legajo = dto.Legajo,
                                Dni = dto.Dni,
                                PlantaId = dto.PlantaId,
                                Username = username
                            });

                            // Validar que no exista otro con mismo dni/legajo/cuil (optimizado: una sola consulta)
                            // Preparar valores para la consulta (fuera de LINQ para evitar problemas de traducción)
                            var cuilTrimmed = !string.IsNullOrWhiteSpace(dto.Cuil) ? dto.Cuil.Trim() : null;
                            
                            var existeDuplicado = ctx.sl_usuario
                                .Where(u => !u.deletemark && 
                                    (u.dni == dto.Dni || 
                                     (dto.Legajo > 0 && u.legajo == dto.Legajo) ||
                                     (cuilTrimmed != null && u.cuil == cuilTrimmed)))
                                .Select(u => new { u.dni, u.legajo, u.cuil })
                                .FirstOrDefault();

                            if (existeDuplicado != null)
                            {
                                if (existeDuplicado.dni == dto.Dni)
                                {
                                    _logger?.LogWarning("CrearUsuario: Ya existe un usuario con el mismo DNI", new { Dni = dto.Dni });
                                    throw new Exception("Ya existe un usuario con el mismo DNI.");
                                }
                                if (dto.Legajo > 0 && existeDuplicado.legajo == dto.Legajo)
                                {
                                    _logger?.LogWarning("CrearUsuario: Ya existe un usuario con el mismo legajo", new { Legajo = dto.Legajo });
                                    throw new Exception("Ya existe un usuario con el mismo legajo.");
                                }
                                if (cuilTrimmed != null && existeDuplicado.cuil == cuilTrimmed)
                                {
                                    _logger?.LogWarning("CrearUsuario: Ya existe un usuario con el mismo CUIL", new { Cuil = dto.Cuil });
                                    throw new Exception("Ya existe un usuario con el mismo CUIL.");
                                }
                            }

                            // ====== Validaciones de FKs (optimizado: cargar todas las entidades en paralelo) ======
                            //PLAN
                            if (!dto.Plannutricional_id.HasValue || dto.Plannutricional_id.Value <= 0)
                                throw new Exception("El plan nutricional es obligatorio.");

                            //PLANTA
                            if (!dto.PlantaId.HasValue || dto.PlantaId.Value <= 0)
                                throw new Exception("Planta es obligatorio.");

                            //CENTRO COSTO
                            if (!dto.CentroCostoId.HasValue || dto.CentroCostoId.Value <= 0)
                                throw new Exception("Centro de Costo es obligatorio.");

                            //PROYECTO
                            if (!dto.ProyectoId.HasValue || dto.ProyectoId.Value <= 0)
                                throw new Exception("Proyecto es obligatorio.");

                            //JERARQUIA
                            if (!dto.JerarquiaId.HasValue || dto.JerarquiaId.Value <= 0)
                                throw new Exception("La Jerarquía es obligatoria.");

                            // Cargar todas las entidades relacionadas en una sola consulta (más eficiente)
                            var plan = ctx.sl_plannutricional
                                .Where(p => p.id == dto.Plannutricional_id.Value && !p.deletemark)
                                .Select(p => new { p.id, p.nombre })
                                .FirstOrDefault();

                            var planta = ctx.sl_planta
                                .Where(p => p.id == dto.PlantaId.Value && !p.deletemark)
                                .Select(p => new { p.id, p.descripcion })
                                .FirstOrDefault();

                            var centroCosto = ctx.sl_centrodecosto
                                .Where(c => c.id == dto.CentroCostoId.Value && !c.deletemark)
                                .Select(c => new { c.id, c.descripcion })
                                .FirstOrDefault();

                            var proyecto = ctx.sl_proyecto
                                .Where(p => p.id == dto.ProyectoId.Value && !p.deletemark)
                                .Select(p => new { p.id, p.descripcion })
                                .FirstOrDefault();

                            var jerarquia = ctx.sl_jerarquia
                                .Where(j => j.id == dto.JerarquiaId.Value && !j.deletemark)
                                .Select(j => new { j.id, j.nombre })
                                .FirstOrDefault();

                            // Validar que todas existan
                            if (plan == null)
                            {
                                _logger?.LogWarning("CrearUsuario: Plan nutricional no encontrado", new { PlanNutricionalId = dto.Plannutricional_id });
                                throw new Exception("El plan nutricional seleccionado no existe.");
                            }

                            if (planta == null)
                            {
                                _logger?.LogWarning("CrearUsuario: Planta no encontrada", new { PlantaId = dto.PlantaId });
                                throw new Exception("La planta seleccionada no existe.");
                            }

                            if (centroCosto == null)
                            {
                                _logger?.LogWarning("CrearUsuario: Centro de costo no encontrado", new { CentroCostoId = dto.CentroCostoId });
                                throw new Exception("El centro de costo seleccionado no existe.");
                            }

                            if (proyecto == null)
                            {
                                _logger?.LogWarning("CrearUsuario: Proyecto no encontrado", new { ProyectoId = dto.ProyectoId });
                                throw new Exception("El proyecto seleccionado no existe.");
                            }

                            if (jerarquia == null)
                            {
                                _logger?.LogWarning("CrearUsuario: Jerarquía no encontrada", new { JerarquiaId = dto.JerarquiaId });
                                throw new Exception("La jerarquía seleccionada no existe.");
                            }

                            // Determinar createuser según si tiene username/password o no (siempre en minúsculas)
                            bool tieneCredenciales = !string.IsNullOrWhiteSpace(dto.Username) && 
                                                    !string.IsNullOrWhiteSpace(dto.Password);
                            string createUser = tieneCredenciales ? "sistema" : "totem";

                            var entity = new sl_usuario
                            {
                                nombre = dto.Nombre.Trim(),
                                apellido = dto.Apellido.Trim(),
                                legajo = dto.Legajo,
                                dni = dto.Dni,
                                cuil = string.IsNullOrWhiteSpace(dto.Cuil) ? null : dto.Cuil.Trim(),
                                domicilio = dto.Domicilio,
                                fechaingreso = dto.FechaIngreso,
                                contrato = dto.Contrato,
                                plannutricional_id = dto.Plannutricional_id,
                                planta_id = dto.PlantaId,
                                centrodecosto_id = dto.CentroCostoId,
                                proyecto_id = dto.ProyectoId,
                                jerarquia_id = dto.JerarquiaId,
                                bonificaciones_invitado = dto.BonificacionesInvitado,
                                createdate = DateTime.Now,
                                createuser = createUser,
                                updatedate = null,
                                updateuser = null,
                                deletemark = false,
                                pedidos = 0,
                                bonificaciones = 0,
                                foto = dto.Foto,
                                email = dto.Email,
                                telefono = dto.Telefono,
                                origen_datos = string.IsNullOrWhiteSpace(dto.OrigenDatos) ? "SISTEMA" : dto.OrigenDatos
                            };

                            ctx.sl_usuario.Add(entity);

                            try
                            {
                                // Primero persistir el usuario para que entity.id tenga valor antes de crear el login
                                ctx.SaveChanges();
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "crear");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("CrearUsuario: Deadlock detectado", ex, new { Dni = dto.Dni, Legajo = dto.Legajo });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("CrearUsuario: Error al guardar usuario", ex, new { Dni = dto.Dni, Legajo = dto.Legajo });
                                throw;
                            }

                            // ===================== CREAR LOGIN DENTRO DE LA MISMA TRANSACCIÓN =====================
                            // Siempre crear login para el usuario: con credenciales enviadas o generadas automáticamente
                            string usernameLogin;
                            byte[] salt, hash;
                            bool debeCambiarClave = false;

                            if (tieneCredenciales)
                            {
                                var existeUsername = ctx.sl_login.Any(l =>
                                    l.username == dto.Username.Trim() && !l.deletemark);
                                if (existeUsername)
                                {
                                    _logger?.LogWarning("CrearUsuario: Ya existe un login con ese username", new { Username = dto.Username });
                                    throw new Exception("Ya existe un login con ese username.");
                                }
                                // Misma regla de fortaleza que exige el frontend (evitable llamando a la API directo)
                                PasswordUtils.ValidarFortaleza(dto.Password);
                                CrearHashPassword(dto.Password, out salt, out hash);
                                usernameLogin = dto.Username.Trim();
                                if (usernameLogin.Length > 50) usernameLogin = usernameLogin.Substring(0, 50);
                            }
                            else
                            {
                                // Generar username (legajo) y contraseña automáticamente; el usuario deberá cambiar la clave
                                usernameLogin = entity.legajo.ToString();
                                if (usernameLogin.Length > 50) usernameLogin = usernameLogin.Substring(0, 50);
                                if (ctx.sl_login.Any(l => l.username == usernameLogin && !l.deletemark))
                                    usernameLogin = "usr_" + entity.id;
                                var passwordGenerada = PasswordUtils.GenerarClaveAleatoria(12);
                                PasswordUtils.CreateHash(passwordGenerada, out salt, out hash);
                                debeCambiarClave = true;
                                _logger?.LogInformation("CrearUsuario: Login generado automáticamente para usuario {UsuarioId}, username={Username}, contraseña generada (debe cambiar en primer acceso)", entity.id, usernameLogin);
                            }

                            var createuserTruncado = tieneCredenciales ? "sistema" : "totem";
                            if (createuserTruncado.Length > 50) createuserTruncado = createuserTruncado.Substring(0, 50);

                            var loginEntity = new sl_login
                            {
                                usuario_id = entity.id,
                                username = usernameLogin,
                                password_salt = salt,
                                password_hash = hash,
                                password_iteraciones = PasswordUtils.IteracionesActuales,
                                activo = true,
                                deletemark = false,
                                debe_cambiar_clave = debeCambiarClave,
                                createdate = DateTime.Now,
                                createuser = createuserTruncado
                            };

                            ctx.sl_login.Add(loginEntity);

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "crear");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("CrearUsuario: Deadlock detectado", ex, new { Dni = dto.Dni, Legajo = dto.Legajo });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("CrearUsuario: Error al guardar login", ex, new { Dni = dto.Dni, Legajo = dto.Legajo });
                                throw;
                            }

                            transaction.Commit();

                            // ===================== CONSTRUIR DTO DIRECTAMENTE (evitar query adicional) =====================
                            // Usar el username del DTO si tiene credenciales, no hacer query adicional
                            var resultado = new UsuarioDetalleDto
                            {
                                Id = entity.id,
                                Nombre = entity.nombre,
                                Apellido = entity.apellido,
                                Legajo = entity.legajo,
                                Dni = entity.dni,
                                Cuil = entity.cuil,
                                Domicilio = entity.domicilio,
                                FechaIngreso = entity.fechaingreso,
                                Contrato = entity.contrato,
                                Plannutricional_id = entity.plannutricional_id,
                                PlanNutricionalNombre = plan.nombre,
                                PlantaId = entity.planta_id,
                                PlantaNombre = planta.descripcion,
                                CentroCostoId = entity.centrodecosto_id,
                                CentroCostoNombre = centroCosto.descripcion,
                                ProyectoId = entity.proyecto_id,
                                ProyectoNombre = proyecto.descripcion,
                                JerarquiaId = entity.jerarquia_id,
                                JerarquiaNombre = jerarquia.nombre,
                                BonificacionesInvitado = (int)entity.bonificaciones_invitado,
                                Pedidos = (int)entity.pedidos,
                                Bonificaciones = (int)entity.bonificaciones,
                                Email = entity.email,
                                Telefono = entity.telefono,
                                Foto = entity.foto,
                                Username = usernameLogin,
                                Activo = !entity.deletemark
                            };

                            _logger?.LogInformation("CrearUsuario: Usuario creado exitosamente", new
                            {
                                UsuarioId = entity.id,
                                Legajo = resultado.Legajo,
                                Nombre = $"{resultado.Nombre} {resultado.Apellido}",
                                TieneCredenciales = tieneCredenciales
                            });

                            return resultado;
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("obligatorio") || ex.Message.Contains("no existe") || ex.Message.Contains("inválido"))))
            {
                _logger?.LogError("CrearUsuario: Error al crear usuario", ex, new
                {
                    Nombre = dto?.Nombre,
                    Apellido = dto?.Apellido,
                    Dni = dto?.Dni,
                    Legajo = dto?.Legajo
                });
                throw;
            }
        }

        // ============= Actualizar usuario =============
        public void ActualizarUsuario(UsuarioUpdateDto dto, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (dto == null || dto.Id <= 0)
                throw new Exception("Datos inválidos.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            if (string.IsNullOrWhiteSpace(dto.Nombre) ||
                string.IsNullOrWhiteSpace(dto.Apellido))
                throw new Exception("Nombre y Apellido son obligatorios.");

            if (dto.Dni <= 0)
                throw new Exception("DNI inválido.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de actualización =====================
                            _logger?.LogInformation("ActualizarUsuario: Iniciando actualización de usuario", new
                            {
                                UsuarioId = dto.Id,
                                Nombre = dto.Nombre,
                                Apellido = dto.Apellido,
                                Username = username
                            });

                            var entity = ctx.sl_usuario.FirstOrDefault(u => u.id == dto.Id && !u.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActualizarUsuario: Usuario no encontrado", new { UsuarioId = dto.Id });
                                throw new Exception("Usuario no encontrado.");
                            }

                            // DNI / legajo / CUIL duplicado (excluyendo el mismo) - dentro de transacción
                            var existeDni = ctx.sl_usuario.Any(u => u.id != dto.Id && u.dni == dto.Dni && !u.deletemark);
                            if (existeDni)
                            {
                                _logger?.LogWarning("ActualizarUsuario: Ya existe otro usuario con el mismo DNI", new { Dni = dto.Dni });
                                throw new Exception("Ya existe otro usuario con el mismo DNI.");
                            }

                            var existeLegajo = ctx.sl_usuario.Any(u => u.id != dto.Id && u.legajo == dto.Legajo && !u.deletemark);
                            if (existeLegajo)
                            {
                                _logger?.LogWarning("ActualizarUsuario: Ya existe otro usuario con el mismo legajo", new { Legajo = dto.Legajo });
                                throw new Exception("Ya existe otro usuario con el mismo legajo.");
                            }

                            // Validar que no exista otro con el mismo CUIL (excluyendo el mismo)
                            if (!string.IsNullOrWhiteSpace(dto.Cuil))
                            {
                                var existeCuil = ctx.sl_usuario.Any(u => u.id != dto.Id && u.cuil == dto.Cuil.Trim() && !u.deletemark);
                                if (existeCuil)
                                {
                                    _logger?.LogWarning("ActualizarUsuario: Ya existe otro usuario con el mismo CUIL", new { Cuil = dto.Cuil });
                                    throw new Exception("Ya existe otro usuario con el mismo CUIL.");
                                }
                            }

                            // Validar FKs si se proporcionan
                            if (dto.Plannutricional_id.HasValue && dto.Plannutricional_id.Value > 0)
                            {
                                var existePlan = ctx.sl_plannutricional.Any(p => p.id == dto.Plannutricional_id.Value && !p.deletemark);
                                if (!existePlan)
                                {
                                    _logger?.LogWarning("ActualizarUsuario: Plan nutricional no encontrado", new { PlanNutricionalId = dto.Plannutricional_id });
                                    throw new Exception("El plan nutricional seleccionado no existe.");
                                }
                            }

                            if (dto.PlantaId.HasValue && dto.PlantaId.Value > 0)
                            {
                                var existePlanta = ctx.sl_planta.Any(p => p.id == dto.PlantaId.Value && !p.deletemark);
                                if (!existePlanta)
                                {
                                    _logger?.LogWarning("ActualizarUsuario: Planta no encontrada", new { PlantaId = dto.PlantaId });
                                    throw new Exception("La planta seleccionada no existe.");
                                }
                            }

                            if (dto.CentroCostoId.HasValue && dto.CentroCostoId.Value > 0)
                            {
                                var existeCentro = ctx.sl_centrodecosto.Any(c => c.id == dto.CentroCostoId.Value && !c.deletemark);
                                if (!existeCentro)
                                {
                                    _logger?.LogWarning("ActualizarUsuario: Centro de costo no encontrado", new { CentroCostoId = dto.CentroCostoId });
                                    throw new Exception("El centro de costo seleccionado no existe.");
                                }
                            }

                            if (dto.ProyectoId.HasValue && dto.ProyectoId.Value > 0)
                            {
                                var existeProyecto = ctx.sl_proyecto.Any(p => p.id == dto.ProyectoId.Value && !p.deletemark);
                                if (!existeProyecto)
                                {
                                    _logger?.LogWarning("ActualizarUsuario: Proyecto no encontrado", new { ProyectoId = dto.ProyectoId });
                                    throw new Exception("El proyecto seleccionado no existe.");
                                }
                            }

                            if (dto.JerarquiaId.HasValue && dto.JerarquiaId.Value > 0)
                            {
                                var existeJerarquia = ctx.sl_jerarquia.Any(j => j.id == dto.JerarquiaId.Value && !j.deletemark);
                                if (!existeJerarquia)
                                {
                                    _logger?.LogWarning("ActualizarUsuario: Jerarquía no encontrada", new { JerarquiaId = dto.JerarquiaId });
                                    throw new Exception("La jerarquía seleccionada no existe.");
                                }
                            }

                            entity.nombre = dto.Nombre.Trim();
                            entity.apellido = dto.Apellido.Trim();
                            entity.legajo = dto.Legajo;
                            entity.dni = dto.Dni;
                            entity.cuil = string.IsNullOrWhiteSpace(dto.Cuil) ? null : dto.Cuil.Trim();
                            entity.domicilio = dto.Domicilio;
                            entity.fechaingreso = dto.FechaIngreso;
                            entity.contrato = dto.Contrato;
                            entity.plannutricional_id = dto.Plannutricional_id;
                            entity.planta_id = dto.PlantaId;
                            entity.centrodecosto_id = dto.CentroCostoId;
                            entity.proyecto_id = dto.ProyectoId;
                            entity.jerarquia_id = dto.JerarquiaId;
                            entity.bonificaciones_invitado = dto.BonificacionesInvitado;
                            entity.bonificaciones = dto.Bonificaciones;
                            entity.email = dto.Email;
                            entity.telefono = dto.Telefono;
                            entity.foto = dto.Foto;
                            entity.origen_datos = "sistema";
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = NormalizarUsuario(username);

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "actualizar");
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActualizarUsuario: Deadlock detectado", ex, new { UsuarioId = dto.Id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("ActualizarUsuario: Error al guardar usuario", ex, new { UsuarioId = dto.Id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("ActualizarUsuario: Usuario actualizado exitosamente", new
                            {
                                UsuarioId = dto.Id,
                                Legajo = entity.legajo,
                                Nombre = $"{entity.nombre} {entity.apellido}"
                            });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("Ya existe") || ex.Message.Contains("obligatorio") || ex.Message.Contains("no existe") || ex.Message.Contains("inválido") || ex.Message.Contains("no encontrado"))))
            {
                _logger?.LogError("ActualizarUsuario: Error al actualizar usuario", ex, new
                {
                    UsuarioId = dto?.Id,
                    Nombre = dto?.Nombre,
                    Apellido = dto?.Apellido
                });
                throw;
            }
        }

        // ============= Baja lógica =============
        public void EliminarUsuario(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de eliminación =====================
                            _logger?.LogInformation("EliminarUsuario: Iniciando eliminación lógica de usuario", new
                            {
                                UsuarioId = id,
                                Username = username
                            });

                            var entity = ctx.sl_usuario.FirstOrDefault(u => u.id == id && !u.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("EliminarUsuario: Usuario no encontrado", new { UsuarioId = id });
                                throw new Exception("Usuario no encontrado.");
                            }

                            entity.deletemark = true;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = NormalizarUsuario(username);

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("EliminarUsuario: Deadlock detectado", ex, new { UsuarioId = id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "eliminar");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("EliminarUsuario: Error al guardar eliminación", ex, new { UsuarioId = id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("EliminarUsuario: Usuario eliminado exitosamente", new
                            {
                                UsuarioId = id,
                                Legajo = entity.legajo,
                                Nombre = $"{entity.nombre} {entity.apellido}"
                            });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("no encontrado")))
            {
                _logger?.LogError("EliminarUsuario: Error al eliminar usuario", ex, new { UsuarioId = id });
                throw;
            }
        }

        // ============= Activar (quitar baja lógica) =============
        public void ActivarUsuario(int id, string username)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (id <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            if (string.IsNullOrWhiteSpace(username))
                throw new Exception("El nombre de usuario es obligatorio.");

            try
            {
                using (var ctx = new DataContext())
                {
                    using (var transaction = ctx.Database.BeginTransaction(IsolationLevel.Serializable))
                    {
                        try
                        {
                            // ===================== LOGGING: Inicio de activación =====================
                            _logger?.LogInformation("ActivarUsuario: Iniciando activación de usuario", new
                            {
                                UsuarioId = id,
                                Username = username
                            });

                            var entity = ctx.sl_usuario.FirstOrDefault(u => u.id == id && u.deletemark);
                            if (entity == null)
                            {
                                _logger?.LogWarning("ActivarUsuario: Usuario no encontrado", new { UsuarioId = id });
                                throw new Exception("Usuario no encontrado.");
                            }

                            entity.deletemark = false;
                            entity.updatedate = DateTime.Now;
                            entity.updateuser = NormalizarUsuario(username);

                            try
                            {
                                ctx.SaveChanges();
                            }
                            catch (SqlException ex) when (ex.Number == 1205)
                            {
                                transaction.Rollback();
                                _logger?.LogError("ActivarUsuario: Deadlock detectado", ex, new { UsuarioId = id });
                                throw new Exception("Error de concurrencia. Por favor, intente nuevamente.");
                            }
                            catch (DbEntityValidationException ex)
                            {
                                throw HandleValidationException(ex, "activar");
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError("ActivarUsuario: Error al guardar activación", ex, new { UsuarioId = id });
                                throw;
                            }

                            transaction.Commit();

                            _logger?.LogInformation("ActivarUsuario: Usuario activado exitosamente", new
                            {
                                UsuarioId = id,
                                Legajo = entity.legajo,
                                Nombre = $"{entity.nombre} {entity.apellido}"
                            });
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
            catch (Exception ex) when (!(ex is Exception && ex.Message.Contains("no encontrado")))
            {
                _logger?.LogError("ActivarUsuario: Error al activar usuario", ex, new { UsuarioId = id });
                throw;
            }
        }

        // =========================================================
        // Buscador simple: devuelve solo legajo y nombre completo
        // Busca por legajo o nombre, solo a partir de la cuarta letra
        // =========================================================
        public IEnumerable<UsuarioBusquedaSimpleDto> BuscarUsuariosSimple(string texto, bool soloActivos = true, int maxResultados = 20)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (maxResultados <= 0 || maxResultados > 100)
                maxResultados = 20;

            if (!string.IsNullOrWhiteSpace(texto) && texto.Length > 200)
                throw new Exception("El texto de búsqueda no puede exceder 200 caracteres.");

            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    _logger?.LogInformation("BuscarUsuariosSimple: Iniciando búsqueda simple de usuarios", new
                    {
                        HasTexto = !string.IsNullOrWhiteSpace(texto),
                        SoloActivos = soloActivos,
                        MaxResultados = maxResultados
                    });

                var query = ctx.sl_usuario
                    .Where(u => !ctx.sl_login.Any(l => l.usuario_id == u.id && l.username == DatabaseSeeder.SmartTimeUsername))
                    .AsQueryable();

                if (soloActivos)
                    query = query.Where(u => !u.deletemark);

                // Solo buscar si el texto tiene al menos 4 caracteres
                if (!string.IsNullOrWhiteSpace(texto))
                {
                    var s = texto.Trim();
                    
                    // Si tiene menos de 4 caracteres, devolver lista vacía
                    if (s.Length < 4)
                    {
                        return new List<UsuarioBusquedaSimpleDto>();
                    }
                    
                    var sLower = s.ToLower();
                    
                    // Intentar parsear como número (legajo)
                    int legajoBuscado = 0;
                    bool esNumero = int.TryParse(s, out legajoBuscado);
                    
                    if (esNumero)
                    {
                        // Buscar por legajo exacto o que contenga el número
                        query = query.Where(u => 
                            u.legajo == legajoBuscado || 
                            u.legajo.ToString().Contains(s)
                        );
                    }
                    else
                    {
                        // Buscar por nombre o apellido
                        query = query.Where(u =>
                            (u.nombre ?? "").ToLower().Contains(sLower) ||
                            (u.apellido ?? "").ToLower().Contains(sLower) ||
                            ((u.nombre ?? "") + " " + (u.apellido ?? "")).ToLower().Contains(sLower)
                        );
                    }
                }
                else
                {
                    // Si no hay texto, devolver lista vacía
                    return new List<UsuarioBusquedaSimpleDto>();
                }

                    var resultados = query
                        .OrderBy(u => u.legajo)
                        .ThenBy(u => u.apellido)
                        .ThenBy(u => u.nombre)
                        .Take(maxResultados)
                        .Select(u => new UsuarioBusquedaSimpleDto
                        {
                            Legajo = u.legajo,
                            Nombre = (u.nombre ?? "") + " " + (u.apellido ?? "")
                        })
                        .ToList();

                    _logger?.LogInformation("BuscarUsuariosSimple: Búsqueda completada exitosamente", new
                    {
                        TotalResultados = resultados.Count
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("BuscarUsuariosSimple: Error al buscar usuarios", ex, new
                {
                    HasTexto = !string.IsNullOrWhiteSpace(texto),
                    SoloActivos = soloActivos
                });
                throw;
            }
        }

        // ========== Obtener datos para impresión ==========
        public List<UsuarioImpresionDto> ObtenerDatosImpresion(UsuarioImpresionRequestDto request)
        {
            try
            {
                using (var ctx = new DataContext())
                {
                    ctx.Configuration.LazyLoadingEnabled = false;

                    _logger?.LogInformation("ObtenerDatosImpresion: Iniciando obtención de datos para impresión", new
                    {
                        IncluirNombre = request?.IncluirNombre ?? false,
                        IncluirApellido = request?.IncluirApellido ?? false,
                        IncluirLegajo = request?.IncluirLegajo ?? false,
                        IncluirDni = request?.IncluirDni ?? false,
                        IncluirEmail = request?.IncluirEmail ?? false,
                        IncluirTelefono = request?.IncluirTelefono ?? false,
                        IncluirPlanta = request?.IncluirPlanta ?? false,
                        IncluirCentroCosto = request?.IncluirCentroCosto ?? false,
                        IncluirProyecto = request?.IncluirProyecto ?? false,
                        IncluirJerarquia = request?.IncluirJerarquia ?? false,
                        IncluirPlanNutricional = request?.IncluirPlanNutricional ?? false,
                        IncluirEstado = request?.IncluirEstado ?? false,
                        Estado = request?.Estado,
                        PlantaId = request?.PlantaId,
                        CentroCostoId = request?.CentroCostoId,
                        ProyectoId = request?.ProyectoId
                    });

                    var query = ctx.sl_usuario
                        .Where(u => !ctx.sl_login.Any(l => l.usuario_id == u.id && l.username == DatabaseSeeder.SmartTimeUsername))
                        .Include(u => u.planta)
                        .Include(u => u.centrodecosto)
                        .Include(u => u.proyecto)
                        .Include(u => u.jerarquia)
                        .Include(u => u.plannutricional)
                        .AsQueryable();

                    // Filtrar por estado
                    if (!string.IsNullOrWhiteSpace(request?.Estado) && request.Estado != "Todos")
                    {
                        if (request.Estado == "Activo")
                            query = query.Where(u => !u.deletemark);
                        else if (request.Estado == "Inactivo")
                            query = query.Where(u => u.deletemark);
                    }

                    // Filtros adicionales
                    if (request?.PlantaId.HasValue == true && request.PlantaId.Value > 0)
                        query = query.Where(u => u.planta_id == request.PlantaId.Value);

                    if (request?.CentroCostoId.HasValue == true && request.CentroCostoId.Value > 0)
                        query = query.Where(u => u.centrodecosto_id == request.CentroCostoId.Value);

                    if (request?.ProyectoId.HasValue == true && request.ProyectoId.Value > 0)
                        query = query.Where(u => u.proyecto_id == request.ProyectoId.Value);

                    var usuarios = query
                        .OrderBy(u => u.apellido)
                        .ThenBy(u => u.nombre)
                        .ToList();

                    var resultados = new List<UsuarioImpresionDto>();

                    foreach (var usuario in usuarios)
                    {
                        var dto = new UsuarioImpresionDto();

                        if (request?.IncluirNombre == true)
                            dto.Nombre = usuario.nombre;

                        if (request?.IncluirApellido == true)
                            dto.Apellido = usuario.apellido;

                        if (request?.IncluirLegajo == true)
                            dto.Legajo = usuario.legajo;

                        if (request?.IncluirDni == true)
                            dto.Dni = usuario.dni;

                        if (request?.IncluirEmail == true)
                            dto.Email = usuario.email;

                        if (request?.IncluirTelefono == true)
                            dto.Telefono = usuario.telefono;

                        if (request?.IncluirPlanta == true && usuario.planta != null)
                            dto.Planta = usuario.planta.descripcion;

                        if (request?.IncluirCentroCosto == true && usuario.centrodecosto != null)
                            dto.CentroCosto = usuario.centrodecosto.descripcion;

                        if (request?.IncluirProyecto == true && usuario.proyecto != null)
                            dto.Proyecto = usuario.proyecto.descripcion;

                        if (request?.IncluirJerarquia == true && usuario.jerarquia != null)
                            dto.Jerarquia = usuario.jerarquia.nombre;

                        if (request?.IncluirPlanNutricional == true && usuario.plannutricional != null)
                            dto.PlanNutricional = usuario.plannutricional.nombre;

                        if (request?.IncluirEstado == true)
                            dto.Estado = !usuario.deletemark ? "Activo" : "Inactivo";

                        resultados.Add(dto);
                    }

                    var totalItems = resultados.Count;
                    _logger?.LogInformation("ObtenerDatosImpresion: Datos obtenidos exitosamente", new
                    {
                        TotalItems = totalItems
                    });

                    return resultados;
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerDatosImpresion: Error al obtener datos para impresión", ex);
                throw;
            }
        }

    }
}
