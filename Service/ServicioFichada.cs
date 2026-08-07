using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using smartlunch_api.Dtos;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    // ============================================
    // INTERFACE
    // ============================================
    public interface IServicioFichada
    {
        PagedResultDto<FichadaListadoDto> ObtenerLista(int page, int pageSize, DateTime? fechaDesde, DateTime? fechaHasta, int? usuarioId, bool soloActivos);
        FichadaDetalleDto ObtenerPorId(int id);
        FichadaDetalleDto CrearFichada(FichadaCreateDto dto);
        Task<FichadaDetalleDto> RegistrarDesdeBiostarAsync(BiostarEventRowDto evento, int planta, int centro_costo, int proyecto, int jerarquia, int bonificacion, int bonificacion_inv, int plan_nutricional);
    }

    // ============================================
    // IMPLEMENTACIÓN
    // ============================================
    public class ServicioFichada : IServicioFichada
    {
        private readonly ServicioClienteSmartTimeDatoLaboral _smartTime =
            new ServicioClienteSmartTimeDatoLaboral();

        // Flag: si es false, NUNCA se llama a SmartTime
        private readonly bool _usarSmartTime;
        private readonly ILoggerService _logger;

        public ServicioFichada(ILoggerService logger = null)
        {
            _logger = logger;
            var flag = ConfigurationManager.AppSettings["smarTime"];
            bool parsed;
            if (bool.TryParse(flag, out parsed))
                _usarSmartTime = parsed;
            else
                _usarSmartTime = true; // por defecto, true si no está bien seteado
        }

        // ============= LISTA PAGINADA + FILTROS =============
        public PagedResultDto<FichadaListadoDto> ObtenerLista(
            int page,
            int pageSize,
            DateTime? fechaDesde,
            DateTime? fechaHasta,
            int? usuarioId,
            bool soloActivos)
        {
            if (page < 1) page = 1;
            if (pageSize <= 0 || pageSize > 100) pageSize = 10;

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var query = ctx.sl_fichada.AsQueryable();

                if (fechaDesde.HasValue)
                    query = query.Where(f => f.fecha_fichada >= fechaDesde.Value);

                if (fechaHasta.HasValue)
                {
                    var hastaExclusivo = fechaHasta.Value.Date.AddDays(1);
                    query = query.Where(f => f.fecha_fichada < hastaExclusivo);
                }

                if (usuarioId.HasValue)
                    query = query.Where(f => f.identificador_usuario == usuarioId.Value);

                // sl_fichada no tiene deletemark, se consideran todos activos
                // if (soloActivos)
                //     query = query.Where(f => !f.deletemark);

                var totalItems = query.Count();
                var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);

                var items = query
                    .OrderByDescending(f => f.fecha_fichada)
                    .ThenByDescending(f => f.id)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(f => new FichadaListadoDto
                    {
                        Id = f.id,
                        IdentificadorUsuario = f.identificador_usuario,
                        TurnoId = f.turno_id,
                        FechaFichada = f.fecha_fichada,
                        IdDispositivo = f.id_dispositivo,
                        Createdate = f.createdate
                    })
                    .ToList();

                return new PagedResultDto<FichadaListadoDto>
                {
                    page = page,
                    pageSize = pageSize,
                    totalItems = totalItems,
                    totalPages = totalPages,
                    items = items
                };
            }
        }

        // ============= DETALLE POR ID =============
        public FichadaDetalleDto ObtenerPorId(int id)
        {
            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                var dto = ctx.sl_fichada
                    .Where(f => f.id == id)
                    .Select(f => new FichadaDetalleDto
                    {
                        Id = f.id,
                        IdentificadorUsuario = f.identificador_usuario,
                        TurnoId = f.turno_id,
                        FechaFichada = f.fecha_fichada,
                        IdDispositivo = f.id_dispositivo,
                        Createdate = f.createdate
                    })
                    .FirstOrDefault();

                return dto;
            }
        }

        // ============= CREAR FICHADA MANUAL =============
        public FichadaDetalleDto CrearFichada(FichadaCreateDto dto)
        {
            if (dto == null)
                throw new Exception("Datos inválidos.");

            if (dto.IdentificadorUsuario <= 0)
                throw new Exception("El identificador de usuario es obligatorio.");

            if (dto.IdDispositivo <= 0)
                throw new Exception("El dispositivo es obligatorio.");

            using (var ctx = new DataContext())
            {
                var existeUsuario = ctx.sl_usuario.Any(u =>
                    u.legajo == dto.IdentificadorUsuario &&
                    !u.deletemark);

                if (!existeUsuario)
                    throw new Exception("El usuario indicado no existe o está dado de baja.");

                if (dto.TurnoId.HasValue)
                {
                    var existeTurno = ctx.sl_turno.Any(t =>
                        t.id == dto.TurnoId.Value &&
                        !t.deletemark);

                    if (!existeTurno)
                        throw new Exception("El turno indicado no existe.");
                }

                var fecha = dto.FechaFichada ?? DateTime.Now;

                var entity = new sl_fichada
                {
                    identificador_usuario = dto.IdentificadorUsuario,
                    turno_id = dto.TurnoId,
                    fecha_fichada = fecha,
                    id_dispositivo = dto.IdDispositivo,
                    createdate = DateTime.Now
                };

                ctx.sl_fichada.Add(entity);
                ctx.SaveChanges();

                return ObtenerPorId(entity.id);
            }
        }

        // ============= NUEVO: REGISTRAR DESDE BIOSTAR =============
        /// <summary>
        /// Registra una fichada a partir de un evento de Biostar.
        /// - Si el legajo YA existe en sl_usuario: solo guarda la fichada.
        /// - Si NO existe:
        ///     * Si smarTime = true: consulta a SmartTime, crea usuario y luego fichada.
        ///     * Si smarTime = false: crea usuario solo con datos de Biostar y luego fichada.
        /// </summary>
        public async Task<FichadaDetalleDto> RegistrarDesdeBiostarAsync(BiostarEventRowDto evento, int planta, int centro_costo, int proyecto, int jerarquia, int bonificacion, int bonificacion_inv, int plan_nutricional)
        {
            _logger?.LogInformation("RegistrarDesdeBiostarAsync: Iniciando registro de fichada desde Biostar", new
            {
                EventoId = evento?.id,
                Legajo = evento?.user_id?.user_id,
                Nombre = evento?.user_id?.name,
                DeviceId = evento?.device_id?.id,
                DeviceName = evento?.device_id?.name,
                FechaEvento = evento?.datetime,
                Planta = planta,
                CentroCosto = centro_costo,
                Proyecto = proyecto,
                Jerarquia = jerarquia,
                Bonificacion = bonificacion,
                BonificacionInvitado = bonificacion_inv,
                PlanNutricional = plan_nutricional,
                PlanNutricionalValido = plan_nutricional > 0
            });

            if (plan_nutricional <= 0)
            {
                var error = $"Plan nutricional inválido: {plan_nutricional}. Debe ser un número mayor a 0.";
                _logger?.LogError(error, null, new { PlanNutricional = plan_nutricional });
                throw new Exception(error);
            }

            if (evento == null || evento.user_id == null || string.IsNullOrWhiteSpace(evento.user_id.user_id))
            {
                var error = "Evento de Biostar inválido: falta legajo.";
                _logger?.LogError(error, null, new { Evento = evento });
                throw new Exception(error);
            }

            if (!int.TryParse(evento.user_id.user_id, out var legajo))
            {
                var error = $"Legajo inválido en Biostar: '{evento.user_id.user_id}'.";
                _logger?.LogError(error, null, new { LegajoRaw = evento.user_id.user_id });
                throw new Exception(error);
            }

            _logger?.LogInformation("RegistrarDesdeBiostarAsync: Legajo parseado correctamente", new { Legajo = legajo });

            int? idDispositivo = null;
            if (evento.device_id != null && !string.IsNullOrWhiteSpace(evento.device_id.id))
            {
                if (int.TryParse(evento.device_id.id, out var parsedId))
                {
                    idDispositivo = parsedId;
                    _logger?.LogInformation("RegistrarDesdeBiostarAsync: Device ID parseado", new { IdDispositivo = idDispositivo });
                }
                else
                {
                    _logger?.LogWarning("RegistrarDesdeBiostarAsync: No se pudo parsear device_id", new { DeviceIdRaw = evento.device_id.id });
                }
            }
            else
            {
                _logger?.LogWarning("RegistrarDesdeBiostarAsync: device_id es null o vacío");
            }

            if (!idDispositivo.HasValue)
            {
                var error = "El id_dispositivo es obligatorio para crear la fichada.";
                _logger?.LogError(error, null, new { Legajo = legajo });
                throw new Exception(error);
            }

            using (var ctx = new DataContext())
            {
                ctx.Configuration.LazyLoadingEnabled = false;

                // Buscar usuario local por legajo
                _logger?.LogInformation("RegistrarDesdeBiostarAsync: Buscando usuario en BD", new { Legajo = legajo });
                var usuario = ctx.sl_usuario
                    .FirstOrDefault(u => u.legajo == legajo && !u.deletemark);

                if (usuario != null)
                {
                    _logger?.LogInformation("RegistrarDesdeBiostarAsync: Usuario encontrado en BD", new
                    {
                        UsuarioId = usuario.id,
                        Legajo = usuario.legajo,
                        Nombre = usuario.nombre,
                        Apellido = usuario.apellido
                    });
                }
                else
                {
                    _logger?.LogInformation("RegistrarDesdeBiostarAsync: Usuario NO encontrado, se creará nuevo usuario", new
                    {
                        Legajo = legajo,
                        UsarSmartTime = _usarSmartTime
                    });
                }

                // Si NO existe el usuario local, ver qué hacer según smarTime
                if (usuario == null)
                {
                    if (_usarSmartTime)
                    {
                        _logger?.LogInformation("RegistrarDesdeBiostarAsync: Consultando SmartTime para obtener datos del usuario", new { Legajo = legajo });
                        // ===== FLUJO CON SMARTTIME =====
                        var datoLaboral = await _smartTime.ObtenerDatoLaboralAsync(legajo);
                        var item = datoLaboral?.DatoLaboral?.FirstOrDefault();

                        if (item == null)
                        {
                            var error = $"No se encontraron datos en SmartTime para el legajo {legajo}.";
                            _logger?.LogError(error, null, new { Legajo = legajo, DatoLaboral = datoLaboral });
                            throw new Exception(error);
                        }

                        _logger?.LogInformation("RegistrarDesdeBiostarAsync: Datos obtenidos de SmartTime", new
                        {
                            Legajo = legajo,
                            ApellidoNombre = item.ApellidoNombre
                        });

                        var apellidoNombre = (item.ApellidoNombre ?? "").Trim();
                        string nombre = apellidoNombre;
                        string apellido = apellidoNombre;

                        var partes = apellidoNombre.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (partes.Length == 2)
                        {
                            apellido = partes[0];
                            nombre = partes[1];
                        }

                        int dni = 0;
                        string cuil = "";

                        usuario = new sl_usuario
                        {
                            nombre = nombre,
                            apellido = apellido,
                            legajo = legajo,
                            dni = dni,
                            cuil = cuil,
                            domicilio = "",
                            fechaingreso = null,
                            contrato = null,
                            plannutricional_id = plan_nutricional,
                            planta_id = planta,
                            centrodecosto_id = centro_costo,
                            proyecto_id = proyecto,
                            jerarquia_id = jerarquia,
                            bonificaciones_invitado = bonificacion_inv,
                            bonificaciones = bonificacion,
                            createdate = DateTime.Now,
                            createuser = "Sistema",
                            updatedate = null,
                            updateuser = null,
                            deletemark = false,
                            pedidos = 0,
                            email = null,
                            telefono = null,
                            llave_acceso = null,
                            origen_datos = "SmartTime",
                            fecha_ultima_sincronizacion = DateTime.Now
                        };

                        _logger?.LogInformation("RegistrarDesdeBiostarAsync: Usuario creado desde SmartTime", new
                        {
                            Legajo = usuario.legajo,
                            Nombre = usuario.nombre,
                            Apellido = usuario.apellido,
                            Planta = usuario.planta_id,
                            CentroCosto = usuario.centrodecosto_id,
                            Proyecto = usuario.proyecto_id,
                            Jerarquia = usuario.jerarquia_id,
                            PlanNutricional = usuario.plannutricional_id,
                            Bonificacion = usuario.bonificaciones,
                            BonificacionInvitado = usuario.bonificaciones_invitado
                        });
                    }
                    else
                    {
                        _logger?.LogInformation("RegistrarDesdeBiostarAsync: Creando usuario solo con datos de Biostar", new { Legajo = legajo });
                        // ===== FLUJO SIN SMARTTIME (solo Biostar) =====
                        var nombreCompletoBiostar = (evento.user_id.name ?? "").Trim();
                        string nombre = nombreCompletoBiostar;
                        string apellido = nombreCompletoBiostar;

                        var partes = nombreCompletoBiostar.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                        if (partes.Length == 2)
                        {
                            apellido = partes[0];
                            nombre = partes[1];
                        }

                        int dni = 0;
                        string cuil = "";

                        usuario = new sl_usuario
                        {
                            nombre = nombre,
                            apellido = apellido,
                            legajo = legajo,
                            dni = dni,
                            cuil = cuil,
                            domicilio = "",
                            fechaingreso = null,
                            contrato = null,
                            plannutricional_id = plan_nutricional,
                            planta_id = planta,
                            centrodecosto_id = centro_costo,
                            proyecto_id = proyecto,
                            jerarquia_id = jerarquia,
                            bonificaciones_invitado = bonificacion_inv,
                            bonificaciones = bonificacion,
                            createdate = DateTime.Now,
                            createuser = "Sistema",
                            updatedate = null,
                            updateuser = null,
                            deletemark = false,
                            pedidos = 0,
                            email = null,
                            telefono = null,
                            llave_acceso = null,
                            origen_datos = "Biostar",
                            fecha_ultima_sincronizacion = DateTime.Now
                        };

                        _logger?.LogInformation("RegistrarDesdeBiostarAsync: Usuario creado desde Biostar", new
                        {
                            Legajo = usuario.legajo,
                            Nombre = usuario.nombre,
                            Apellido = usuario.apellido,
                            Planta = usuario.planta_id,
                            CentroCosto = usuario.centrodecosto_id,
                            Proyecto = usuario.proyecto_id,
                            Jerarquia = usuario.jerarquia_id,
                            PlanNutricional = usuario.plannutricional_id,
                            Bonificacion = usuario.bonificaciones,
                            BonificacionInvitado = usuario.bonificaciones_invitado
                        });
                    }

                    _logger?.LogInformation("RegistrarDesdeBiostarAsync: Guardando usuario en BD", new
                    {
                        Legajo = usuario.legajo,
                        Nombre = usuario.nombre,
                        Apellido = usuario.apellido,
                        Planta = usuario.planta_id,
                        CentroCosto = usuario.centrodecosto_id,
                        Proyecto = usuario.proyecto_id,
                        Jerarquia = usuario.jerarquia_id,
                        PlanNutricional = usuario.plannutricional_id,
                        PlanNutricionalValido = usuario.plannutricional_id.HasValue && usuario.plannutricional_id.Value > 0,
                        Bonificacion = usuario.bonificaciones,
                        BonificacionInvitado = usuario.bonificaciones_invitado,
                        OrigenDatos = usuario.origen_datos
                    });
                    ctx.sl_usuario.Add(usuario);
                    ctx.SaveChanges();
                    _logger?.LogInformation("RegistrarDesdeBiostarAsync: Usuario guardado exitosamente", new
                    {
                        UsuarioId = usuario.id,
                        Legajo = usuario.legajo
                    });
                }

                // Si el usuario no tiene login, crearlo automáticamente (username = legajo, contraseña generada, debe_cambiar_clave si SmartTime)
                if (!ctx.sl_login.Any(l => l.usuario_id == usuario.id && !l.deletemark))
                {
                    var usernameLogin = usuario.legajo.ToString();
                    if (usernameLogin.Length > 50) usernameLogin = usernameLogin.Substring(0, 50);
                    var passwordGenerada = PasswordUtils.GenerarClaveAleatoria(12);
                    PasswordUtils.CreateHash(passwordGenerada, out var saltLogin, out var hashLogin);
                    var loginFichada = new sl_login
                    {
                        usuario_id = usuario.id,
                        username = usernameLogin,
                        password_salt = saltLogin,
                        password_hash = hashLogin,
                        password_iteraciones = PasswordUtils.IteracionesActuales,
                        activo = true,
                        deletemark = false,
                        debe_cambiar_clave = _usarSmartTime,
                        createdate = DateTime.Now,
                        createuser = "Sistema"
                    };
                    ctx.sl_login.Add(loginFichada);
                    ctx.SaveChanges();
                    _logger?.LogInformation("RegistrarDesdeBiostarAsync: Login creado automáticamente para usuario legajo {Legajo}, username={Username}, debe_cambiar_clave={DebeCambiar}", usuario.legajo, usernameLogin, _usarSmartTime);
                }

                // Crear fichada usando el usuario (nuevo o existente)
                _logger?.LogInformation("RegistrarDesdeBiostarAsync: Preparando creación de fichada", new
                {
                    Legajo = usuario.legajo,
                    IdDispositivo = idDispositivo,
                    FechaEvento = evento.datetime
                });

                // Parsear event_id desde el JSON de Biostar
                long eventId = 0;
                if (!string.IsNullOrWhiteSpace(evento.id))
                {
                    long.TryParse(evento.id, out eventId);
                }

                // Validar que no exista una fichada duplicada (mismo legajo + event_id)
                if (eventId > 0)
                {
                    var fichadaExistente = ctx.sl_fichada
                        .Any(f => f.identificador_usuario == usuario.legajo && f.event_id == eventId);

                    if (fichadaExistente)
                    {
                        var error = $"La fichada ya fue registrada anteriormente (Legajo: {usuario.legajo}, EventId: {eventId}).";
                        _logger?.LogWarning(error, new { Legajo = usuario.legajo, EventId = eventId });
                        throw new Exception(error);
                    }
                }

                // Parsear event_index
                long eventIndex = 0;
                if (!string.IsNullOrWhiteSpace(evento.index))
                {
                    long.TryParse(evento.index, out eventIndex);
                }

                // Parsear device_ext_id
                long deviceExtId = 0;
                if (evento.device_id != null && !string.IsNullOrWhiteSpace(evento.device_id.id))
                {
                    long.TryParse(evento.device_id.id, out deviceExtId);
                }

                // Obtener device_name
                string deviceName = evento.device_id?.name ?? "";

                // Parsear event_code
                int eventCode = 0;
                if (evento.event_type_id != null && !string.IsNullOrWhiteSpace(evento.event_type_id.code))
                {
                    int.TryParse(evento.event_type_id.code, out eventCode);
                }

                var fechaEventoUtc = evento.datetime;
                if (fechaEventoUtc.Kind == DateTimeKind.Unspecified)
                    fechaEventoUtc = DateTime.SpecifyKind(fechaEventoUtc, DateTimeKind.Utc);

                var fechaLocal = fechaEventoUtc.ToUniversalTime().AddHours(-3); // Argentina (-3)

                _logger?.LogInformation("RegistrarDesdeBiostarAsync: Fecha convertida", new
                {
                    FechaEventoUtc = fechaEventoUtc,
                    FechaLocal = fechaLocal
                });

                var entity = new sl_fichada
                {
                    identificador_usuario = usuario.legajo,
                    turno_id = null,
                    fecha_fichada = fechaLocal,
                    id_dispositivo = idDispositivo.Value,
                    createdate = DateTime.Now,
                    event_id = eventId,
                    event_index = eventIndex,
                    device_ext_id = deviceExtId,
                    device_name = deviceName,
                    event_code = eventCode
                };

                _logger?.LogInformation("RegistrarDesdeBiostarAsync: Guardando fichada en BD", new
                {
                    IdentificadorUsuario = entity.identificador_usuario,
                    IdDispositivo = entity.id_dispositivo,
                    FechaFichada = entity.fecha_fichada
                });

                ctx.sl_fichada.Add(entity);
                ctx.SaveChanges();

                _logger?.LogInformation("RegistrarDesdeBiostarAsync: Fichada guardada exitosamente", new
                {
                    FichadaId = entity.id,
                    Legajo = entity.identificador_usuario
                });

                var fichadaDetalle = ObtenerPorId(entity.id);
                _logger?.LogInformation("RegistrarDesdeBiostarAsync: Proceso completado exitosamente", new
                {
                    FichadaId = fichadaDetalle?.Id,
                    Legajo = fichadaDetalle?.IdentificadorUsuario
                });

                return fichadaDetalle;
            }
        }
    }
}
