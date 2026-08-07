using smartlunch_api.Models;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using System.Web.Http.Cors;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Threading.Tasks;
using System.Text;
using System.Diagnostics;
using static System.Net.WebRequestMethods;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;

namespace smartlunch_api.Controllers
{
    ////[EnableCors(origins: "*", headers: "*", methods: "*")]
    public class ApiExternaController : ApiController
    {
        /*private readonly HttpClient _httpClient;
        string traza = ConfigurationManager.AppSettings["traza"];
        string aplicacionOrigen = ConfigurationManager.AppSettings["aplicacionOrigen"];
        string empresa = ConfigurationManager.AppSettings["empresa"];
        string getUrl = ConfigurationManager.AppSettings["url"];
        string codigoVisualizacion = ConfigurationManager.AppSettings["codigoVisualizacion"];
        public ApiExternaController()
        {
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(ConfigurationManager.AppSettings["url"]);
        }

        public string GetToken()
        {
            try
            {
                using (DataContext ctx = new DataContext())
                {
                    sl_tokenFichada fichada = ctx.sl_tokenFichada.FirstOrDefault();
                    if (fichada != null && fichada.token != null)
                    {
                        DateTime now = DateTime.Now;
                        DateTime fechaBD = (DateTime)fichada.fecha;
                        // Calcular la diferencia entre las dos fechas
                        TimeSpan difference = now - fechaBD;
                        if (difference.TotalHours > 3)
                        {
                            // Si el token ha expirado o no es válido, eliminarlo y obtener uno nuevo
                            DeleteToken();
                            string newToken = ConsultaTokenApi();
                            // Guardar el nuevo token y la fecha exacta en la base de datos
                            fichada = new sl_tokenFichada();
                            fichada.token = newToken;
                            fichada.fecha = DateTime.Now; // Fecha actual en día, mes, año, hora, minuto y segundo
                            ctx.sl_tokenFichada.Add(fichada);
                            ctx.SaveChangesAsync();
                            return newToken;
                        }
                        else
                        {
                            // El token existente es válido, retornarlo
                            return fichada.token;
                        }
                    }
                    else
                    {
                        // No hay un token existente, obtener uno nuevo y guardarlo en la base de datos
                        string newToken = ConsultaTokenApi();
                        ctx.SaveChangesAsync();
                        return newToken;
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que ocurra durante la llamada a la API o el proceso de obtención del token
                return "Error al obtener o validar el token: " + ex.Message;
            }
        }


        public async Task DeleteToken()
        {
            try
            {
                using (DataContext ctx = new DataContext())
                {
                    sl_tokenFichada fichada = ctx.sl_tokenFichada.FirstOrDefault();
                    if (fichada != null && fichada.token != null)
                    {
                        ctx.sl_tokenFichada.Remove(fichada);
                        await ctx.SaveChangesAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que ocurra durante el proceso
                throw new Exception("Error al eliminar el token: " + ex.Message);
            }
        }

        public async Task<bool> ValidateToken(string token)
        {
            try
            {

                // Hacer una solicitud GET a una ruta protegida (puedes cambiar la URL según tu API)
                var requestBody = new
                {
                    login = new
                    {
                        nombreAplicacion = ConfigurationManager.AppSettings["nombreAplicacion"],
                        key = ConfigurationManager.AppSettings["key"]
                    }
                };
                // string existingToken = ConfigurationManager.AppSettings["tokenLogin"];

                // Convertir el objeto a JSON
                var jsonRequestBody = JsonConvert.SerializeObject(requestBody);

                // Configurar el tipo de contenido de la solicitud
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Agregar el token al encabezado de la solicitud
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                // Realizar la solicitud HTTP POST a LoginAplicacion
                var response = await _httpClient.PostAsync("api/Seguridad/LoginAplicacion", new StringContent(jsonRequestBody, Encoding.UTF8, "application/json"));

                //HttpResponseMessage response = await httpClient.GetAsync("ruta/a/la/api/protegida");

                // Verificar si la solicitud fue exitosa
                if (response.IsSuccessStatusCode)
                {
                    // El token es válido
                    return true;
                }
                else
                {
                    // El token no es válido
                    return false;
                }
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que ocurra durante el proceso
                throw new Exception("Error al validar el token: " + ex.Message);
            }
        }

        public string ConsultaTokenApi()
        {
            try
            {
                string url = $"api/Seguridad/LoginAplicacion";

                
                // Crear el cuerpo de la solicitud para LoginAplicacion
                var requestBody = new
                {
                    login = new
                    {
                        nombreAplicacion = ConfigurationManager.AppSettings["nombreAplicacion"],
                        key = ConfigurationManager.AppSettings["key"]
                    }
                };
                string existingToken = ConfigurationManager.AppSettings["tokenLogin"];

                // Convertir el objeto a JSON
                var jsonRequestBody = JsonConvert.SerializeObject(requestBody);

                // Configurar el tipo de contenido de la solicitud
                _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                // Agregar el token al encabezado de la solicitud
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", existingToken);

                // Realizar la solicitud HTTP POST a LoginAplicacion
                var response = _httpClient.PostAsync(url, new StringContent(jsonRequestBody, Encoding.UTF8, "application/json")).Result;
                
                // Verificar si la solicitud fue exitosa
                if (response.IsSuccessStatusCode)
                {
                    // Leer el contenido de la respuesta
                    var responseBody = response.Content.ReadAsStringAsync().Result;

                    // Deserializar la respuesta JSON para extraer el nuevo token
                    dynamic responseObject = JsonConvert.DeserializeObject(responseBody);
                    string newToken = responseObject.token;

                    // Guardar el nuevo token y la fecha en la base de datos
                    using (DataContext ctx = new DataContext())
                    {
                        var fichada = ctx.sl_tokenFichada.FirstOrDefault();
                        if (fichada == null)
                        {
                            fichada = new sl_tokenFichada();
                            ctx.sl_tokenFichada.Add(fichada);
                        }

                        // Establecer el token y la fecha con hora, minuto y segundo
                        fichada.token = newToken;
                        fichada.fecha = DateTime.UtcNow; // Guarda la fecha y hora actual en UTC

                        ctx.SaveChanges();
                    }

                    return newToken;
                }
                else
                {
                    var errorResponse = response.Content.ReadAsStringAsync().Result;
                    //return $"Error: No se pudieron obtener correctamente los datos laborales. Respuesta del servidor: {errorResponse}";
                    return "Error al llamar a la API para obtener el token:   { errorResponse}";
                }
            }
            catch (Exception ex)
            {
                return "Error al llamar a la API LoginAplicacion: " + ex.Message;
            }
        }


     

        public string DatoLaboralgetByLegajo(string legajo, string token)
        {
            try
            {
                string url = $"api/DatoLaboral?DatoLaboral.Legajos={legajo}&ZonaHoraria=Argentina%20Standard%20Time&DatoLaboral.Activo=true";
                // Configurar el tipo de contenido de la solicitud
                if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
                // Agregar el token al encabezado de la solicitud
                if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Verificar y agregar las variables al encabezado
                if (!_httpClient.DefaultRequestHeaders.Contains("traza") && !string.IsNullOrEmpty(traza))
                {
                    _httpClient.DefaultRequestHeaders.Add("traza", traza);
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("aplicacionorigen") && !string.IsNullOrEmpty(aplicacionOrigen))
                {
                    _httpClient.DefaultRequestHeaders.Add("aplicacionorigen", aplicacionOrigen);
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("empresa") && !string.IsNullOrEmpty(empresa))
                {
                    _httpClient.DefaultRequestHeaders.Add("empresa", empresa);
                }


                // Realizar la solicitud HTTP GET a la API
                var response = _httpClient.GetAsync(url).Result;

                // Verificar si la solicitud fue exitosa
                if (response.IsSuccessStatusCode)
                {
                    // Leer el contenido de la respuesta
                    var responseBody = response.Content.ReadAsStringAsync().Result;
                    // Deserializar la respuesta JSON para extraer el ID
                    dynamic responseObject = JsonConvert.DeserializeObject(responseBody);
                    if (responseObject.datosLaborales != null && responseObject.datosLaborales.Count > 0)
                    {
                        string id = responseObject.datosLaborales[0].id;
                        // Guardar en la base de datos
                        int valLegajo = Convert.ToInt32(legajo);
                        GuardarLegajoYIdFichada(valLegajo, id);
                        return id;
                    }
                    else
                    {
                        return "Error: No se pudieron recuperar los datos laborales asociados al legajo.";
                    }
                }
                else
                {
                    var errorResponse = response.Content.ReadAsStringAsync().Result;
                    return $"Error: No se pudieron obtener correctamente los datos laborales. Respuesta del servidor: {errorResponse}";
                }
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }


        public dynamic DatoLaboralgetByLegajoBD(string legajo)
        {
            try
            {
                using (DataContext ctx = new DataContext())
                {
                    // Convertir el legajo a entero antes de la consulta LINQ
                    int legajoInt = int.Parse(legajo);

                    // Verificar si el legajo ya existe en la base de datos
                    var fichada = ctx.sl_datosfichada.FirstOrDefault(df => df.Legajo == legajoInt);

                    if (fichada != null)
                    {                       
                        // Si existe, devolver los valores existentes
                        return new
                        {
                            IdLegajoFichada = fichada.IdLegajoFichada,
                            LlaveAcceso = fichada.LlaveAcceso,
                            CantidadFichada = fichada.CantidadFichada,
                            UltimaFichada = fichada.UltimaFichada
                        };
                    }
                    else
                    {
                        // Si no hay valores, retornar null
                        return null;
                    }
                }
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que ocurra durante el proceso
                return null;
            }
        }

        public bool GuardarLegajoYIdFichada(int legajo, string idLegajoFichada)
        {
            try
            {
                using (DataContext ctx = new DataContext())
                {
                    // Crear una nueva instancia del modelo
                    sl_datosfichada nuevaFichada = new sl_datosfichada
                    {
                        Legajo = legajo,
                        IdLegajoFichada = idLegajoFichada
                    };

                    // Agregar el nuevo objeto al contexto
                    ctx.sl_datosfichada.Add(nuevaFichada);

                    // Guardar los cambios en la base de datos
                    ctx.SaveChanges();

                    return true; // Indica que el registro fue exitoso
                }
            }
            catch (Exception ex)
            {
                // Manejar excepciones (puedes registrar el error en un log)
                Console.WriteLine($"Error al guardar en la base de datos: {ex.Message}");
                return false; // Indica que ocurrió un error
            }
        }

        public string DatoLaboralgetByLlaveDeAccesoVigente(string id_empleado, string token)
        {
            try
            {
                string url = $"api/DatoLaboral/{id_empleado}/LlaveDeAccesoVigente";
                // Configurar el tipo de contenido de la solicitud
                if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }
                // Agregar el token al encabezado de la solicitud
                if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Verificar y agregar las variables al encabezado
                if (!_httpClient.DefaultRequestHeaders.Contains("traza") && !string.IsNullOrEmpty(traza))
                {
                    _httpClient.DefaultRequestHeaders.Add("traza", traza);
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("aplicacionorigen") && !string.IsNullOrEmpty(aplicacionOrigen))
                {
                    _httpClient.DefaultRequestHeaders.Add("aplicacionorigen", aplicacionOrigen);
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("empresa") && !string.IsNullOrEmpty(empresa))
                {
                    _httpClient.DefaultRequestHeaders.Add("empresa", empresa);
                }

                var response = _httpClient.GetAsync(url).Result;
                string respuestaLlave = string.Empty;

                if (response.IsSuccessStatusCode)
                {
                    var responseBody = response.Content.ReadAsStringAsync().Result;
                    // Deserializar la respuesta JSON
                    dynamic responseObject = JsonConvert.DeserializeObject(responseBody);

                    // Buscar las llaves de acceso del tipo "Tarjeta Magnética"
                    var tarjetasMagneticas = new List<dynamic>();

                    foreach (var llave in responseObject.datoLaboralLlaveDeAcceso)
                    {
                        if (llave.llaveDeAccesoTipo == "Tarjeta Magnetica")
                        {
                            tarjetasMagneticas.Add(llave);
                        }
                    }

                    // Si hay llaves de acceso del tipo "Tarjeta Magnética", devolver el número de la primera
                    if (tarjetasMagneticas.Any())
                    {
                        respuestaLlave = tarjetasMagneticas.First().numeroDeLlaveDeAcceso;
                        return respuestaLlave;
                    }

                    if (tarjetasMagneticas.Count > 0)
                    {
                        respuestaLlave = responseObject.datoLaboralLlaveDeAcceso.FirstOrDefault()?.numeroDeLlaveDeAcceso;
                        return respuestaLlave;
                    }
                    else
                    {
                        return "Error en llave de acceso, el usuario no posee ninguna llave registrada";
                    }
                }
                else
                {
                    string errorContent = response.Content.ReadAsStringAsync().Result;
                    return $"Error al obtener los datos de la fichada: {response.ReasonPhrase}, Detalles: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                // Manejar cualquier excepción que ocurra durante la llamada a la API
                return "Error en llave de acceso: " + ex.Message;
            }
        }


        public async Task GuardarLlaveDeAccesoPorEmpleado(string id_empleado, string llaveDeAcceso, DateTime? ultimaFichada, int cantidad)
        {
            using (DataContext ctx = new DataContext())
            {
                // Buscar el registro en la tabla sl_datosfichada por id_empleado
                var registro = await ctx.sl_datosfichada.FirstOrDefaultAsync(f => f.IdLegajoFichada == id_empleado);

                if (registro != null)
                {
                    // Actualizar las columnas necesarias
                    registro.LlaveAcceso = llaveDeAcceso;
                    registro.UltimaFichada = ultimaFichada;
                    registro.CantidadFichada = cantidad;
                    /* if (ultimaFichada != null && cantidad != 0)
                     {
                         registro.UltimaFichada = ultimaFichada;
                         registro.CantidadFichada = cantidad;
                     }*/

                    // Guardar los cambios en la base de datos
                   /* await ctx.SaveChangesAsync();
                }
            }
        }

  
        public string FichadasGetById(string llaveDeAcceso, string token)
        {
            try
            {
                var fechaDesde = new DateTime(2024, 11, 13, 0, 0, 0);
                var fechaHasta = new DateTime(2024, 11, 13, 23, 59, 59);

                // Construir la URL
                string url = $"api/Fichada?Fichada.NumeroDeTarjeta={llaveDeAcceso}" +
                             $"&Fichada.FechaFichadaDesde={fechaDesde:yyyy-MM-ddTHH:mm:ss}" +
                             $"&Fichada.FechaFichadaHasta={fechaHasta:yyyy-MM-ddTHH:mm:ss}" +
                             $"&Fichada.ZonaHoraria=Argentina Standard Time";

                // Configurar el tipo de contenido de la solicitud
                if (!_httpClient.DefaultRequestHeaders.Accept.Contains(new MediaTypeWithQualityHeaderValue("application/json")))
                {
                    _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                }

                // Agregar el token al encabezado de la solicitud
                if (!_httpClient.DefaultRequestHeaders.Contains("Authorization"))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }

                // Verificar y agregar las variables al encabezado
                if (!_httpClient.DefaultRequestHeaders.Contains("traza") && !string.IsNullOrEmpty(traza))
                {
                    _httpClient.DefaultRequestHeaders.Add("traza", traza);
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("aplicacionorigen") && !string.IsNullOrEmpty(aplicacionOrigen))
                {
                    _httpClient.DefaultRequestHeaders.Add("aplicacionorigen", aplicacionOrigen);
                }
                if (!_httpClient.DefaultRequestHeaders.Contains("empresa") && !string.IsNullOrEmpty(empresa))
                {
                    _httpClient.DefaultRequestHeaders.Add("empresa", empresa);
                }

                // Realizar la solicitud
                var response = _httpClient.GetAsync(url).Result;

                if (response.IsSuccessStatusCode)
                {
                    return response.Content.ReadAsStringAsync().Result;
                }
                else
                {
                    string errorContent = response.Content.ReadAsStringAsync().Result;
                    return $"Error al obtener los datos de la fichada: {response.ReasonPhrase}, Detalles: {errorContent}";
                }
            }
            catch (Exception ex)
            {
                return $"Error al obtener los datos de la fichada: {ex.Message}";
            }
        }*/


       


    }
}
