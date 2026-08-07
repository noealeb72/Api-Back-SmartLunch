using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// DTO para listado de comandas con paginación
    /// </summary>
    public class ComandaListadoDto
    {
        public int Id { get; set; }
        public int Npedido { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }
        public bool Bonificado { get; set; }
        public bool Invitado { get; set; }
        public int? Calificacion { get; set; }
        public string Estado { get; set; }
        /// <summary>Comentario que agrega el usuario al pedido (para mostrar en despacho).</summary>
        public string Comentario { get; set; }

        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }

        public int PlatoId { get; set; }
        public string PlatoDescripcion { get; set; }

        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }

        public int PlantaId { get; set; }
        public string PlantaDescripcion { get; set; }

        public int CentroDeCostoId { get; set; }
        public string CentroDeCostoDescripcion { get; set; }

        public int ProyectoId { get; set; }
        public string ProyectoDescripcion { get; set; }

        public int JerarquiaId { get; set; }
        public string JerarquiaDescripcion { get; set; }

        public string Foto { get; set; }
    }

    /// <summary>
    /// DTO para detalle completo de una comanda
    /// </summary>
    public class ComandaDetalleDto
    {
        public int Id { get; set; }
        public int Npedido { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }
        public bool Bonificado { get; set; }
        public bool Invitado { get; set; }
        public int? Calificacion { get; set; }
        public string Estado { get; set; }
        public string Comentario { get; set; }

        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }

        public int PlatoId { get; set; }
        public string PlatoDescripcion { get; set; }

        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }

        public int PlantaId { get; set; }
        public string PlantaDescripcion { get; set; }

        public int CentroDeCostoId { get; set; }
        public string CentroDeCostoDescripcion { get; set; }

        public int ProyectoId { get; set; }
        public string ProyectoDescripcion { get; set; }

        public int JerarquiaId { get; set; }
        public string JerarquiaDescripcion { get; set; }

        public decimal PlatoImporte { get; set; }

        /// <summary>Costo de proveedor congelado al momento del pedido (sl_comanda.costo_proveedor).</summary>
        public decimal CostoProveedor { get; set; }

        public string Foto { get; set; }

        public int NutricionalId { get; set; }
        public string NutricionalNombre { get; set; }
    }

    /// <summary>
    /// DTO para crear una nueva comanda
    /// </summary>
    public class ComandaCreateDto
    {
        public int Id { get; set; }
        public int Npedido { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }
        public bool Bonificado { get; set; }
        public bool Invitado { get; set; }
        public int? Calificacion { get; set; }
        public string Estado { get; set; }
        public string Comentario { get; set; }

        //public int UsuarioId { get; set; }
        //public string UsuarioNombre { get; set; }

        public int PlatoId { get; set; }
        //public string PlatoDescripcion { get; set; }

        public int TurnoId { get; set; }
        //public string TurnoNombre { get; set; }

        public int PlantaId { get; set; }
        //public string PlantaDescripcion { get; set; }

        public int CentroDeCostoId { get; set; }
        //public string CentroDeCostoDescripcion { get; set; }

        public int ProyectoId { get; set; }
        //public string ProyectoDescripcion { get; set; }

        public int JerarquiaId { get; set; }
        //public string JerarquiaDescripcion { get; set; }

        public decimal PlatoImporte { get; set; }

        //public string Foto { get; set; }

        //public int NutricionalId { get; set; }
        //public string NutricionalNombre { get; set; }

        public int MenuddId { get; set; }
    }

    /// <summary>
    /// DTO para actualizar una comanda existente
    /// </summary>
    public class ComandaUpdateDto
    {
        /// <summary>
        /// ID de la comanda a actualizar
        /// </summary>
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }

        /// <summary>
        /// Monto de la comanda
        /// </summary>
        [Required(ErrorMessage = "El monto es obligatorio")]
        [Range(0, double.MaxValue, ErrorMessage = "El monto debe ser mayor o igual a 0")]
        public decimal Monto { get; set; }

        /// <summary>
        /// Indica si la comanda está bonificada
        /// </summary>
        public bool Bonificado { get; set; }

        /// <summary>
        /// Indica si es un invitado
        /// </summary>
        public bool Invitado { get; set; }

        /// <summary>
        /// Calificación del servicio (1-5, opcional)
        /// </summary>
        [Range(1, 5, ErrorMessage = "La calificación debe estar entre 1 y 5")]
        public int? Calificacion { get; set; }

        /// <summary>
        /// Estado de la comanda: "pendiente", "preparando", "listo", "entregado", "cancelado"
        /// </summary>
        [StringLength(50, ErrorMessage = "El estado no puede exceder 50 caracteres")]
        public string Estado { get; set; }

        /// <summary>
        /// Comentarios adicionales
        /// </summary>
        [StringLength(500, ErrorMessage = "El comentario no puede exceder 500 caracteres")]
        public string Comentario { get; set; }
    }

    /// <summary>
    /// DTO para acciones de eliminar o activar una comanda
    /// </summary>
    public class ComandaAccionDto
    {
        /// <summary>
        /// ID de la comanda sobre la que se realizará la acción
        /// </summary>
        [Required(ErrorMessage = "El número de pedido es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El número de pedido debe ser mayor a 0")]
        public int Npedido { get; set; }

        /// <summary>
        /// Calificación que el usuario le da al pedido (opcional)
        /// </summary>
        public int? Calificacion { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class ComandaImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirNpedido { get; set; }
        public bool IncluirFecha { get; set; }
        public bool IncluirUsuario { get; set; }
        public bool IncluirPlato { get; set; }
        public bool IncluirTurno { get; set; }
        public bool IncluirPlanta { get; set; }
        public bool IncluirCentroCosto { get; set; }
        public bool IncluirProyecto { get; set; }
        public bool IncluirJerarquia { get; set; }
        public bool IncluirMonto { get; set; }
        public bool IncluirBonificado { get; set; }
        public bool IncluirInvitado { get; set; }
        public bool IncluirEstado { get; set; }
        public bool IncluirCalificacion { get; set; }

        // Filtros
        public DateTime? FechaDesde { get; set; }
        public DateTime? FechaHasta { get; set; }
        public int? UsuarioId { get; set; }
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo" o estados específicos
    }

    public class ComandaImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public int? Npedido { get; set; }
        public DateTime? Fecha { get; set; }
        public string Usuario { get; set; }
        public string Plato { get; set; }
        public string Turno { get; set; }
        public string Planta { get; set; }
        public string CentroCosto { get; set; }
        public string Proyecto { get; set; }
        public string Jerarquia { get; set; }
        public decimal? Monto { get; set; }
        public bool? Bonificado { get; set; }
        public bool? Invitado { get; set; }
        public string Estado { get; set; }
        public int? Calificacion { get; set; }
    }
}
