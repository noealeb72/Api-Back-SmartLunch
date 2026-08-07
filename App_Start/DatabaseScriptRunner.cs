using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace smartlunch_api.App_Start
{
    public class ScriptInfo
    {
        public string NombreScript { get; set; }
        public string Descripcion { get; set; }
        public bool Ejecutado { get; set; }
        public DateTime? FechaEjecucion { get; set; }
        public string UltimoResultado { get; set; }
        public string UltimoMensaje { get; set; }
    }

    public class ScriptEjecucionResultado
    {
        public string NombreScript { get; set; }
        public bool Exito { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>
    /// Descubre y ejecuta los scripts incrementales que se van dejando en
    /// Scripts\nuevos_scripts\ (cualquier .sql ahí adentro es candidato), y lleva
    /// un registro de cuáles ya se corrieron en sl_script_ejecutado. Los scripts
    /// llegan a esa carpeta con el deploy normal del backend (no hay upload desde
    /// la web); ejecutarlos siempre es una acción manual desde el panel.
    /// Usado por Controllers/DbScriptsController.cs (panel de administración) y por
    /// el aviso informativo en Global.asax.cs al levantar la aplicación.
    /// </summary>
    public static class DatabaseScriptRunner
    {
        private const string PatronScripts = "*.sql";
        private const string CarpetaNuevosScripts = "nuevos_scripts";

        private static string ObtenerCarpetaScripts()
        {
            var baseDir = HostingEnvironment.ApplicationPhysicalPath ?? AppDomain.CurrentDomain.BaseDirectory;
            return Path.Combine(baseDir, "Scripts", CarpetaNuevosScripts);
        }

        private static string ObtenerConnectionString()
        {
            return ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
        }

        private static void AsegurarTablaSeguimiento(SqlConnection cn)
        {
            using (var cmd = cn.CreateCommand())
            {
                cmd.CommandText = @"
IF OBJECT_ID(N'dbo.sl_script_ejecutado', N'U') IS NULL
CREATE TABLE dbo.sl_script_ejecutado (
    id INT IDENTITY(1,1) NOT NULL,
    nombre_script NVARCHAR(255) NOT NULL,
    fecha_ejecucion DATETIME2 NOT NULL,
    resultado NVARCHAR(20) NOT NULL,
    mensaje NVARCHAR(MAX) NULL,
    CONSTRAINT PK_sl_script_ejecutado PRIMARY KEY (id),
    CONSTRAINT UQ_sl_script_ejecutado_nombre UNIQUE (nombre_script)
);";
                cmd.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Extrae la descripción de un script a partir de sus líneas de comentario
        /// iniciales (líneas en blanco o que empiezan con "--"), descartando los
        /// separadores de puro "====".
        /// </summary>
        private static string ParsearDescripcion(string filePath)
        {
            var lineas = File.ReadAllLines(filePath);
            var partes = new List<string>();
            foreach (var linea in lineas)
            {
                var t = linea.Trim();
                if (t.Length == 0) continue;
                if (!t.StartsWith("--")) break;

                var contenido = t.TrimStart('-').Trim();
                if (contenido.Length == 0) continue;
                if (contenido.All(c => c == '=')) continue; // separador tipo "===="

                partes.Add(contenido);
            }
            return partes.Count > 0 ? string.Join(" ", partes) : "(sin descripción)";
        }

        /// <summary>
        /// Devuelve todos los .sql encontrados en Scripts/nuevos_scripts/, con su
        /// descripción y si ya fueron ejecutados (según sl_script_ejecutado).
        /// </summary>
        public static List<ScriptInfo> ListarScripts()
        {
            var carpeta = ObtenerCarpetaScripts();
            // Se ordena por fecha de última modificación del archivo (no alfabético): con
            // scripts incrementales, el orden en que fueron escritos suele coincidir con el
            // orden en que deben ejecutarse (uno puede depender de una columna/tabla que
            // creó el anterior).
            var archivos = Directory.Exists(carpeta)
                ? Directory.GetFiles(carpeta, PatronScripts).OrderBy(f => File.GetLastWriteTimeUtc(f)).ToList()
                : new List<string>();

            var ejecutados = new Dictionary<string, (DateTime fecha, string resultado, string mensaje)>(StringComparer.OrdinalIgnoreCase);

            var connectionString = ObtenerConnectionString();
            if (!string.IsNullOrEmpty(connectionString))
            {
                using (var cn = new SqlConnection(connectionString))
                {
                    cn.Open();
                    AsegurarTablaSeguimiento(cn);

                    using (var cmd = cn.CreateCommand())
                    {
                        cmd.CommandText = "SELECT nombre_script, fecha_ejecucion, resultado, mensaje FROM dbo.sl_script_ejecutado";
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var nombre = reader.GetString(0);
                                var fecha = reader.GetDateTime(1);
                                var resultado = reader.GetString(2);
                                var mensaje = reader.IsDBNull(3) ? null : reader.GetString(3);
                                ejecutados[nombre] = (fecha, resultado, mensaje);
                            }
                        }
                    }
                }
            }

            var lista = new List<ScriptInfo>();
            foreach (var archivo in archivos)
            {
                var nombre = Path.GetFileName(archivo);
                var info = new ScriptInfo
                {
                    NombreScript = nombre,
                    Descripcion = ParsearDescripcion(archivo)
                };
                if (ejecutados.TryGetValue(nombre, out var e))
                {
                    info.Ejecutado = e.resultado == "OK";
                    info.FechaEjecucion = e.fecha;
                    info.UltimoResultado = e.resultado;
                    info.UltimoMensaje = e.mensaje;
                }
                lista.Add(info);
            }
            return lista;
        }

        private static void RegistrarResultado(string connectionString, string nombreScript, bool exito, string mensaje)
        {
            using (var cn = new SqlConnection(connectionString))
            {
                cn.Open();
                AsegurarTablaSeguimiento(cn);
                using (var cmd = cn.CreateCommand())
                {
                    cmd.CommandText = @"
IF EXISTS (SELECT 1 FROM dbo.sl_script_ejecutado WHERE nombre_script = @nombre)
    UPDATE dbo.sl_script_ejecutado
    SET fecha_ejecucion = @fecha, resultado = @resultado, mensaje = @mensaje
    WHERE nombre_script = @nombre
ELSE
    INSERT INTO dbo.sl_script_ejecutado (nombre_script, fecha_ejecucion, resultado, mensaje)
    VALUES (@nombre, @fecha, @resultado, @mensaje)";
                    cmd.Parameters.AddWithValue("@nombre", nombreScript);
                    cmd.Parameters.AddWithValue("@fecha", DateTime.Now);
                    cmd.Parameters.AddWithValue("@resultado", exito ? "OK" : "ERROR");
                    cmd.Parameters.AddWithValue("@mensaje", (object)mensaje ?? DBNull.Value);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Ejecuta los scripts seleccionados (por nombre de archivo) contra la base
        /// de la aplicación. Cada nombre se valida contra los archivos realmente
        /// presentes en Scripts/ (nunca se acepta una ruta del cliente). Un script
        /// que falla no interrumpe a los siguientes.
        /// </summary>
        public static List<ScriptEjecucionResultado> EjecutarScripts(IEnumerable<string> nombresSeleccionados)
        {
            var connectionString = ObtenerConnectionString();
            if (string.IsNullOrEmpty(connectionString))
                throw new Exception("No hay cadena de conexión configurada (DataContext).");

            var carpeta = ObtenerCarpetaScripts();
            var archivosValidos = Directory.Exists(carpeta)
                ? Directory.GetFiles(carpeta, PatronScripts).ToDictionary(Path.GetFileName, f => f, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var resultados = new List<ScriptEjecucionResultado>();

            foreach (var nombreSolicitado in nombresSeleccionados ?? Enumerable.Empty<string>())
            {
                var nombre = Path.GetFileName((nombreSolicitado ?? "").Trim());
                if (string.IsNullOrEmpty(nombre) || !archivosValidos.TryGetValue(nombre, out var rutaCompleta))
                {
                    resultados.Add(new ScriptEjecucionResultado
                    {
                        NombreScript = nombreSolicitado,
                        Exito = false,
                        Mensaje = "El script indicado no existe en Scripts/."
                    });
                    continue;
                }

                try
                {
                    var contenido = File.ReadAllText(rutaCompleta);
                    var lotes = Regex.Split(contenido, @"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .ToList();

                    using (var cn = new SqlConnection(connectionString))
                    {
                        cn.Open();
                        using (var tx = cn.BeginTransaction())
                        {
                            try
                            {
                                foreach (var lote in lotes)
                                {
                                    using (var cmd = cn.CreateCommand())
                                    {
                                        cmd.Transaction = tx;
                                        cmd.CommandText = lote;
                                        cmd.CommandTimeout = 120;
                                        cmd.ExecuteNonQuery();
                                    }
                                }
                                tx.Commit();
                            }
                            catch
                            {
                                tx.Rollback();
                                throw;
                            }
                        }
                    }

                    RegistrarResultado(connectionString, nombre, true, null);
                    resultados.Add(new ScriptEjecucionResultado { NombreScript = nombre, Exito = true, Mensaje = "Ejecutado correctamente." });
                }
                catch (Exception ex)
                {
                    RegistrarResultado(connectionString, nombre, false, ex.Message);
                    resultados.Add(new ScriptEjecucionResultado { NombreScript = nombre, Exito = false, Mensaje = ex.Message });
                }
            }

            return resultados;
        }
    }
}
