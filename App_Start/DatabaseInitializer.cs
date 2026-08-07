using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.Text.RegularExpressions;
using System.Web.Hosting;

namespace smartlunch_api.App_Start
{
    /// <summary>
    /// Al levantar el proyecto, crea la base de datos (si no existe) con el nombre de Web.config -> DatabaseName
    /// y ejecuta el script de tablas y datos. Así solo hace falta poner el valor en DatabaseName y en la
    /// connection string (Database=) para que la BD se cree y quede lista.
    /// </summary>
    public static class DatabaseInitializer
    {
        /// <summary>
        /// Indica si la base de datos con el nombre indicado existe en el servidor.
        /// </summary>
        public static bool DatabaseExists(string databaseName)
        {
            if (string.IsNullOrWhiteSpace(databaseName)) return false;
            try
            {
                var connectionString = ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
                if (string.IsNullOrEmpty(connectionString)) return false;
                var builder = new SqlConnectionStringBuilder(connectionString);
                builder.InitialCatalog = "master";
                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = "SELECT 1 FROM sys.databases WHERE name = @name";
                        cmd.Parameters.AddWithValue("@name", databaseName.Trim());
                        var exists = cmd.ExecuteScalar() != null;
                        return exists;
                    }
                }
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Ejecuta una sola vez al iniciar la aplicación: crea la BD si no existe y ejecuta el script de tablas y datos.
        /// </summary>
        public static void EnsureDatabaseAndSchema()
        {
            var cs = ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
            var databaseName = string.IsNullOrEmpty(cs) ? "" : new SqlConnectionStringBuilder(cs).InitialCatalog.Trim();
            EnsureDatabaseAndSchema(databaseName);
        }

        /// <summary>
        /// Crea la BD con el nombre indicado (si no existe) y ejecuta el script de tablas y datos.
        /// Usado por la página de setup inicial.
        /// </summary>
        /// <param name="databaseName">Nombre de la base de datos. Si null o vacío, no hace nada.</param>
        /// <returns>Mensaje de error o null si todo bien.</returns>
        public static string EnsureDatabaseAndSchema(string databaseName)
        {
            try
            {
                databaseName = (databaseName ?? "").Trim();
                if (string.IsNullOrEmpty(databaseName))
                    return "El nombre de la base de datos es obligatorio.";

                var connectionString = ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
                if (string.IsNullOrEmpty(connectionString))
                    return "No hay cadena de conexión configurada (DataContext).";

                // Conexión a master para crear la BD y ejecutar el script (el script hace CREATE DATABASE y luego USE [db] + tablas)
                var builder = new SqlConnectionStringBuilder(connectionString);
                builder.InitialCatalog = "master";

                var scriptPath = ConfigurationManager.AppSettings["DatabaseSchemaScriptPath"] ?? "Scripts\\CrearBaseDatosYTablas.sql";
                var fullPath = HostingEnvironment.ApplicationPhysicalPath != null
                    ? Path.Combine(HostingEnvironment.ApplicationPhysicalPath, scriptPath)
                    : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, scriptPath);

                if (!File.Exists(fullPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] No se encontró el script: {fullPath}");
                    return "No se encontró el script de creación de tablas.";
                }

                var scriptContent = File.ReadAllText(fullPath);

                // Reemplazar el nombre de la base en el script (DECLARE @DatabaseName ... = N'...')
                scriptContent = Regex.Replace(
                    scriptContent,
                    @"(DECLARE\s+@DatabaseName\s+NVARCHAR\s*\(\s*128\s*\)\s*=\s*N')([^']*)(')",
                    "$1" + databaseName.Replace("'", "''") + "$3",
                    RegexOptions.IgnoreCase);

                using (var connection = new SqlConnection(builder.ConnectionString))
                {
                    connection.Open();

                    // Ejecutar el script completo (crea BD si no existe, luego tablas y datos)
                    using (var cmd = connection.CreateCommand())
                    {
                        cmd.CommandText = scriptContent;
                        cmd.CommandTimeout = 120; // 2 minutos por si la BD es grande
                        cmd.ExecuteNonQuery();
                    }
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DatabaseInitializer] Error: {ex.Message}");
                return ex.Message;
            }
        }
    }
}
