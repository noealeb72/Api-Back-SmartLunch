using System;

namespace smartlunch_api.Services
{
    /// <summary>
    /// Mapeo de jerarquía (sl_jerarquia.id) a nombre de rol para el JWT.
    /// Coincide con la tabla: 1=Admin, 2=Cocina, 3=Comensal, 4=Gerencia.
    /// </summary>
    public static class JerarquiaRolHelper
    {
        /// <summary>
        /// Devuelve el nombre del rol para [Authorize(Roles = "...")] según el id de jerarquía.
        /// </summary>
        public static string RolDesdeJerarquia(int? jerarquia_id)
        {
            if (!jerarquia_id.HasValue) return "User";
            switch (jerarquia_id.Value)
            {
                case 1: return "Admin";
                case 2: return "Cocina";
                case 3: return "Comensal";
                case 4: return "Gerencia";
                default: return "User";
            }
        }
    }
}
