// Dtos/PreviewBonificacionDto.cs
namespace smartlunch_api.Dtos
{
    /// <summary>
    /// Precio que le correspondería a un ítem del menú del día si el usuario lo pidiera
    /// ahora mismo, según las reglas de bonificación activas. Se usa para mostrar el precio
    /// (tachado/final) en el menú antes de confirmar el pedido, sin crear ninguna comanda.
    /// </summary>
    public class PreviewBonificacionDto
    {
        public int MenuddId { get; set; }
        public decimal CostoLista { get; set; }
        public decimal PrecioFinal { get; set; }
        public bool Bonificado { get; set; }
        public string ReglaNombre { get; set; }
    }
}
