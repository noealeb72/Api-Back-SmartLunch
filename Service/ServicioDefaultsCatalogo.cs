using System;
using System.Configuration;
using System.Linq;
using smartlunch_api.Models;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Devuelve los IDs por defecto de los catálogos (planta, plan nutricional, centro de costo, proyecto, jerarquía)
    /// desde la base de datos (registro con is_default = true y activo). Si no hay ninguno marcado como default,
    /// usa el valor del Web.config para no depender de un ID que pueda estar dado de baja.
    /// </summary>
    public class ServicioDefaultsCatalogo
    {
        /// <summary>
        /// Obtiene los IDs por defecto: primero busca en BD (is_default = true y !deletemark); si no hay, usa Web.config.
        /// </summary>
        public static DefaultsCatalogoDto Obtener()
        {
            var dto = new DefaultsCatalogoDto();
            try
            {
                using (var ctx = new DataContext())
                {
                    var planta = ctx.sl_planta.FirstOrDefault(p => p.is_default && !p.deletemark);
                    dto.PlantaId = planta != null ? planta.id : LeerIntConfig("Planta", 1);

                    var plan = ctx.sl_plannutricional.FirstOrDefault(p => p.is_default && !p.deletemark);
                    dto.PlanNutricionalId = plan != null ? plan.id : LeerIntConfig("Plan_nutricional", 2);

                    var centro = ctx.sl_centrodecosto.FirstOrDefault(c => c.is_default && !c.deletemark);
                    dto.CentroCostoId = centro != null ? centro.id : LeerIntConfig("Centro_costo", 5);

                    var proyecto = ctx.sl_proyecto.FirstOrDefault(p => p.is_default && !p.deletemark);
                    dto.ProyectoId = proyecto != null ? proyecto.id : LeerIntConfig("Proyecto", 1);

                    var jerarquia = ctx.sl_jerarquia.FirstOrDefault(j => j.is_default && !j.deletemark);
                    dto.JerarquiaId = jerarquia != null ? jerarquia.id : LeerIntConfig("Jerarquia", 3);
                }
            }
            catch
            {
                dto.PlantaId = LeerIntConfig("Planta", 1);
                dto.PlanNutricionalId = LeerIntConfig("Plan_nutricional", 2);
                dto.CentroCostoId = LeerIntConfig("Centro_costo", 5);
                dto.ProyectoId = LeerIntConfig("Proyecto", 1);
                dto.JerarquiaId = LeerIntConfig("Jerarquia", 3);
            }

            return dto;
        }

        private static int LeerIntConfig(string key, int valorPorDefecto)
        {
            var s = ConfigurationManager.AppSettings[key];
            return int.TryParse(s, out var n) && n > 0 ? n : valorPorDefecto;
        }
    }

    /// <summary>
    /// IDs por defecto de los catálogos (desde BD o config).
    /// </summary>
    public class DefaultsCatalogoDto
    {
        public int PlantaId { get; set; }
        public int PlanNutricionalId { get; set; }
        public int CentroCostoId { get; set; }
        public int ProyectoId { get; set; }
        public int JerarquiaId { get; set; }
    }
}
