using System.Collections.Generic;
using System;
using smartlunch_api.Dtos;

namespace smartlunch_api.Models.DTOs
{
    /// <summary>
    /// DTO para representar un turno en el dashboard
    /// </summary>
    public class TurnoDto
    {
        public int id { get; set; }
        public string nombre { get; set; }
        public TimeSpan? hora_desde { get; set; }
        public TimeSpan? hora_hasta { get; set; }
    }

    public class PedidoDto
    {
        public int id { get; set; }
        public DateTime createdate { get; set; }
        public string estado { get; set; }
        public string cod_plato { get; set; }
        public string plato_descripcion { get; set; }
        public decimal? monto { get; set; }
        public bool? bonificado { get; set; }
        public bool? invitado { get; set; }
    }

    public class MenuDiaItemDto
    {
        public int menudd_id { get; set; }
        public DateTime fecha { get; set; }
        public int turno_id { get; set; }
        public string turno_nombre { get; set; }
        public int plato_id { get; set; }
        public string plato_codigo { get; set; }
        public string plato_descripcion { get; set; }
        public decimal? plato_costo { get; set; }
    }

    public class DashboardInicioDto
    {
        public IList<TurnoDto> Turnos { get; set; }
        public IList<PedidoDto> PedidosHoy { get; set; }
    }

    public class DashboardInicioTotemDto
    {
        public IList<TurnoDto> Turnos { get; set; }
        public IList<MenuDiaItemDto> MenuDia { get; set; }
    }
}
