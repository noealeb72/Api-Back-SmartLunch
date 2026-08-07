using System;
using System.Collections.Generic;

namespace smartlunch_api.Dtos
{
    /// <summary>
    /// DTO para reporte de usuario con todas sus relaciones
    /// </summary>
    public class ReporteUsuarioDto
    {
        // Datos del usuario
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public int Dni { get; set; }
        public string Foto { get; set; }
        public int Legajo { get; set; }
        public string Domicilio { get; set; }

        // Relaciones del usuario
        public int? PlantaId { get; set; }
        public string PlantaNombre { get; set; }
        public string PlantaDescripcion { get; set; }

        public int PerfilNutricionalId { get; set; }
        public string PerfilNutricionalNombre { get; set; }

        public int? CentroDeCostoId { get; set; }
        public string CentroDeCostoNombre { get; set; }
        public string CentroDeCostoDescripcion { get; set; }

        public int? ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }
        public string ProyectoDescripcion { get; set; }

        public int? JerarquiaId { get; set; }
        public string JerarquiaNombre { get; set; }
        public string JerarquiaDescripcion { get; set; }

        public int? PlanNutricionalId { get; set; }
        public string PlanNutricionalNombre { get; set; }
        public string PlanNutricionalDescripcion { get; set; }

        // Bonificaciones
        public int Bonificados { get; set; }
        public int BonificacionesInvitadoAcum { get; set; }
        public int BonificadosInvitadosRango { get; set; }

        // Comandas del rango
        public List<ComandaReporteDto> Consumidos { get; set; }

        // Estadísticas del rango
        public double Monto { get; set; }
        public List<string> Estados { get; set; }
        public string UltimoEstado { get; set; }
        public string UltimoPlato { get; set; }
        public List<string> DescripcionesPlatos { get; set; }
    }

    /// <summary>
    /// DTO para comanda en reporte
    /// </summary>
    public class ComandaReporteDto
    {
        public int Id { get; set; }
        public int Npedido { get; set; }
        public DateTime Fecha { get; set; }
        public decimal Monto { get; set; }
        public string Estado { get; set; }
        public int PlatoId { get; set; }
        public string DescripcionPlato { get; set; }
        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }
        public bool Bonificado { get; set; }
        public bool Invitado { get; set; }
        public string Plato { get; set; }
    }

    /// <summary>
    /// DTO para reporte general (legacy - mantener por compatibilidad)
    /// </summary>
    public class ReporteGeneralDto
    {
        public int PlatosPlanta { get; set; }
        public int PlatosCc { get; set; }
        public int PlatosProyecto { get; set; }
        public int PlatosDistintos { get; set; }
        public double PromedioCalificacion { get; set; }
        public int Devueltos { get; set; }
        public double Monto { get; set; }
    }

    /// <summary>
    /// DTO para reporte de gestión (tabla detallada de comandas)
    /// </summary>
    public class ReporteGestionDto
    {
        public DateTime Fecha { get; set; }
        public string Planta { get; set; }
        public string CC { get; set; }
        public string Proyecto { get; set; }
        public string Perfil { get; set; }
        public int Legajo { get; set; }
        public string NombreCompleto { get; set; }
        public string Plato { get; set; }
        public string Estado { get; set; }
        public bool Bonificacion { get; set; }
        public decimal Costo { get; set; }
    }

    /// <summary>
    /// DTO para comanda en listado de reporte
    /// </summary>
    public class ComandaListadoReporteDto
    {
        public int Npedido { get; set; }
        public int TurnoId { get; set; }
        public string TurnoNombre { get; set; }
        public DateTime Fecha { get; set; }
        public int PlantaId { get; set; }
        public string PlantaNombre { get; set; }
        public int CentrodecostoId { get; set; }
        public string CentrodecostoNombre { get; set; }
        public int ProyectoId { get; set; }
        public string ProyectoNombre { get; set; }
        public int UsuarioId { get; set; }
        public string UsuarioNombre { get; set; }
        public string UsuarioApellido { get; set; }
        public int PlatoId { get; set; }
        public string PlatoNombre { get; set; }
        public string Estado { get; set; }
        public bool Bonificado { get; set; }
        public int? Calificacion { get; set; }
        public decimal Monto { get; set; }
        public bool Invitado { get; set; }
    }

    /// <summary>
    /// Reporte de facturación: comandas recibidas (R) con reparto empleado / empresa según bonificación de jerarquía del usuario.
    /// </summary>
    public class ReporteFacturacionDTO
    {
        public string Legajo { get; set; }
        public string Apellido { get; set; }
        public string Nombre { get; set; }
        /// <summary>Estado del usuario (Activo/Inactivo) según alta o baja lógica.</summary>
        public string EstadoUsuario { get; set; }
        public DateTime Fecha { get; set; }
        public string PlatoDescripcion { get; set; }
        public string PlantaNombre { get; set; }
        /// <summary>Precio original del plato (sl_plato.costo).</summary>
        public decimal PlatoImporte { get; set; }
        /// <summary>Monto que paga el empleado.</summary>
        public decimal MontoEmpleado { get; set; }
        /// <summary>Monto que asume la empresa (parte bonificada).</summary>
        public decimal MontoEmpresa { get; set; }
        /// <summary>Costo real que factura el proveedor, congelado al momento del pedido (sl_comanda.costo_proveedor).</summary>
        public decimal CostoProveedor { get; set; }
        public bool Bonificado { get; set; }
    }
}

