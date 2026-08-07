using smartlunch_api.Models;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System;

[Table("sl_planta")]
public class sl_planta
{
    [Key]
    public int id { get; set; }

    [Required]
    [StringLength(150)]
    [Index("IX_sl_planta_nombre", IsUnique = true)]
    public string nombre { get; set; }

    [StringLength(300)]
    public string descripcion { get; set; }

    public DateTime? createdate { get; set; }

    [StringLength(50)]
    public string createuser { get; set; }

    public DateTime? updatedate { get; set; }

    [StringLength(50)]
    public string updateuser { get; set; }

    [Required]
    public bool deletemark { get; set; }

    /// <summary>
    /// Si es true, este registro se usa como valor por defecto cuando no se especifica uno (evita depender de IDs fijos en config si el default est� dado de baja).
    /// </summary>
    public bool is_default { get; set; }

    // --- Navegaci�n (1:N con usuarios) ---
    public virtual ICollection<sl_usuario> usuarios { get; set; }
    public virtual ICollection<sl_centrodecosto> centrosdecosto { get; set; }
    public virtual ICollection<sl_menudd> menuesDelDia { get; set; }
    public sl_planta()
    {
        usuarios = new HashSet<sl_usuario>();
        centrosdecosto = new HashSet<sl_centrodecosto>();
        menuesDelDia = new HashSet<sl_menudd>();
        deletemark = false;
    }
}
