using Newtonsoft.Json;
using smartlunch_api.Dtos;
using smartlunch_api.Services;
using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.Cors;

namespace smartlunch_api.Controllers
{
    [RoutePrefix("api/biostar")]
    ////[EnableCors(origins: "*", headers: "*", methods: "*")]
    public class BiostarController : ApiController
    {
        private string _baseUrl;
        private string _loginPath;
        private string _eventsPath;
        private string _user;
        private string _password;

        private int _defaultMinutesBack;
        private string _biostarEventCodeFromConfig;
        private string _biostarDeviceIdFromConfig;
        private BiostarSessionManager _sessionManager;
        private BiostarClient _biostarClient;
        private readonly IServicioFichada _servicioFichada;
        private readonly ILoggerService _logger;

        public BiostarController(IServicioFichada servicioFichada, ILoggerService logger)
        {
            _servicioFichada = servicioFichada ?? throw new ArgumentNullException(nameof(servicioFichada));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Initialize();
        }

        public BiostarController() : this(new ServicioFichada(new SerilogLoggerService(Serilog.Log.Logger)), new SerilogLoggerService(Serilog.Log.Logger))
        {
        }

        private void Initialize()
        {
            // ===== Configuración Biostar desde Web.config =====
            _baseUrl = (ConfigurationManager.AppSettings["BiostarBaseUrl"] ?? "").TrimEnd('/');
            _loginPath = ConfigurationManager.AppSettings["BiostarLoginPath"] ?? "/api/login";
            _eventsPath = ConfigurationManager.AppSettings["BiostarEventsPath"] ?? "/api/events/search";
            _user = ConfigurationManager.AppSettings["BiostarUser"];
            _password = ConfigurationManager.AppSettings["BiostarPassword"];


            if (string.IsNullOrWhiteSpace(_baseUrl) ||
                string.IsNullOrWhiteSpace(_user) ||
                string.IsNullOrWhiteSpace(_password))
            {
                throw new Exception("Faltan claves de configuración de Biostar en Web.config (BiostarBaseUrl/BiostarUser/BiostarPassword).");
            }

            // Minutos hacia atrás por defecto
            var minutesBackStr = ConfigurationManager.AppSettings["BiostarDefaultMinutesBack"] ?? "5";
            if (!int.TryParse(minutesBackStr, out _defaultMinutesBack))
                _defaultMinutesBack = 5;

            // Código de evento desde Web.config (ej: 4865)
            _biostarEventCodeFromConfig = ConfigurationManager.AppSettings["BiostarDefaultEventId"];
            if (string.IsNullOrWhiteSpace(_biostarEventCodeFromConfig))
            {
                throw new Exception("Falta BiostarDefaultEventId en Web.config (se usa como event_type_id.code).");
            }

            // DeviceId desde Web.config (ej: 865718937)
            _biostarDeviceIdFromConfig = ConfigurationManager.AppSettings["BiostarDefaultDeviceId"];
            // Si querés que sea obligatorio:
            if (string.IsNullOrWhiteSpace(_biostarDeviceIdFromConfig))
            {
                throw new Exception("Falta BiostarDefaultDeviceId en Web.config (se usa como device_id.id).");
            }

            // ===== Servicios Biostar (login + events/search) =====
            _sessionManager = new BiostarSessionManager(
                baseUrl: _baseUrl,
                loginPath: _loginPath,
                user: _user,
                password: _password
            );

            _biostarClient = new BiostarClient(
                sessionManager: _sessionManager,
                baseUrl: _baseUrl,
                eventsPath: _eventsPath
            );
        }

        //[HttpGet]
        //[Route("events/hoy")]
        //public async Task<IHttpActionResult> GetEventsHoy()
        //{
        //    // Redirigir al método POST existente
        //    return await GetEvents();
        //}

        [HttpPost]
        [Route("events")]
        public async Task<IHttpActionResult> GetEvents()
        {
            try
            {
                // ===== 1) Rango de fechas (UTC) =====
                var desde = DateTime.UtcNow;                    
                var hasta = desde.AddDays(1);
                //desde = DateTime.UtcNow.Date.AddDays(-1);
                var limit = 1;

                // Las tres condiciones SIEMPRE: datetime + event_type_id.code + device_id.id
                var conditions = new object[]
                {
                    new
                    {
                        column = "datetime",
                        @operator = 3,
                        values = new[]
                        {
                            desde.ToString("yyyy-MM-ddTHH:mm:ss.000Z"),
                            hasta.ToString("yyyy-MM-ddTHH:mm:ss.000Z")
                        }
                    },
                    new
                    {
                        column = "event_type_id.code",
                        @operator = 0,
                        values = new[]
                        {
                            _biostarEventCodeFromConfig
                        }
                    },
                    new
                    {
                        column = "device_id.id",
                        @operator = 0,
                        values = new[]
                        {
                            _biostarDeviceIdFromConfig
                        }
                    }
                };

                var queryBody = new
                {
                    Query = new
                    {
                        limit = limit,
                        conditions = conditions,
                        orders = new object[]
                        {
                    new
                    {
                        column = "datetime",
                        descending = true
                    }
                        }
                    }
                };

                _logger.LogInformation("GetEvents: Preparando consulta a Biostar", new
                {
                    FromUtc = desde,
                    ToUtc = hasta,
                    MinutesBack = _defaultMinutesBack,
                    Limit = limit,
                    EventCode = _biostarEventCodeFromConfig,
                    DeviceId = _biostarDeviceIdFromConfig,
                    QueryBody = JsonConvert.SerializeObject(queryBody)
                });

                // ===== 2) Llamar a /api/events/search =====
                _logger.LogInformation("GetEvents: Llamando a Biostar /api/events/search");
                var jsonResponse = await _biostarClient.SearchEventsAsync(queryBody);

                _logger.LogInformation("GetEvents: Respuesta JSON recibida de Biostar", new
                {
                    ResponseLength = jsonResponse?.Length ?? 0,
                    ResponsePreview = jsonResponse?.Length > 500
                        ? jsonResponse.Substring(0, 500) + "..."
                        : jsonResponse,
                    FullResponse = jsonResponse
                });

                var biostarData = JsonConvert.DeserializeObject<BiostarEventsResponseDto>(jsonResponse);

                _logger.LogInformation("GetEvents: Respuesta de Biostar deserializada", new
                {
                    ResponseCode = biostarData?.Response?.code,
                    ResponseMessage = biostarData?.Response?.message,
                    TotalRows = biostarData?.EventCollection?.rows?.Count ?? 0,
                    HasEventCollection = biostarData?.EventCollection != null
                });

                if (biostarData?.Response == null || biostarData.Response.code != "0")
                {
                    _logger.LogWarning("GetEvents: Biostar devolvió un error en la respuesta", new
                    {
                        ResponseCode = biostarData?.Response?.code,
                        ResponseMessage = biostarData?.Response?.message,
                        FullResponse = jsonResponse
                    });

                    return Content(HttpStatusCode.BadRequest, new
                    {
                        message = "Biostar devolvió un error",
                        code = biostarData?.Response?.code,
                        detail = biostarData?.Response?.message
                    });
                }

                var rows = biostarData.EventCollection?.rows
                           ?? new System.Collections.Generic.List<BiostarEventRowDto>();

                _logger.LogInformation("GetEvents: Eventos obtenidos de Biostar", new
                {
                    TotalEventos = rows.Count,
                    Eventos = rows.Select(r => new
                    {
                        Id = r.id,//id
                        Index = r.index,
                        Legajo = r.user_id?.user_id,
                        Nombre = r.user_id?.name,
                        Fecha = r.datetime,
                        DeviceId = r.device_id?.id,
                        DeviceName = r.device_id?.name,
                        EventCode = r.event_type_id?.code
                    }).ToList()
                });

                // ===== 3) Si no hay eventos =====
                if (!rows.Any())
                {
                    _logger.LogInformation("GetEvents: No se encontraron eventos en Biostar para el rango configurado", new
                    {
                        FromUtc = desde,
                        ToUtc = hasta,
                        MinutesBack = _defaultMinutesBack
                    });

                    return Ok(new
                    {
                        tieneDatos = false,
                        mensaje = "Biostar no devolvió eventos en el rango configurado."
                    });
                }

                // ===== 4) Tomar SOLO el último evento por fecha =====
                var ultimoRow = rows
                    .OrderByDescending(r => r.datetime)
                    .First();

                _logger.LogInformation("GetEvents: Último evento seleccionado", new
                {
                    EventoId = ultimoRow.id,//id
                    Index = ultimoRow.index,//index
                    Legajo = ultimoRow.user_id?.user_id,
                    Nombre = ultimoRow.user_id?.name,
                    Fecha = ultimoRow.datetime,
                    DeviceId = ultimoRow.device_id?.id,//device_ext_id
                    DeviceName = ultimoRow.device_id?.name,//device_name
                    EventCode = ultimoRow.event_type_id?.code//event_code
                });

                // ===== 5) Registrar fichada en nuestra BD =====
                _logger.LogInformation("GetEvents: Iniciando registro de fichada", new
                {
                    Legajo = ultimoRow?.user_id?.user_id,
                    Nombre = ultimoRow?.user_id?.name,
                    EventoId = ultimoRow?.id,
                    FechaEvento = ultimoRow?.datetime
                });

                FichadaDetalleDto fichadaCreada = null;
                // Defaults desde la BD (is_default = true y activo); si no hay, desde Web.config (evita usar un ID dado de baja).
                var defaults = ServicioDefaultsCatalogo.Obtener();
                var _planta = defaults.PlantaId;
                var _centro_costo = defaults.CentroCostoId;
                var _proyecto = defaults.ProyectoId;
                var _jerarquia = defaults.JerarquiaId;
                var _plan_nutricional = defaults.PlanNutricionalId;
                var _bonificacion = int.TryParse(ConfigurationManager.AppSettings["Bonificaciones"], out var b) ? b : 0;
                var _bonificacion_invitado = int.TryParse(ConfigurationManager.AppSettings["Bonificaciones_invitado"], out var i) ? i : 0;
                var planNutricionalConfigRaw = ConfigurationManager.AppSettings["Plan_nutricional"];

                _logger.LogInformation("GetEvents: Parámetros de configuración (defaults desde BD o config)", new
                {
                    Planta = _planta,
                    CentroCosto = _centro_costo,
                    Proyecto = _proyecto,
                    Jerarquia = _jerarquia,
                    Bonificacion = _bonificacion,
                    BonificacionInvitado = _bonificacion_invitado,
                    PlanNutricional = _plan_nutricional,
                    PlanNutricionalConfigRaw = planNutricionalConfigRaw,
                    PlanNutricionalValido = _plan_nutricional > 0
                });

                if (_plan_nutricional <= 0)
                {
                    var error = $"Plan_nutricional inválido o no configurado. Valor en config: '{planNutricionalConfigRaw}', Valor parseado: {_plan_nutricional}. Debe ser un número mayor a 0.";
                    _logger.LogError(error, null, new
                    {
                        PlanNutricionalConfigRaw = planNutricionalConfigRaw,
                        PlanNutricionalParseado = _plan_nutricional
                    });
                    return Content(HttpStatusCode.InternalServerError, new
                    {
                        message = "Error de configuración",
                        detail = error
                    });
                }

                try
                {
                    _logger.LogInformation("GetEvents: Llamando a RegistrarDesdeBiostarAsync", new
                    {
                        Legajo = ultimoRow?.user_id?.user_id
                    });

                    fichadaCreada = await _servicioFichada.RegistrarDesdeBiostarAsync(
                        ultimoRow,
                        _planta,
                        _centro_costo,
                        _proyecto,
                        _jerarquia,
                        _bonificacion,
                        _bonificacion_invitado,
                        _plan_nutricional);

                    _logger.LogInformation("GetEvents: Fichada registrada exitosamente", new
                    {
                        FichadaId = fichadaCreada?.Id,
                        Legajo = fichadaCreada?.IdentificadorUsuario
                    });
                }
                catch (Exception exReg)
                {
                    _logger.LogError(exReg, "GetEvents: Error al registrar fichada desde Biostar", new
                    {
                        Legajo = ultimoRow?.user_id?.user_id,
                        Nombre = ultimoRow?.user_id?.name,
                        EventoId = ultimoRow?.id,
                        FechaEvento = ultimoRow?.datetime,
                        ErrorMessage = exReg.Message,
                        StackTrace = exReg.StackTrace,
                        InnerException = exReg.InnerException?.Message
                    });

                    // Si la fichada ya existe, devolver respuesta indicando duplicado
                    if (exReg.Message.Contains("ya fue registrada anteriormente"))
                    {
                        return Ok(new
                        {
                            tieneDatos = true,
                            fichadaDuplicada = true,
                            mensaje = exReg.Message,
                            evento = new
                            {
                                legajo = ultimoRow.user_id?.user_id,
                                nombre = ultimoRow.user_id?.name,
                                rawId = ultimoRow.id,
                                fechaHoraUtc = ultimoRow.datetime
                            }
                        });
                    }

                    throw; // Re-lanzar otros errores
                }

                // ===== 6) Respuesta al front =====
                var evento = new
                {
                    legajo = ultimoRow.user_id?.user_id,
                    nombre = ultimoRow.user_id?.name,
                    planta = _planta,
                    centro_costo = _centro_costo,
                    proyecto = _proyecto,
                    jerarquia = _jerarquia,
                    bonificacion = _bonificacion,
                    bonificacion_invitado = _bonificacion_invitado,
                    fechaHoraUtc = ultimoRow.datetime,
                    deviceBiostarId = ultimoRow.device_id?.id,
                    deviceBiostarName = ultimoRow.device_id?.name,
                    eventCode = ultimoRow.event_type_id?.code,
                    rawId = ultimoRow.id,
                    rawIndex = ultimoRow.index
                };

                var respuesta = new
                {
                    tieneDatos = true,
                    evento,
                    fichadaId = fichadaCreada?.Id
                };

                _logger.LogInformation("GetEvents: Respuesta exitosa preparada", new
                {
                    TieneDatos = respuesta.tieneDatos,
                    FichadaId = respuesta.fichadaId,
                    Evento = respuesta.evento,
                    RespuestaCompleta = JsonConvert.SerializeObject(respuesta)
                });

                return Ok(respuesta);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException || ex.CancellationToken.IsCancellationRequested == false)
            {
                return Content(HttpStatusCode.RequestTimeout, new
                {
                    message = "Error consultando Biostar",
                    detail = "La petición a Biostar excedió el tiempo de espera. Verifique la conectividad y el tiempo de respuesta del servidor Biostar.",
                    timeout = true
                });
            }
            catch (Exception ex)
            {
                return Content(HttpStatusCode.InternalServerError, new
                {
                    message = "Error consultando Biostar",
                    detail = ex.Message
                });
            }
        }
    }
}
