using System.Linq;
using System.Web.Http;
using smartlunch_api.App_Start;

namespace smartlunch_api
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            GlobalConfiguration.Configure(WebApiConfig.Register);

            // Al arrancar: solo validar si la BD existe. Si existe, cargar datos iniciales si las tablas están vacías.
            // La BD no se crea aquí: se crea desde la página de configuración (Default.html) cuando el usuario indica las claves de admin y smarTime.
            try
            {
                var connStr = System.Configuration.ConfigurationManager.ConnectionStrings["DataContext"]?.ConnectionString;
                var databaseName = string.IsNullOrEmpty(connStr) ? "smartlunch" : new System.Data.SqlClient.SqlConnectionStringBuilder(connStr).InitialCatalog;
                if (string.IsNullOrWhiteSpace(databaseName)) databaseName = "smartlunch";
                if (DatabaseInitializer.DatabaseExists(databaseName.Trim()))
                {
                    DatabaseSeeder.SeedIfEmpty();

                    // Aviso informativo: no ejecuta nada, solo indica si hay scripts
                    // pendientes en Scripts/nuevos_scripts/ para revisar en /api/DbScripts.
                    try
                    {
                        var pendientes = DatabaseScriptRunner.ListarScripts().Count(s => !s.Ejecutado);
                        if (pendientes > 0)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[Application_Start] Hay {pendientes} script(s) SQL pendientes de ejecutar. Revisar en /api/DbScripts.");
                        }
                    }
                    catch (System.Exception exScripts)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Application_Start] No se pudo verificar scripts pendientes: {exScripts.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Application_Start] Base de datos / seed: {ex.Message}");
            }
        }
    }
}
