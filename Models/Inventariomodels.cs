using System;
using System.Collections.Generic;

namespace SEG_INV.Models
{
    // ─────────────────────────────────────────────────
    // PRODUCTO CON INFO DE RECEPCIÓN (para inventario)
    // ─────────────────────────────────────────────────
    public class ProductoInventario
    {
        public int ProductoId { get; set; }
        public string NombreProducto { get; set; } = "";
        public int InventarioActual { get; set; }

        // Cuántas unidades han llegado vía recepciones
        public int UnidadesRecibidas { get; set; }

        // Cuántas unidades están aún pendientes (en pedidos no recibidos)
        public int UnidadesPendientes { get; set; }

        // Para el detalle de pedidos por producto
        public List<PedidoPorProducto> PedidosActivos { get; set; } = new();
    }

    // ─────────────────────────────────────────────────
    // PEDIDO INDIVIDUAL POR PRODUCTO
    // ─────────────────────────────────────────────────
    public class PedidoPorProducto
    {
        public int CompraId { get; set; }
        public string NombreProveedor { get; set; } = "";
        public int Cantidad { get; set; }
        public string Estado { get; set; } = "";
        public DateTime? FechaEntregaProbable { get; set; }
        public DateTime? FechaRecibido { get; set; }
        public string? Factura { get; set; }
        public string? QuienRecibio { get; set; }
        public string? Novedades { get; set; }
    }

    // ─────────────────────────────────────────────────
    // REGISTRO COMPLETO DE RECEPCIÓN (para historial)
    // ─────────────────────────────────────────────────
    public class RecepcionDetalle
    {
        public int Id { get; set; }
        public int CompraId { get; set; }
        public string NombreProveedor { get; set; } = "";
        public DateTime? FechaRecibido { get; set; }
        public TimeSpan? Hora { get; set; }
        public string? Factura { get; set; }
        public string? Contenido { get; set; }
        public int? CantidadBultos { get; set; }
        public string? QuienRecibe { get; set; }
        public string? Novedades { get; set; }
        public DateTime? FechaRegistro { get; set; }
        public string EstadoCompra { get; set; } = "";

        // Productos de esa compra
        public List<DetalleCompra> Productos { get; set; } = new();

        public bool TieneNovedad => Novedades != null && Novedades.Contains("[PEDIDO NO RECIBIDO");
    }

    // ─────────────────────────────────────────────────
    // VIEWMODEL INVENTARIO
    // ─────────────────────────────────────────────────
    public class InventarioViewModel
    {
        // Resumen por producto
        public List<ProductoInventario> Productos { get; set; } = new();

        // Pedidos pendientes (no recibidos)
        public List<CompraPendiente> PedidosPendientes { get; set; } = new();

        // Pedidos recibidos con su recepción
        public List<RecepcionDetalle> PedidosRecibidos { get; set; } = new();

        // Pedidos con novedad
        public List<RecepcionDetalle> PedidosNovedad { get; set; } = new();

        // Contadores para las tarjetas KPI
        public int TotalPendientes { get; set; }
        public int TotalRecibidos { get; set; }
        public int TotalNovedad { get; set; }
        public int TotalProductos { get; set; }
    }
}