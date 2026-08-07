using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// DTO base que comparten WEB y TÓTEM:
    /// Usuario + Turnos + Menú del día.
    /// </summary>
    public class InicioBaseDto
    {
        public UsuarioBaseDto Usuario { get; set; }
        public List<TurnoUpdateDto> Turnos { get; set; }
        public List<MenuddPorTurnoDto> MenuDelDia { get; set; }
    }

    /// <summary>
    /// DTO específico para la WEB: agrega PlatosPedidos.
    /// </summary>
    public class InicioWebDto : InicioBaseDto
    {
        public List<ComandaDetalleDto> PlatosPedidos { get; set; }
    }
}