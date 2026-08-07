using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace smartlunch_api.Dtos
{
    // ================= LISTADO =================
    public class MenuddListadoDto
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; }

        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }

        public int PlatoId { get; set; }
        public string PlatoNombre { get; set; }

        public int Cantidad { get; set; }
        public int Comandas { get; set; }
        public int Despachado { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int? PlanNutricionalId { get; set; }
        public string PlanNutricionalNombre { get; set; }

        public bool Activo { get; set; }
    }

    // ================= DETALLE =================
    public class MenuddDetalleDto
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; }

        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }

        public int PlatoId { get; set; }
        public string PlatoNombre { get; set; }

        public int Cantidad { get; set; }
        public int Comandas { get; set; }
        public int Despachado { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int? PlanNutricionalId { get; set; }
        public string PlanNutricionalNombre { get; set; }

        // Campos de auditoría
        public bool DeleteMark { get; set; }
        public DateTime? Createdate { get; set; }
        public string Createuser { get; set; }
        public DateTime? Updatedate { get; set; }
        public string Updateuser { get; set; }
    }

    // ================= CREATE =================
    public class MenuddCreateDto
    {
        [Required(ErrorMessage = "La fecha es obligatoria")]
        public DateTime Fecha { get; set; }

        [Required(ErrorMessage = "El ID de turno es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de turno debe ser mayor a 0")]
        public int TurnoId { get; set; }

        [Required(ErrorMessage = "El ID de plato es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de plato debe ser mayor a 0")]
        public int PlatoId { get; set; }

        [Required(ErrorMessage = "La cantidad es obligatoria")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public int Cantidad { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El número de comandas debe ser mayor o igual a 0")]
        public int? Comandas { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "El número de despachados debe ser mayor o igual a 0")]
        public int? Despachado { get; set; }

        [Required(ErrorMessage = "El ID de planta es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de planta debe ser mayor a 0")]
        public int PlantaId { get; set; }

        [Required(ErrorMessage = "El ID de centro de costo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de centro de costo debe ser mayor a 0")]
        public int CentroCostoId { get; set; }

        [Required(ErrorMessage = "El ID de proyecto es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de proyecto debe ser mayor a 0")]
        public int ProyectoId { get; set; }

        [Required(ErrorMessage = "El ID de jerarquía es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de jerarquía debe ser mayor a 0")]
        public int JerarquiaId { get; set; }
    }

    // ================= CREAR MÚLTIPLES (un ítem del array) =================
    /// <summary>Un ítem del array para POST api/menudd/crear-multiples.</summary>
    public class MenuddItemCrearDto
    {
        [Required(ErrorMessage = "El ID de plato es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de plato debe ser mayor a 0")]
        public int PlatoId { get; set; }

        [Required(ErrorMessage = "El ID de turno es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de turno debe ser mayor a 0")]
        public int TurnoId { get; set; }

        [Required(ErrorMessage = "El ID de jerarquía es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de jerarquía debe ser mayor a 0")]
        public int JerarquiaId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "El ID de plan nutricional debe ser mayor a 0")]
        public int? Plannutricional_id { get; set; }

        [Required(ErrorMessage = "El ID de proyecto es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de proyecto debe ser mayor a 0")]
        public int ProyectoId { get; set; }

        [Required(ErrorMessage = "El ID de centro de costo es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de centro de costo debe ser mayor a 0")]
        public int CentroCostoId { get; set; }

        [Required(ErrorMessage = "El ID de planta es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID de planta debe ser mayor a 0")]
        public int PlantaId { get; set; }

        [Required(ErrorMessage = "La cantidad es obligatoria")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser mayor a 0")]
        public int Cantidad { get; set; }

        [Required(ErrorMessage = "La fecha es obligatoria")]
        public DateTime Fecha { get; set; }
    }

    /// <summary>Request para POST api/menudd/crear-multiples.</summary>
    public class MenuddCrearMultiplesRequestDto
    {
        [Required(ErrorMessage = "El array de menús es obligatorio")]
        public List<MenuddItemCrearDto> Menus { get; set; }
    }

    /// <summary>Ítem que no se creó (duplicado u otro motivo) en crear-multiples.</summary>
    public class MenuddItemNoCreadoDto
    {
        public int Indice { get; set; }
        public string Motivo { get; set; }
    }

    /// <summary>Respuesta de POST api/menudd/crear-multiples.</summary>
    public class MenuddCrearMultiplesResponseDto
    {
        public List<MenuddDetalleDto> Creados { get; set; }
        public List<MenuddItemNoCreadoDto> NoCreados { get; set; }
        /// <summary>Mensaje resumen para mostrar en el front (ej. "Se guardaron 2. 1 duplicado omitido.").</summary>
        public string Mensaje { get; set; }
    }

    // ================= UPDATE =================
    public class MenuddUpdateDto : MenuddCreateDto
    {
        [Required(ErrorMessage = "El ID es obligatorio")]
        [Range(1, int.MaxValue, ErrorMessage = "El ID debe ser mayor a 0")]
        public int Id { get; set; }
    }

    // ============== MENÚ POR TURNO (TOTEM) ==============
    public class MenuddPorTurnoDto
    {
        public int Id { get; set; }

        public DateTime Fecha { get; set; }

        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }

        public int PlatoId { get; set; }
        public string PlatoNombre { get; set; }

        public int Cantidad { get; set; }
        public int Despachado { get; set; }
        public int Disponible { get; set; }

        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }

        public int CentroCostoId { get; set; }
        public string CentroCostoNombre { get; set; }

        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }

        public int? NutricionalId { get; set; }
        public string NutricionalNombre { get; set; }
        public string Foto { get; set; }
        public decimal Importe { get; set; }
        public string Estado { get; set; }
    }

    // ================= IMPRESIÓN =================
    public class MenuddImpresionRequestDto
    {
        // Columnas a incluir
        public bool IncluirPlato { get; set; }
        public bool IncluirTurno { get; set; }
        public bool IncluirProyecto { get; set; }
        public bool IncluirPlanta { get; set; }
        public bool IncluirFecha { get; set; }
        public bool IncluirPlanNutricional { get; set; }
        public bool IncluirJerarquia { get; set; }
        public bool IncluirCentroCosto { get; set; }
        public bool IncluirCantidad { get; set; }
        public bool IncluirEstado { get; set; }

        // Filtros
        public DateTime? Fecha { get; set; }
        public int? TurnoId { get; set; }
        public int? PlanNutricionalId { get; set; }
        public int? JerarquiaId { get; set; }
        public int? ProyectoId { get; set; }
        public int? CentroCostoId { get; set; }
        public int? PlantaId { get; set; }
        public string Estado { get; set; } // "Todos", "Activo", "Inactivo"
    }

    public class MenuddImpresionDto
    {
        // Campos opcionales - solo se llenarán si están seleccionados
        public string Plato { get; set; }
        public string Turno { get; set; }
        public string Proyecto { get; set; }
        public string Planta { get; set; }
        public DateTime? Fecha { get; set; }
        public string PlanNutricional { get; set; }
        public string Jerarquia { get; set; }
        public string CentroCosto { get; set; }
        public int? Cantidad { get; set; }
        public string Estado { get; set; }
    }

    // ================= MENÚ Y COMANDAS POR TURNO =================
    public class MenuYComandasPorTurnoDto
    {
        public List<MenuddPorTurnoDto> MenuDelDia { get; set; }
        public List<ComandaDetalleDto> Comandas { get; set; }
    }

    public class MenuddComedorDisponibleDto
    {
        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }
    }
}
