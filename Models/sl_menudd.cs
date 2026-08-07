using smartlunch_api.Models;
using System.ComponentModel.DataAnnotations.Schema;
using System;

[Table("sl_menudd")]
public class sl_menudd
{
    public int id { get; set; }

    public DateTime fecha { get; set; }

    public int turno_id { get; set; }
    public virtual sl_turno Turno { get; set; }

    public int plato_id { get; set; }
    public virtual sl_plato Plato { get; set; }

    public int cantidad { get; set; }
    public int comandas { get; set; }
    public int despachado { get; set; }

    public int planta_id { get; set; }
    public virtual sl_planta Planta { get; set; }

    public int centrodecosto_id { get; set; }
    public virtual sl_centrodecosto CentroDeCosto { get; set; }

    public int proyecto_id { get; set; }
    public virtual sl_proyecto Proyecto { get; set; }


    public int? jerarquia_id { get; set; }
    public virtual sl_jerarquia Jerarquia { get; set; }

    public bool deletemark { get; set; }
    public DateTime? createdate { get; set; }
    
}
