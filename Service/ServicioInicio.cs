using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using smartlunch_api.Dtos;
using smartlunch_api.Models;
using smartlunch_api.Services;
using System.IO;
using System.Text;
using System.Data.Entity;

namespace smartlunch_api.Service
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioInicio
    {
        InicioBaseDto ObtenerTotem(int usuarioId, DateTime? fecha = null);
        InicioBaseDto ObtenerTotem(UsuarioBaseDto usuario, DateTime? fecha = null); // Sobrecarga: acepta usuario completo
        InicioWebDto ObtenerWeb(int usuarioId, DateTime? fecha = null);
        InicioWebDto ObtenerWeb(UsuarioBaseDto usuario, DateTime? fecha = null); // Sobrecarga: acepta usuario completo
        InicioWebDto ObtenerWebActualizado(int usuarioId, int turnoId, DateTime? fecha = null);
        InicioWebDto ObtenerWebActualizado(UsuarioBaseDto usuario, int turnoId, DateTime? fecha = null); // Sobrecarga: acepta usuario completo
        InicioWebDto ObtenerTotemActualizado(UsuarioBaseDto usuario, int turnoId, DateTime? fecha = null); // Para tótem sin token
        InicioBaseDto ObtenerBase(int usuarioId);

        // Versiones async: usadas por /api/inicio/web y /api/inicio/web-actualizado, que el front consulta
        // cada 2 segundos mientras el comensal tiene la pantalla de Inicio abierta. Sin esto, cada request
        // bloqueaba un hilo de IIS durante todo el tiempo de las consultas a SQL Server.
        Task<InicioWebDto> ObtenerWebAsync(int usuarioId, DateTime? fecha = null);
        Task<InicioWebDto> ObtenerWebAsync(UsuarioBaseDto usuario, DateTime? fecha = null);
        Task<InicioWebDto> ObtenerWebActualizadoAsync(int usuarioId, int turnoId, DateTime? fecha = null);
        Task<InicioWebDto> ObtenerWebActualizadoAsync(UsuarioBaseDto usuario, int turnoId, DateTime? fecha = null);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioInicio : IServicioInicio
    {
        private readonly IServicioUsuario _servicioUsuario;
        private readonly IServicioTurno _servicioTurno;
        private readonly IServicioMenudd _servicioMenuDia;
        private readonly IServicioComanda _servicioPedido;
        private readonly ILoggerService _logger;

        // Constructor por defecto (para compatibilidad)
        public ServicioInicio(ILoggerService logger = null)
            : this(new ServicioUsuario(), new ServicioTurno(), new ServicioMenudd(), new ServicioComanda(), logger)
        {
        }

        // Constructor con inyección de dependencias
        public ServicioInicio(IServicioUsuario servicioUsuario, IServicioTurno servicioTurno, IServicioMenudd servicioMenuDia, IServicioComanda servicioPedido, ILoggerService logger = null)
        {
            _servicioUsuario = servicioUsuario ?? throw new ArgumentNullException(nameof(servicioUsuario));
            _servicioTurno = servicioTurno ?? throw new ArgumentNullException(nameof(servicioTurno));
            _servicioMenuDia = servicioMenuDia ?? throw new ArgumentNullException(nameof(servicioMenuDia));
            _servicioPedido = servicioPedido ?? throw new ArgumentNullException(nameof(servicioPedido));
            _logger = logger;
        }

        // Método que acepta usuarioId (busca el usuario)
        public InicioBaseDto ObtenerTotem(int usuarioId, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            if (fecha.HasValue)
            {
                var fechaValidada = fecha.Value.Date;
                var hoy = DateTime.Today;
                if (fechaValidada > hoy)
                    throw new Exception("No se puede consultar fechas futuras.");
                if ((hoy - fechaValidada).Days > 30)
                    throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");
            }

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerTotem: Iniciando obtención de datos para tótem", new
                {
                    UsuarioId = usuarioId,
                    Fecha = fecha
                });

                // Obtener y mapear usuario usando método común
                var usuarioBase = ObtenerUsuarioBasePorId(usuarioId);
                
                // Usar método común para obtener datos base
                var resultado = ObtenerTotem(usuarioBase, fecha);

                _logger?.LogInformation("ObtenerTotem: Datos obtenidos exitosamente", new
                {
                    UsuarioId = usuarioId,
                    Legajo = usuarioBase.Legajo,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerTotem: Error al obtener datos para tótem", ex, new
                {
                    UsuarioId = usuarioId,
                    Fecha = fecha
                });
                throw;
            }
        }

        // Método optimizado que acepta usuario completo (sin buscar en BD)
        public InicioBaseDto ObtenerTotem(UsuarioBaseDto usuario, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            if (fecha.HasValue)
            {
                var fechaValidada = fecha.Value.Date;
                var hoy = DateTime.Today;
                if (fechaValidada > hoy)
                    throw new Exception("No se puede consultar fechas futuras.");
                if ((hoy - fechaValidada).Days > 30)
                    throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");
            }

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerTotem: Iniciando obtención de datos para tótem (usuario completo)", new
                {
                    UsuarioId = usuario.Id,
                    Legajo = usuario.Legajo,
                    Fecha = fecha
                });

                // Usar método común para obtener datos base
                var resultado = ObtenerBaseComun(usuario, fecha);

                _logger?.LogInformation("ObtenerTotem: Datos obtenidos exitosamente (usuario completo)", new
                {
                    UsuarioId = usuario.Id,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerTotem: Error al obtener datos para tótem (usuario completo)", ex, new
                {
                    UsuarioId = usuario?.Id,
                    Fecha = fecha
                });
                throw;
            }
        }

        /// <summary>
        /// Método helper para obtener y mapear usuario por ID
        /// Reutilizado por Totem y Web para evitar duplicación
        /// Optimizado: intenta usar ObtenerLegajo si es posible, sino mapea desde UsuarioDetalleDto
        /// </summary>
        private UsuarioBaseDto ObtenerUsuarioBasePorId(int usuarioId)
        {
            try
            {
                // Intentar obtener usando ObtenerLegajo (más eficiente si tenemos el legajo)
                // Si no funciona, usar ObtenerPorId y mapear
                UsuarioBaseDto usuarioBase = null;

                try
                {
                    // Primero intentar obtener el usuario completo
                    var usuario = _servicioUsuario.ObtenerPorId(usuarioId);
                    if (usuario == null)
                        throw new Exception($"No se encontró el usuario con ID {usuarioId}.");

                    // Validar que el usuario esté activo
                    if (!usuario.Activo)
                    {
                        _logger?.LogWarning("ObtenerUsuarioBasePorId: Usuario inactivo", new { UsuarioId = usuarioId });
                        throw new Exception($"El usuario con ID {usuarioId} está inactivo.");
                    }

                    // Bonificaciones aplicadas: siempre calculadas desde sl_comanda (día, estado R, bonificado = true)
                    var bonifAplicadas = ObtenerBonificacionesAplicadasHoy(usuario.Id, DateTime.Today);

                    // Mapear a UsuarioBaseDto
                    usuarioBase = new UsuarioBaseDto
                    {
                        Id = usuario.Id,
                        Nombre = usuario.Nombre,
                        Apellido = usuario.Apellido,
                        Legajo = usuario.Legajo,
                        Dni = usuario.Dni,
                        Plannutricional_id = usuario.Plannutricional_id,
                        PlanNutricionalNombre = usuario.PlanNutricionalNombre,
                        PlantaId = usuario.PlantaId ?? 0,
                        PlantaNombre = usuario.PlantaNombre,
                        CentroCostoId = usuario.CentroCostoId ?? 0,
                        CentroCostoNombre = usuario.CentroCostoNombre,
                        ProyectoId = usuario.ProyectoId ?? 0,
                        ProyectoNombre = usuario.ProyectoNombre,
                        JerarquiaId = usuario.JerarquiaId ?? 0,
                        JerarquiaNombre = usuario.JerarquiaNombre,
                        Bonificaciones = usuario.Bonificaciones,
                        BonificacionesInvitado = usuario.BonificacionesInvitado,
                        Pedidos = usuario.Pedidos,
                        BonificacionesAplicadas = bonifAplicadas,
                        Descuento = usuario.Descuento,
                        Activo = usuario.Activo
                    };
                }
                catch (Exception ex) when (ex.Message.Contains("no encontrado") || ex.Message.Contains("inactivo"))
                {
                    throw; // Re-lanzar excepciones de negocio
                }

                return usuarioBase;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerUsuarioBasePorId: Error al obtener usuario", ex, new { UsuarioId = usuarioId });
                throw;
            }
        }

        // Versión async del método anterior: usada por el polling de Inicio (cada 2s).
        private async Task<UsuarioBaseDto> ObtenerUsuarioBasePorIdAsync(int usuarioId)
        {
            try
            {
                var usuario = await _servicioUsuario.ObtenerPorIdAsync(usuarioId);
                if (usuario == null)
                    throw new Exception($"No se encontró el usuario con ID {usuarioId}.");

                if (!usuario.Activo)
                {
                    _logger?.LogWarning("ObtenerUsuarioBasePorIdAsync: Usuario inactivo", new { UsuarioId = usuarioId });
                    throw new Exception($"El usuario con ID {usuarioId} está inactivo.");
                }

                var bonifAplicadas = await ObtenerBonificacionesAplicadasHoyAsync(usuario.Id, DateTime.Today);

                return new UsuarioBaseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Apellido = usuario.Apellido,
                    Legajo = usuario.Legajo,
                    Dni = usuario.Dni,
                    Plannutricional_id = usuario.Plannutricional_id,
                    PlanNutricionalNombre = usuario.PlanNutricionalNombre,
                    PlantaId = usuario.PlantaId ?? 0,
                    PlantaNombre = usuario.PlantaNombre,
                    CentroCostoId = usuario.CentroCostoId ?? 0,
                    CentroCostoNombre = usuario.CentroCostoNombre,
                    ProyectoId = usuario.ProyectoId ?? 0,
                    ProyectoNombre = usuario.ProyectoNombre,
                    JerarquiaId = usuario.JerarquiaId ?? 0,
                    JerarquiaNombre = usuario.JerarquiaNombre,
                    Bonificaciones = usuario.Bonificaciones,
                    BonificacionesInvitado = usuario.BonificacionesInvitado,
                    Pedidos = usuario.Pedidos,
                    BonificacionesAplicadas = bonifAplicadas,
                    Descuento = usuario.Descuento,
                    Activo = usuario.Activo
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerUsuarioBasePorIdAsync: Error al obtener usuario", ex, new { UsuarioId = usuarioId });
                throw;
            }
        }

        /// <summary>
        /// Calcula la cantidad de bonificaciones aplicadas para un usuario en una fecha.
        /// Cuenta en sl_comanda: mismo usuario, día de la fecha, bonificado = true.
        /// No se cuentan los que tienen estado D (Devuelto) ni C (Cancelado).
        /// </summary>
        private int ObtenerBonificacionesAplicadasHoy(int usuarioId, DateTime fecha)
        {
            var hoy = fecha.Date;
            var diaSiguiente = hoy.AddDays(1);
            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                return ctx.sl_comanda.Count(c => c.usuario_id == usuarioId
                        && !c.deletemark
                        && c.fecha >= hoy
                        && c.fecha < diaSiguiente
                        && c.bonificado == true
                        && c.estado != "D"
                        && c.estado != "C");
            }
        }

        // Versión async del método anterior.
        private async Task<int> ObtenerBonificacionesAplicadasHoyAsync(int usuarioId, DateTime fecha)
        {
            var hoy = fecha.Date;
            var diaSiguiente = hoy.AddDays(1);
            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;
                return await ctx.sl_comanda.CountAsync(c => c.usuario_id == usuarioId
                        && !c.deletemark
                        && c.fecha >= hoy
                        && c.fecha < diaSiguiente
                        && c.bonificado == true
                        && c.estado != "D"
                        && c.estado != "C");
            }
        }

        /// <summary>
        /// Método común que obtiene los datos base (usuario, turnos, menú del día)
        /// Usado tanto por Totem como por Web para garantizar consistencia total
        /// Ambos métodos retornan EXACTAMENTE los mismos datos base
        /// </summary>
        private InicioBaseDto ObtenerBaseComun(UsuarioBaseDto usuario, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            // Validar que el usuario esté activo
            if (!usuario.Activo)
                throw new Exception("El usuario está inactivo.");

            // Validar que el usuario tenga los datos necesarios
            if (usuario.PlantaId <= 0)
                throw new Exception("El usuario no tiene planta asignada.");

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerBaseComun: Iniciando obtención de datos base", new
                {
                    UsuarioId = usuario.Id,
                    Legajo = usuario.Legajo,
                    PlantaId = usuario.PlantaId,
                    Fecha = hoy
                });

                // ===================== OBTENER TURNOS DISPONIBLES =====================
                var turnos = _servicioTurno
                    .ObtenerHorarioCombo()
                    .ToList();

                if (turnos == null)
                {
                    _logger?.LogWarning("ObtenerBaseComun: La lista de turnos es null");
                    throw new Exception("Error al obtener turnos disponibles.");
                }

                if (!turnos.Any())
                {
                    _logger?.LogWarning("ObtenerBaseComun: No hay turnos disponibles");
                    throw new Exception("No hay turnos disponibles.");
                }

                // Validar que todos los turnos tengan datos válidos
                var turnosInvalidos = turnos.Where(t => t == null || t.Id <= 0).ToList();
                if (turnosInvalidos.Any())
                {
                    _logger?.LogWarning("ObtenerBaseComun: Se encontraron turnos inválidos", new
                    {
                        TotalTurnos = turnos.Count,
                        TurnosInvalidos = turnosInvalidos.Count
                    });
                }

                // Obtener primer turno válido
                var primerTurno = turnos.FirstOrDefault(t => t != null && t.Id > 0);
                if (primerTurno == null)
                {
                    _logger?.LogWarning("ObtenerBaseComun: No se pudo obtener un turno válido");
                    throw new Exception("No se pudo obtener un turno válido.");
                }

                _logger?.LogInformation("ObtenerBaseComun: Turnos obtenidos correctamente", new
                {
                    TotalTurnos = turnos.Count,
                    PrimerTurnoId = primerTurno.Id,
                    PrimerTurnoNombre = primerTurno.Nombre
                });

                // ===================== OBTENER MENÚ DEL DÍA =====================
                // Normalizar IDs a nullable para pasar al servicio de menú
                // Esto garantiza que ambos métodos (Totem y Web) usen los mismos filtros
                int? centroCostoId = usuario.CentroCostoId > 0 ? (int?)usuario.CentroCostoId : null;
                int? proyectoId = usuario.ProyectoId > 0 ? (int?)usuario.ProyectoId : null;
                int? jerarquiaId = usuario.JerarquiaId > 0 ? (int?)usuario.JerarquiaId : null;

                // Obtener menú del día para el primer turno usando los datos del usuario
                // Filtros aplicados: Planta, Turno, CentroCosto, Proyecto, Jerarquía, PlanNutricional
                var menuDia = _servicioMenuDia.ObtenerMenuPorTurno(
                    hoy,
                    usuario.PlantaId,
                    primerTurno.Id,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    usuario.Plannutricional_id,
                    soloConStock: true
                );

                // Validar que menuDia no sea null
                if (menuDia == null)
                {
                    _logger?.LogWarning("ObtenerBaseComun: El menú del día es null", new
                    {
                        Fecha = hoy,
                        PlantaId = usuario.PlantaId,
                        TurnoId = primerTurno.Id,
                        CentroCostoId = centroCostoId,
                        ProyectoId = proyectoId,
                        JerarquiaId = jerarquiaId,
                        PlanNutricionalId = usuario.Plannutricional_id
                    });
                    menuDia = new List<MenuddPorTurnoDto>(); // Inicializar lista vacía en lugar de null
                }

                _logger?.LogInformation("ObtenerBaseComun: Menú del día obtenido correctamente", new
                {
                    Fecha = hoy,
                    PlantaId = usuario.PlantaId,
                    TurnoId = primerTurno.Id,
                    TotalItems = menuDia.Count
                });

                // ===================== FILTRAR MENÚ SI YA CONSUMIÓ BONIFICACIÓN =====================
                // Validar en la tabla comanda si el usuario ya consumió una bonificación:
                // - usuario_id = usuario actual
                // - fecha = fecha del día actual
                // - estado = "R" (Recibido)
                // - bonificado = 1 (true) - ya aplicó bonificación
                // Si existe, NO mostrar los ítems del menú que tienen bonificación disponible
                /*if (menuDia != null && menuDia.Any())
                {
                    using (var ctx = new DataContext())
                    {
                        ctx.Configuration.LazyLoadingEnabled = false;

                        // Verificar si el usuario ya consumió una bonificación recibida en la fecha actual
                        var yaConsumioBonificacion = ctx.sl_comanda
                            .Any(c => c.usuario_id == usuario.Id
                                    && !c.deletemark
                                    && c.fecha == hoy
                                    && c.estado == "R"
                                    && c.bonificado == true);

                        /*if (yaConsumioBonificacion)
                        {
                            _logger?.LogInformation("ObtenerBaseComun: Usuario ya consumió bonificación, filtrando menú", new
                            {
                                UsuarioId = usuario.Id,
                                Fecha = hoy,
                                ItemsAntesFiltrado = menuDia.Count
                            });

                            // Obtener IDs de los menús del día que tienen bonificación disponible
                            // (basado en la jerarquía del menú que tenga bonificación > 0)
                            var menuIdsConBonificacion = ctx.sl_menudd
                                .Include("Jerarquia")
                                .Where(m => !m.deletemark
                                         && m.fecha == hoy
                                         && m.planta_id == usuario.PlantaId
                                         && m.turno_id == primerTurno.Id
                                         && m.Jerarquia != null
                                         && m.Jerarquia.bonificacion > 0)
                                .Select(m => m.id)
                                .ToList();

                            // Filtrar los ítems del menú que tienen bonificación disponible
                            if (menuIdsConBonificacion.Any())
                            {
                                var menuDiaFiltrado = menuDia
                                    .Where(m => m != null && !menuIdsConBonificacion.Contains(m.Id))
                                    .ToList();

                                _logger?.LogInformation("ObtenerBaseComun: Filtrado de menú por bonificación consumida", new
                                {
                                    UsuarioId = usuario.Id,
                                    Fecha = hoy,
                                    ItemsAntes = menuDia.Count,
                                    ItemsDespues = menuDiaFiltrado.Count,
                                    ItemsFiltrados = menuDia.Count - menuDiaFiltrado.Count,
                                    MenuIdsConBonificacion = menuIdsConBonificacion
                                });

                                menuDia = menuDiaFiltrado;
                            }
                            else
                            {
                                _logger?.LogInformation("ObtenerBaseComun: No se encontraron menús con bonificación para filtrar", new
                                {
                                    UsuarioId = usuario.Id,
                                    Fecha = hoy
                                });
                            }
                        }
                        else
                        {
                            _logger?.LogInformation("ObtenerBaseComun: Usuario no ha consumido bonificación, mostrando todos los ítems", new
                            {
                                UsuarioId = usuario.Id,
                                Fecha = hoy,
                                TotalItems = menuDia.Count
                            });
                        }*/
                    //}
                //}

                // ===================== CALCULAR BONIFICACIONES APLICADAS EN EL DÍA ACTUAL =====================
                // Siempre calculado desde sl_comanda: día, estado "R", bonificado = true
                int bonificacionesAplicadasHoy = ObtenerBonificacionesAplicadasHoy(usuario.Id, hoy);

                _logger?.LogInformation("ObtenerBaseComun: Bonificaciones aplicadas en el día actual", new
                {
                    UsuarioId = usuario.Id,
                    Fecha = hoy,
                    BonificacionesAplicadasHoy = bonificacionesAplicadasHoy
                });

                // Actualizar el campo Bonificaciones del usuario con las aplicadas hoy
                // Crear una copia del usuario para no modificar el original
                var usuarioConBonificaciones = new UsuarioBaseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Apellido = usuario.Apellido,
                    Legajo = usuario.Legajo,
                    Dni = usuario.Dni,
                    Plannutricional_id = usuario.Plannutricional_id,
                    PlanNutricionalNombre = usuario.PlanNutricionalNombre,
                    PlantaId = usuario.PlantaId,
                    PlantaNombre = usuario.PlantaNombre,
                    CentroCostoId = usuario.CentroCostoId,
                    CentroCostoNombre = usuario.CentroCostoNombre,
                    ProyectoId = usuario.ProyectoId,
                    ProyectoNombre = usuario.ProyectoNombre,
                    JerarquiaId = usuario.JerarquiaId,
                    JerarquiaNombre = usuario.JerarquiaNombre,
                    BonificacionesInvitado = usuario.BonificacionesInvitado,
                    Pedidos = usuario.Pedidos,
                    Bonificaciones = usuario.Bonificaciones,
                    BonificacionesAplicadas = bonificacionesAplicadasHoy, // Actualizar con las bonificaciones del día actual
                    Descuento = usuario.Descuento,
                    Activo = usuario.Activo
                };

                // ===================== VALIDAR RESULTADO FINAL =====================
                if (usuario == null)
                {
                    _logger?.LogError("ObtenerBaseComun: El usuario es null al construir resultado");
                    throw new Exception("Error: El usuario no puede ser nulo.");
                }

                if (turnos == null || !turnos.Any())
                {
                    _logger?.LogError("ObtenerBaseComun: Los turnos son null o vacíos al construir resultado");
                    throw new Exception("Error: No se pudieron obtener los turnos.");
                }

                // menuDia puede estar vacío pero no null (ya se inicializó arriba)
                if (menuDia == null)
                {
                    _logger?.LogWarning("ObtenerBaseComun: El menú del día es null, inicializando lista vacía");
                    menuDia = new List<MenuddPorTurnoDto>();
                }

                var resultado = new InicioBaseDto
                {
                    Usuario = usuarioConBonificaciones, // Usar el usuario con bonificaciones actualizadas
                    Turnos = turnos,
                    MenuDelDia = menuDia
                };

                // Validar que el resultado se construyó correctamente
                if (resultado.Usuario == null || resultado.Turnos == null || resultado.MenuDelDia == null)
                {
                    var errorMsg = $"Error al construir el resultado: UsuarioNull={resultado.Usuario == null}, TurnosNull={resultado.Turnos == null}, MenuDelDiaNull={resultado.MenuDelDia == null}";
                    _logger?.LogError("ObtenerBaseComun: El resultado tiene propiedades null", new Exception(errorMsg), new
                    {
                        UsuarioNull = resultado.Usuario == null,
                        TurnosNull = resultado.Turnos == null,
                        MenuDelDiaNull = resultado.MenuDelDia == null
                    });
                    throw new Exception("Error al construir el resultado: propiedades requeridas son null.");
                }

                _logger?.LogInformation("ObtenerBaseComun: Datos base obtenidos exitosamente", new
                {
                    UsuarioId = usuario.Id,
                    UsuarioNombre = $"{usuario.Nombre} {usuario.Apellido}",
                    TotalTurnos = turnos.Count,
                    TotalMenuItems = menuDia.Count,
                    TurnosIds = turnos.Select(t => t.Id).ToList(),
                    PrimerTurnoNombre = primerTurno.Nombre
                });

                return resultado;
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("No hay turnos") || ex.Message.Contains("no tiene planta") || ex.Message.Contains("inactivo") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerBaseComun: Error al obtener datos base", ex, new
                {
                    UsuarioId = usuario?.Id,
                    PlantaId = usuario?.PlantaId,
                    Fecha = hoy
                });
                throw;
            }
        }

        // Versión async del método anterior: usada por el polling de Inicio (cada 2s).
        private async Task<InicioBaseDto> ObtenerBaseComunAsync(UsuarioBaseDto usuario, DateTime? fecha = null)
        {
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            if (!usuario.Activo)
                throw new Exception("El usuario está inactivo.");

            if (usuario.PlantaId <= 0)
                throw new Exception("El usuario no tiene planta asignada.");

            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                var turnos = (await _servicioTurno.ObtenerHorarioComboAsync()).ToList();

                if (turnos == null || !turnos.Any())
                {
                    _logger?.LogWarning("ObtenerBaseComunAsync: No hay turnos disponibles");
                    throw new Exception("No hay turnos disponibles.");
                }

                var primerTurno = turnos.FirstOrDefault(t => t != null && t.Id > 0);
                if (primerTurno == null)
                    throw new Exception("No se pudo obtener un turno válido.");

                int? centroCostoId = usuario.CentroCostoId > 0 ? (int?)usuario.CentroCostoId : null;
                int? proyectoId = usuario.ProyectoId > 0 ? (int?)usuario.ProyectoId : null;
                int? jerarquiaId = usuario.JerarquiaId > 0 ? (int?)usuario.JerarquiaId : null;

                var menuDia = await _servicioMenuDia.ObtenerMenuPorTurnoAsync(
                    hoy,
                    usuario.PlantaId,
                    primerTurno.Id,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    usuario.Plannutricional_id,
                    soloConStock: true
                ) ?? new List<MenuddPorTurnoDto>();

                int bonificacionesAplicadasHoy = await ObtenerBonificacionesAplicadasHoyAsync(usuario.Id, hoy);

                var usuarioConBonificaciones = new UsuarioBaseDto
                {
                    Id = usuario.Id,
                    Nombre = usuario.Nombre,
                    Apellido = usuario.Apellido,
                    Legajo = usuario.Legajo,
                    Dni = usuario.Dni,
                    Plannutricional_id = usuario.Plannutricional_id,
                    PlanNutricionalNombre = usuario.PlanNutricionalNombre,
                    PlantaId = usuario.PlantaId,
                    PlantaNombre = usuario.PlantaNombre,
                    CentroCostoId = usuario.CentroCostoId,
                    CentroCostoNombre = usuario.CentroCostoNombre,
                    ProyectoId = usuario.ProyectoId,
                    ProyectoNombre = usuario.ProyectoNombre,
                    JerarquiaId = usuario.JerarquiaId,
                    JerarquiaNombre = usuario.JerarquiaNombre,
                    BonificacionesInvitado = usuario.BonificacionesInvitado,
                    Pedidos = usuario.Pedidos,
                    Bonificaciones = usuario.Bonificaciones,
                    BonificacionesAplicadas = bonificacionesAplicadasHoy,
                    Descuento = usuario.Descuento,
                    Activo = usuario.Activo
                };

                _logger?.LogInformation("ObtenerBaseComunAsync: Datos base obtenidos exitosamente", new
                {
                    UsuarioId = usuario.Id,
                    TotalTurnos = turnos.Count,
                    TotalMenuItems = menuDia.Count
                });

                return new InicioBaseDto
                {
                    Usuario = usuarioConBonificaciones,
                    Turnos = turnos,
                    MenuDelDia = menuDia
                };
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("No hay turnos") || ex.Message.Contains("no tiene planta") || ex.Message.Contains("inactivo") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerBaseComunAsync: Error al obtener datos base", ex, new
                {
                    UsuarioId = usuario?.Id,
                    PlantaId = usuario?.PlantaId,
                    Fecha = hoy
                });
                throw;
            }
        }

        public InicioWebDto ObtenerWeb(int usuarioId, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            if (fecha.HasValue)
            {
                var fechaValidada = fecha.Value.Date;
                var hoy = DateTime.Today;
                if (fechaValidada > hoy)
                    throw new Exception("No se puede consultar fechas futuras.");
                if ((hoy - fechaValidada).Days > 30)
                    throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");
            }

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerWeb: Iniciando obtención de datos para web", new
                {
                    UsuarioId = usuarioId,
                    Fecha = fecha
                });

                // Obtener y mapear usuario usando método común
                var usuarioBase = ObtenerUsuarioBasePorId(usuarioId);
                
                // Usar método común que acepta usuario completo
                var resultado = ObtenerWeb(usuarioBase, fecha);

                _logger?.LogInformation("ObtenerWeb: Datos obtenidos exitosamente", new
                {
                    UsuarioId = usuarioId,
                    Legajo = usuarioBase.Legajo,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0,
                    TotalComandas = resultado.PlatosPedidos?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerWeb: Error al obtener datos para web", ex, new
                {
                    UsuarioId = usuarioId,
                    Fecha = fecha
                });
                throw;
            }
        }

        // Método sobrecargado que acepta usuario completo (sin buscar en BD)
        public InicioWebDto ObtenerWeb(UsuarioBaseDto usuario, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerWeb: Iniciando obtención de datos para web (usuario completo)", new
                {
                    UsuarioId = usuario.Id,
                    Legajo = usuario.Legajo,
                    Fecha = hoy
                });

                // Obtener datos base usando el mismo método que Totem (garantiza consistencia total)
                var baseDto = ObtenerBaseComun(usuario, fecha);

                var primerTurno = baseDto.Turnos?.FirstOrDefault();
                if (primerTurno == null)
                {
                    _logger?.LogWarning("ObtenerWeb: No hay turnos disponibles");
                    throw new Exception("No hay turnos disponibles.");
                }

                // ÚNICA DIFERENCIA: Obtener comandas del día (solo para Web)
                var comanda = _servicioPedido.ObtenerXDia(
                    hoy, 
                    hoy, 
                    baseDto.Usuario.Id, 
                    primerTurno.Id, 
                    baseDto.Usuario.PlantaId, 
                    baseDto.Usuario.CentroCostoId, 
                    baseDto.Usuario.ProyectoId, 
                    baseDto.Usuario.JerarquiaId,
                    estado: null // Sin filtro de estado
                );

                // Validar que comanda no sea null
                if (comanda == null)
                {
                    _logger?.LogWarning("ObtenerWeb: Las comandas son null", new
                    {
                        UsuarioId = baseDto.Usuario.Id,
                        Fecha = hoy,
                        TurnoId = primerTurno.Id
                    });
                    comanda = new List<ComandaDetalleDto>(); // Inicializar lista vacía
                }

                // Retornar InicioWebDto que hereda de InicioBaseDto y agrega PlatosPedidos
                var resultado = new InicioWebDto
                {
                    Usuario = baseDto.Usuario,
                    Turnos = baseDto.Turnos,
                    MenuDelDia = baseDto.MenuDelDia,
                    PlatosPedidos = comanda
                };

                _logger?.LogInformation("ObtenerWeb: Datos obtenidos exitosamente (usuario completo)", new
                {
                    UsuarioId = usuario.Id,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0,
                    TotalComandas = resultado.PlatosPedidos?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("No hay turnos") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerWeb: Error al obtener datos para web (usuario completo)", ex, new
                {
                    UsuarioId = usuario?.Id,
                    Fecha = hoy
                });
                throw;
            }
        }

        // Versión async: usada por /api/inicio/web.
        public async Task<InicioWebDto> ObtenerWebAsync(int usuarioId, DateTime? fecha = null)
        {
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            var usuarioBase = await ObtenerUsuarioBasePorIdAsync(usuarioId);
            return await ObtenerWebAsync(usuarioBase, fecha);
        }

        // Versión async: sobrecarga que acepta usuario completo (sin buscar en BD).
        public async Task<InicioWebDto> ObtenerWebAsync(UsuarioBaseDto usuario, DateTime? fecha = null)
        {
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                var baseDto = await ObtenerBaseComunAsync(usuario, fecha);

                var primerTurno = baseDto.Turnos?.FirstOrDefault();
                if (primerTurno == null)
                    throw new Exception("No hay turnos disponibles.");

                var comanda = await _servicioPedido.ObtenerXDiaAsync(
                    hoy,
                    hoy,
                    baseDto.Usuario.Id,
                    primerTurno.Id,
                    baseDto.Usuario.PlantaId,
                    baseDto.Usuario.CentroCostoId,
                    baseDto.Usuario.ProyectoId,
                    baseDto.Usuario.JerarquiaId,
                    estado: null
                ) ?? new List<ComandaDetalleDto>();

                return new InicioWebDto
                {
                    Usuario = baseDto.Usuario,
                    Turnos = baseDto.Turnos,
                    MenuDelDia = baseDto.MenuDelDia,
                    PlatosPedidos = comanda
                };
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("No hay turnos") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerWebAsync: Error al obtener datos para web (usuario completo)", ex, new
                {
                    UsuarioId = usuario?.Id,
                    Fecha = hoy
                });
                throw;
            }
        }

        // Método ObtenerBase - implementación base común
        public InicioBaseDto ObtenerBase(int usuarioId)
        {
            // Reutiliza ObtenerTotem que ya tiene todas las validaciones y logging
            return ObtenerTotem(usuarioId);
        }

        /// <summary>
        /// Obtiene datos de inicio web actualizados para un turno específico
        /// Similar a ObtenerWeb pero permite especificar el turno seleccionado
        /// </summary>
        public InicioWebDto ObtenerWebActualizado(int usuarioId, int turnoId, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            // turnoId 0: no hacer búsqueda de turno; se devuelve OK con datos base y menú/pedidos vacíos (sin turno seleccionado)

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            if (fecha.HasValue)
            {
                var fechaValidada = fecha.Value.Date;
                var hoy = DateTime.Today;
                if (fechaValidada > hoy)
                    throw new Exception("No se puede consultar fechas futuras.");
                if ((hoy - fechaValidada).Days > 30)
                    throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");
            }

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerWebActualizado: Iniciando obtención de datos para web actualizado", new
                {
                    UsuarioId = usuarioId,
                    TurnoId = turnoId,
                    Fecha = fecha
                });

                // Obtener y mapear usuario usando método común
                var usuarioBase = ObtenerUsuarioBasePorId(usuarioId);
                
                // Usar método sobrecargado que acepta usuario completo
                var resultado = ObtenerWebActualizado(usuarioBase, turnoId, fecha);

                _logger?.LogInformation("ObtenerWebActualizado: Datos obtenidos exitosamente", new
                {
                    UsuarioId = usuarioId,
                    TurnoId = turnoId,
                    Legajo = usuarioBase.Legajo,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0,
                    TotalComandas = resultado.PlatosPedidos?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex)
            {
                _logger?.LogError("ObtenerWebActualizado: Error al obtener datos para web actualizado", ex, new
                {
                    UsuarioId = usuarioId,
                    TurnoId = turnoId,
                    Fecha = fecha
                });
                throw;
            }
        }

        /// <summary>
        /// Obtiene datos de inicio web actualizados para un turno específico
        /// Método sobrecargado que acepta usuario completo (sin buscar en BD)
        /// Refactorizado para reutilizar ObtenerBaseComun y solo cambiar el turno
        /// </summary>
        public InicioWebDto ObtenerWebActualizado(UsuarioBaseDto usuario, int turnoId, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            // turnoId 0: no hay turno seleccionado; devolver OK con datos base (usuario + turnos) y menú/pedidos vacíos
            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerWebActualizado: Iniciando obtención de datos para web actualizado (usuario completo)", new
                {
                    UsuarioId = usuario.Id,
                    Legajo = usuario.Legajo,
                    TurnoId = turnoId,
                    Fecha = hoy
                });

                // Obtener datos base usando el mismo método que Totem (garantiza consistencia total)
                var baseDto = ObtenerBaseComun(usuario, fecha);

                // Si no hay turno seleccionado (turnoId 0), devolver OK con usuario + turnos y menú/pedidos vacíos (no hay turnos para cargar)
                if (turnoId <= 0)
                {
                    _logger?.LogInformation("ObtenerWebActualizado: Sin turno seleccionado (turnoId=0), devolviendo datos base sin menú ni pedidos", new
                    {
                        UsuarioId = usuario.Id,
                        TotalTurnos = baseDto.Turnos?.Count ?? 0
                    });
                    return new InicioWebDto
                    {
                        Usuario = baseDto.Usuario, // Incluye BonificacionesAplicadas y Descuento
                        Turnos = baseDto.Turnos,
                        MenuDelDia = new List<MenuddPorTurnoDto>(),
                        PlatosPedidos = new List<ComandaDetalleDto>()
                    };
                }

                // Usar turno solicitado si está en la lista; si no, usar el primer turno disponible
                var turnoSeleccionado = baseDto.Turnos?.FirstOrDefault(t => t.Id == turnoId);
                if (turnoSeleccionado == null)
                {
                    turnoSeleccionado = baseDto.Turnos?.FirstOrDefault();
                    if (turnoSeleccionado == null)
                        throw new Exception("No hay turnos disponibles.");
                    _logger?.LogWarning("ObtenerWebActualizado: Turno solicitado no disponible o no enviado, usando primer turno", new
                    {
                        TurnoIdSolicitado = turnoId,
                        TurnoIdUsado = turnoSeleccionado.Id
                    });
                }

                // Normalizar IDs a nullable para pasar al servicio de menú
                int? centroCostoId = usuario.CentroCostoId > 0 ? (int?)usuario.CentroCostoId : null;
                int? proyectoId = usuario.ProyectoId > 0 ? (int?)usuario.ProyectoId : null;
                int? jerarquiaId = usuario.JerarquiaId > 0 ? (int?)usuario.JerarquiaId : null;

                // Obtener menú del día para el turno seleccionado (o primer turno)
                var menuDia = _servicioMenuDia.ObtenerMenuPorTurno(
                    hoy,
                    usuario.PlantaId,
                    turnoSeleccionado.Id,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    usuario.Plannutricional_id,
                    soloConStock: true
                );

                // Validar que menuDia no sea null
                if (menuDia == null)
                {
                    _logger?.LogWarning("ObtenerWebActualizado: El menú del día es null", new
                    {
                        Fecha = hoy,
                        PlantaId = usuario.PlantaId,
                        TurnoId = turnoSeleccionado.Id
                    });
                    menuDia = new List<MenuddPorTurnoDto>(); // Inicializar lista vacía
                }

                // Obtener comandas del día para el turno seleccionado
                var comanda = _servicioPedido.ObtenerXDia(
                    hoy,
                    hoy,
                    usuario.Id,
                    turnoSeleccionado.Id,
                    usuario.PlantaId,
                    usuario.CentroCostoId,
                    usuario.ProyectoId,
                    usuario.JerarquiaId,
                    estado: null // Sin filtro de estado
                );

                // Validar que comanda no sea null
                if (comanda == null)
                {
                    _logger?.LogWarning("ObtenerWebActualizado: Las comandas son null", new
                    {
                        UsuarioId = usuario.Id,
                        Fecha = hoy,
                        TurnoId = turnoSeleccionado.Id
                    });
                    comanda = new List<ComandaDetalleDto>(); // Inicializar lista vacía
                }

                // Retornar InicioWebDto con los datos del turno seleccionado
                var resultado = new InicioWebDto
                {
                    Usuario = baseDto.Usuario, // Incluye BonificacionesAplicadas y Descuento
                    Turnos = baseDto.Turnos, // Usar los turnos de baseDto (ya validados)
                    MenuDelDia = menuDia,
                    PlatosPedidos = comanda
                };

                _logger?.LogInformation("ObtenerWebActualizado: Datos obtenidos exitosamente (usuario completo)", new
                {
                    UsuarioId = usuario.Id,
                    TurnoId = turnoSeleccionado.Id,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0,
                    TotalComandas = resultado.PlatosPedidos?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("no está disponible") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerWebActualizado: Error al obtener datos para web actualizado (usuario completo)", ex, new
                {
                    UsuarioId = usuario?.Id,
                    TurnoId = turnoId,
                    Fecha = hoy
                });
                throw;
            }
        }

        // Versión async: usada por /api/inicio/web-actualizado, el polling que el front dispara cada 2s.
        public async Task<InicioWebDto> ObtenerWebActualizadoAsync(int usuarioId, int turnoId, DateTime? fecha = null)
        {
            if (usuarioId <= 0)
                throw new Exception("El ID del usuario debe ser mayor a 0.");

            var usuarioBase = await ObtenerUsuarioBasePorIdAsync(usuarioId);
            return await ObtenerWebActualizadoAsync(usuarioBase, turnoId, fecha);
        }

        // Versión async: sobrecarga que acepta usuario completo (sin buscar en BD).
        public async Task<InicioWebDto> ObtenerWebActualizadoAsync(UsuarioBaseDto usuario, int turnoId, DateTime? fecha = null)
        {
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                var baseDto = await ObtenerBaseComunAsync(usuario, fecha);

                // Sin turno seleccionado (turnoId 0): devolver datos base con menú/pedidos vacíos.
                if (turnoId <= 0)
                {
                    return new InicioWebDto
                    {
                        Usuario = baseDto.Usuario,
                        Turnos = baseDto.Turnos,
                        MenuDelDia = new List<MenuddPorTurnoDto>(),
                        PlatosPedidos = new List<ComandaDetalleDto>()
                    };
                }

                var turnoSeleccionado = baseDto.Turnos?.FirstOrDefault(t => t.Id == turnoId)
                    ?? baseDto.Turnos?.FirstOrDefault();
                if (turnoSeleccionado == null)
                    throw new Exception("No hay turnos disponibles.");

                int? centroCostoId = usuario.CentroCostoId > 0 ? (int?)usuario.CentroCostoId : null;
                int? proyectoId = usuario.ProyectoId > 0 ? (int?)usuario.ProyectoId : null;
                int? jerarquiaId = usuario.JerarquiaId > 0 ? (int?)usuario.JerarquiaId : null;

                var menuDia = await _servicioMenuDia.ObtenerMenuPorTurnoAsync(
                    hoy,
                    usuario.PlantaId,
                    turnoSeleccionado.Id,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    usuario.Plannutricional_id,
                    soloConStock: true
                ) ?? new List<MenuddPorTurnoDto>();

                var comanda = await _servicioPedido.ObtenerXDiaAsync(
                    hoy,
                    hoy,
                    usuario.Id,
                    turnoSeleccionado.Id,
                    usuario.PlantaId,
                    usuario.CentroCostoId,
                    usuario.ProyectoId,
                    usuario.JerarquiaId,
                    estado: null
                ) ?? new List<ComandaDetalleDto>();

                return new InicioWebDto
                {
                    Usuario = baseDto.Usuario,
                    Turnos = baseDto.Turnos,
                    MenuDelDia = menuDia,
                    PlatosPedidos = comanda
                };
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("no está disponible") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerWebActualizadoAsync: Error al obtener datos para web actualizado (usuario completo)", ex, new
                {
                    UsuarioId = usuario?.Id,
                    TurnoId = turnoId,
                    Fecha = hoy
                });
                throw;
            }
        }

        /// <summary>
        /// Obtiene datos de inicio para tótem actualizados para un turno específico
        /// Similar a ObtenerWebActualizado pero para tótem (sin token, solo legajo)
        /// </summary>
        public InicioWebDto ObtenerTotemActualizado(UsuarioBaseDto usuario, int turnoId, DateTime? fecha = null)
        {
            // ===================== VALIDACIÓN DE ENTRADA =====================
            if (usuario == null)
                throw new ArgumentNullException(nameof(usuario), "El usuario no puede ser nulo.");

            // Si turnoId es 0 o no está en la lista, se usará el primer turno disponible más abajo

            // Validar rango de fechas (máximo 30 días hacia atrás, no futuras)
            var hoy = fecha?.Date ?? DateTime.Today;
            var hoyActual = DateTime.Today;
            if (hoy > hoyActual)
                throw new Exception("No se puede consultar fechas futuras.");
            if ((hoyActual - hoy).Days > 30)
                throw new Exception("No se puede consultar fechas con más de 30 días de antigüedad.");

            try
            {
                // ===================== LOGGING: Inicio de operación =====================
                _logger?.LogInformation("ObtenerTotemActualizado: Iniciando obtención de datos para tótem actualizado", new
                {
                    UsuarioId = usuario.Id,
                    Legajo = usuario.Legajo,
                    TurnoId = turnoId,
                    Fecha = hoy
                });

                // Obtener datos base usando el mismo método que Totem (garantiza consistencia total)
                var baseDto = ObtenerBaseComun(usuario, fecha);

                // Usar turno solicitado si está en la lista; si no, usar el primer turno disponible
                var turnoSeleccionado = (turnoId > 0)
                    ? baseDto.Turnos?.FirstOrDefault(t => t.Id == turnoId)
                    : null;
                if (turnoSeleccionado == null)
                {
                    turnoSeleccionado = baseDto.Turnos?.FirstOrDefault();
                    if (turnoSeleccionado == null)
                        throw new Exception("No hay turnos disponibles.");
                    _logger?.LogWarning("ObtenerTotemActualizado: Turno solicitado no disponible o no enviado, usando primer turno", new
                    {
                        TurnoIdSolicitado = turnoId,
                        TurnoIdUsado = turnoSeleccionado.Id
                    });
                }

                // Normalizar IDs a nullable para pasar al servicio de menú
                int? centroCostoId = usuario.CentroCostoId > 0 ? (int?)usuario.CentroCostoId : null;
                int? proyectoId = usuario.ProyectoId > 0 ? (int?)usuario.ProyectoId : null;
                int? jerarquiaId = usuario.JerarquiaId > 0 ? (int?)usuario.JerarquiaId : null;

                // Obtener menú del día para el turno seleccionado (o primer turno)
                var menuDia = _servicioMenuDia.ObtenerMenuPorTurno(
                    hoy,
                    usuario.PlantaId,
                    turnoSeleccionado.Id,
                    centroCostoId,
                    proyectoId,
                    jerarquiaId,
                    usuario.Plannutricional_id,
                    soloConStock: true
                );

                // Validar que menuDia no sea null
                if (menuDia == null)
                {
                    _logger?.LogWarning("ObtenerTotemActualizado: El menú del día es null", new
                    {
                        Fecha = hoy,
                        PlantaId = usuario.PlantaId,
                        TurnoId = turnoSeleccionado.Id
                    });
                    menuDia = new List<MenuddPorTurnoDto>(); // Inicializar lista vacía
                }

                // Obtener comandas del día para el turno seleccionado
                var comanda = _servicioPedido.ObtenerXDia(
                    hoy,
                    hoy,
                    usuario.Id,
                    turnoSeleccionado.Id,
                    usuario.PlantaId,
                    usuario.CentroCostoId,
                    usuario.ProyectoId,
                    usuario.JerarquiaId,
                    estado: null // Sin filtro de estado
                );

                // Validar que comanda no sea null
                if (comanda == null)
                {
                    _logger?.LogWarning("ObtenerTotemActualizado: Las comandas son null", new
                    {
                        UsuarioId = usuario.Id,
                        Fecha = hoy,
                        TurnoId = turnoSeleccionado.Id
                    });
                    comanda = new List<ComandaDetalleDto>(); // Inicializar lista vacía
                }

                // Retornar InicioWebDto con los datos del turno seleccionado
                var resultado = new InicioWebDto
                {
                    Usuario = baseDto.Usuario, // Incluye BonificacionesAplicadas y Descuento
                    Turnos = baseDto.Turnos, // Usar los turnos de baseDto (ya validados)
                    MenuDelDia = menuDia,
                    PlatosPedidos = comanda
                };

                _logger?.LogInformation("ObtenerTotemActualizado: Datos obtenidos exitosamente", new
                {
                    UsuarioId = usuario.Id,
                    TurnoId = turnoSeleccionado.Id,
                    TotalTurnos = resultado.Turnos?.Count ?? 0,
                    TotalMenuItems = resultado.MenuDelDia?.Count ?? 0,
                    TotalComandas = resultado.PlatosPedidos?.Count ?? 0
                });

                return resultado;
            }
            catch (Exception ex) when (!(ex is Exception && (ex.Message.Contains("no está disponible") || ex.Message.Contains("fechas"))))
            {
                _logger?.LogError("ObtenerTotemActualizado: Error al obtener datos para tótem actualizado", ex, new
                {
                    UsuarioId = usuario?.Id,
                    TurnoId = turnoId,
                    Fecha = hoy
                });
                throw;
            }
        }

    }
}
