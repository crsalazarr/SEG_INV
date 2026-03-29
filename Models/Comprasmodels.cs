using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SEG_INV.Models
{
    // ─────────────────────────────────────────────────
    // PROVEEDOR
    // ─────────────────────────────────────────────────
    public class Proveedor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre de la empresa es requerido.")]
        [StringLength(150, ErrorMessage = "Máximo 150 caracteres.")]
        [Display(Name = "Nombre Empresa")]
        public string NombreEmpresa { get; set; } = "";

        [StringLength(20, ErrorMessage = "Máximo 20 caracteres.")]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [Display(Name = "Descripción")]
        public string? Descripcion { get; set; }

        public DateTime? FechaCreacion { get; set; }
    }

    // ─────────────────────────────────────────────────
    // PRODUCTO
    // ─────────────────────────────────────────────────
    public class Producto
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre del producto es requerido.")]
        [StringLength(150, ErrorMessage = "Máximo 150 caracteres.")]
        [Display(Name = "Nombre")]
        public string Nombre { get; set; } = "";

        [Range(0, int.MaxValue, ErrorMessage = "El inventario no puede ser negativo.")]
        [Display(Name = "Inventario Actual")]
        public int InventarioActual { get; set; } = 0;

        public DateTime? FechaCreacion { get; set; }
    }

    // ─────────────────────────────────────────────────
    // COMPRA
    // ─────────────────────────────────────────────────
    public class Compra
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un proveedor.")]
        [Display(Name = "Proveedor")]
        public int ProveedorId { get; set; }

        public string? NombreProveedor { get; set; }

        [Display(Name = "Fecha Entrega Probable")]
        [DataType(DataType.Date)]
        public DateTime? FechaEntregaProbable { get; set; }

        [StringLength(100)]
        [Display(Name = "Gerente")]
        public string? Gerente { get; set; }

        [Display(Name = "Estado")]
        public string Estado { get; set; } = "PENDIENTE";

        public DateTime? FechaCreacion { get; set; }

        // Detalle de la compra
        public List<DetalleCompra> Detalles { get; set; } = new();
    }

    // ─────────────────────────────────────────────────
    // DETALLE DE COMPRA
    // ─────────────────────────────────────────────────
    public class DetalleCompra
    {
        public int Id { get; set; }

        [Required]
        public int CompraId { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un producto.")]
        [Display(Name = "Producto")]
        public int ProductoId { get; set; }

        public string? NombreProducto { get; set; }

        [Required(ErrorMessage = "La cantidad es requerida.")]
        [Range(1, int.MaxValue, ErrorMessage = "La cantidad debe ser al menos 1.")]
        [Display(Name = "Cantidad")]
        public int Cantidad { get; set; }
    }

    // ─────────────────────────────────────────────────
    // VIEWMODEL PARA LA VISTA PRINCIPAL DEL GERENTE
    // ─────────────────────────────────────────────────
    public class ComprasViewModel
    {
        public List<Compra> Compras { get; set; } = new();
        public List<Proveedor> Proveedores { get; set; } = new();
        public List<Producto> Productos { get; set; } = new();

        // Para el formulario de nueva/editar compra
        public Compra CompraActual { get; set; } = new();
    }
}