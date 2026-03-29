using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SEG_INV.Models
{
    // ─────────────────────────────────────────────────
    // RECEPCIÓN BODEGA
    // ─────────────────────────────────────────────────
    public class RecepcionBodega
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una compra.")]
        [Display(Name = "Compra / Pedido")]
        public int CompraId { get; set; }

        // Datos del pedido (para mostrar en la vista)
        public string? NombreProveedor { get; set; }
        public DateTime? FechaEntregaProbable { get; set; }
        public string? EstadoCompra { get; set; }

        [Display(Name = "Fecha Recibido")]
        [DataType(DataType.Date)]
        public DateTime? FechaRecibido { get; set; }

        [Display(Name = "Hora")]
        [DataType(DataType.Time)]
        public TimeSpan? Hora { get; set; }

        [StringLength(100)]
        [Display(Name = "N° Factura")]
        public string? Factura { get; set; }

        [Display(Name = "Contenido / Descripción")]
        public string? Contenido { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Cantidad de bultos no válida.")]
        [Display(Name = "Cantidad de Bultos")]
        public int? CantidadBultos { get; set; }

        [StringLength(100)]
        [Display(Name = "Quien Recibe")]
        public string? QuienRecibe { get; set; }

        [Display(Name = "Novedades / Observaciones")]
        public string? Novedades { get; set; }

        public DateTime? FechaRegistro { get; set; }

        // Para el check "¿Llegó completo?"
        [Display(Name = "¿El pedido llegó?")]
        public bool LlegoCompleto { get; set; } = true;
    }

    // ─────────────────────────────────────────────────
    // COMPRA PENDIENTE (resumen para listar)
    // ─────────────────────────────────────────────────
    public class CompraPendiente
    {
        public int Id { get; set; }
        public string NombreProveedor { get; set; } = "";
        public DateTime? FechaEntregaProbable { get; set; }
        public string Estado { get; set; } = "PENDIENTE";
        public List<DetalleCompra> Detalles { get; set; } = new();

        // Si ya tiene recepción registrada
        public bool TieneRecepcion { get; set; } = false;
        public int? RecepcionId { get; set; }
    }

    // ─────────────────────────────────────────────────
    // VIEWMODEL PRINCIPAL DE BODEGA
    // ─────────────────────────────────────────────────
    public class BodegaViewModel
    {
        public List<CompraPendiente> PedidosPendientes { get; set; } = new();
        public List<RecepcionBodega> RecepcionesRegistradas { get; set; } = new();
        public RecepcionBodega RecepcionActual { get; set; } = new();
    }
}