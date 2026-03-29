using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SEG_INV.Models
{
    // ─────────────────────────────────────────────────
    // USUARIO ADMIN  (entidad completa para CRUD)
    // ─────────────────────────────────────────────────
    public class UsuarioAdmin
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "El nombre completo es requerido.")]
        [StringLength(100, ErrorMessage = "Máximo 100 caracteres.")]
        [Display(Name = "Nombre Completo")]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El nombre de usuario es requerido.")]
        [StringLength(50, ErrorMessage = "Máximo 50 caracteres.")]
        [Display(Name = "Usuario")]
        public string Usuario { get; set; } = string.Empty;

        // Nullable: sólo se exige al crear; en edición, vacío = no cambiar contraseña
        [StringLength(255)]
        [Display(Name = "Contraseña")]
        public string? Contrasena { get; set; }

        [Required(ErrorMessage = "Debe seleccionar un rol.")]
        [Display(Name = "Rol")]
        public string Rol { get; set; } = "BODEGA";

        [Display(Name = "Activo")]
        public bool Activo { get; set; } = true;

        public DateTime? FechaCreacion { get; set; }
    }

    // ─────────────────────────────────────────────────
    // KPI / ESTADÍSTICAS DEL DASHBOARD ADMIN
    // ─────────────────────────────────────────────────
    public class AdminEstadisticas
    {
        // Usuarios
        public int TotalUsuarios     { get; set; }
        public int UsuariosActivos   { get; set; }
        public int UsuariosInactivos { get; set; }

        // Compras
        public int TotalCompras      { get; set; }
        public int ComprasPendientes { get; set; }
        public int ComprasRecibidas  { get; set; }
        public int ComprasNovedad    { get; set; }

        // Inventario
        public int TotalProductos    { get; set; }
        public int ProductosSinStock { get; set; }
        public int ProductosCriticos { get; set; }  // stock <= 5

        // Proveedores y Recepciones
        public int TotalProveedores  { get; set; }
        public int TotalRecepciones  { get; set; }
    }

    // ─────────────────────────────────────────────────
    // LOG DE AUDITORÍA
    // ─────────────────────────────────────────────────
    public class LogActividad
    {
        public int      Id           { get; set; }
        public string   Accion       { get; set; } = string.Empty;  // CREAR | EDITAR | ELIMINAR
        public string   Entidad      { get; set; } = string.Empty;  // Usuario, Compra, etc.
        public int?     EntidadId    { get; set; }
        public string   Descripcion  { get; set; } = string.Empty;
        public string   RealizadoPor { get; set; } = string.Empty;
        public DateTime FechaHora    { get; set; }
    }

    // ─────────────────────────────────────────────────
    // VIEWMODEL PRINCIPAL DEL ADMINISTRADOR
    // ─────────────────────────────────────────────────
    public class AdminViewModel
    {
        public List<UsuarioAdmin>  Usuarios      { get; set; } = new();
        public AdminEstadisticas   Estadisticas  { get; set; } = new();
        public List<LogActividad>  Logs          { get; set; } = new();

        // Objeto de trabajo para alta y edición en la misma vista
        public UsuarioAdmin        UsuarioActual { get; set; } = new();
    }
}