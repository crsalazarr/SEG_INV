using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using SEG_INV.Models;
using Microsoft.Extensions.Configuration; // Asegúrate de tener este using
using System.Collections.Generic; // Para las listas

namespace SEG_INV.Controllers;

public class HomeController : Controller
{
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    // Constructor para recibir la configuración
    public HomeController(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = configuration.GetConnectionString("MySQLConnection") 
            ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
    }

    public IActionResult Index()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    public IActionResult Gerente()
    {
        // Verificar si el usuario está autenticado y es gerente
        if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
            return RedirectToAction("Login", "Account");

        string? rol = HttpContext.Session.GetString("Rol");
        if (rol != "GERENTE")
            return RedirectToAction("Login", "Account");

        // Obtener los datos necesarios para el dashboard del gerente
        var vm = new ComprasViewModel
        {
            Compras = ObtenerCompras(),           // Llamamos a métodos locales
            Proveedores = ObtenerProveedores(),   // en lugar de usar ComprasController
            Productos = ObtenerProductos(),
            CompraActual = new Compra()
        };

        ViewBag.NombreUsuario = HttpContext.Session.GetString("Nombre");

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    // ═══════════════════════════════════════════════════════════════════
    // ██ MÉTODOS PRIVADOS (copiados de ComprasController)
    // ═══════════════════════════════════════════════════════════════════

    private List<Proveedor> ObtenerProveedores()
    {
        var lista = new List<Proveedor>();
        try
        {
            using var conn = new MySqlConnector.MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlConnector.MySqlCommand("SELECT id, nombre_empresa, telefono, descripcion, fecha_creacion FROM proveedores ORDER BY nombre_empresa", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Proveedor
                {
                    Id = reader.GetInt32("id"),
                    NombreEmpresa = reader.GetString("nombre_empresa"),
                    Telefono = reader.IsDBNull(reader.GetOrdinal("telefono")) ? null : reader.GetString("telefono"),
                    Descripcion = reader.IsDBNull(reader.GetOrdinal("descripcion")) ? null : reader.GetString("descripcion"),
                    FechaCreacion = reader.IsDBNull(reader.GetOrdinal("fecha_creacion")) ? null : reader.GetDateTime("fecha_creacion")
                });
            }
        }
        catch (System.Exception ex)
        {
            // Log del error si es necesario
            Console.WriteLine($"Error al obtener proveedores: {ex.Message}");
        }
        return lista;
    }

    private List<Producto> ObtenerProductos()
    {
        var lista = new List<Producto>();
        try
        {
            using var conn = new MySqlConnector.MySqlConnection(_connectionString);
            conn.Open();
            using var cmd = new MySqlConnector.MySqlCommand("SELECT id, nombre, inventario_actual, fecha_creacion FROM productos ORDER BY nombre", conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                lista.Add(new Producto
                {
                    Id = reader.GetInt32("id"),
                    Nombre = reader.GetString("nombre"),
                    InventarioActual = reader.IsDBNull(reader.GetOrdinal("inventario_actual")) ? 0 : reader.GetInt32("inventario_actual"),
                    FechaCreacion = reader.IsDBNull(reader.GetOrdinal("fecha_creacion")) ? null : reader.GetDateTime("fecha_creacion")
                });
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"Error al obtener productos: {ex.Message}");
        }
        return lista;
    }

    private List<Compra> ObtenerCompras()
    {
        var compras = new List<Compra>();
        try
        {
            using var conn = new MySqlConnector.MySqlConnection(_connectionString);
            conn.Open();
            
            // Cargar compras con nombre de proveedor
            string sqlCompras = @"
                SELECT c.id, c.proveedor_id, p.nombre_empresa, c.fecha_entrega_provable, 
                       c.gerente, c.estado, c.fecha_creacion
                FROM compras c
                INNER JOIN proveedores p ON c.proveedor_id = p.id
                ORDER BY c.fecha_creacion DESC";

            using var cmd = new MySqlConnector.MySqlCommand(sqlCompras, conn);
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                compras.Add(new Compra
                {
                    Id = reader.GetInt32("id"),
                    ProveedorId = reader.GetInt32("proveedor_id"),
                    NombreProveedor = reader.GetString("nombre_empresa"),
                    FechaEntregaProbable = reader.IsDBNull(reader.GetOrdinal("fecha_entrega_provable")) ? null : reader.GetDateTime("fecha_entrega_provable"),
                    Gerente = reader.IsDBNull(reader.GetOrdinal("gerente")) ? null : reader.GetString("gerente"),
                    Estado = reader.GetString("estado"),
                    FechaCreacion = reader.IsDBNull(reader.GetOrdinal("fecha_creacion")) ? null : reader.GetDateTime("fecha_creacion"),
                    Detalles = new List<DetalleCompra>()
                });
            }
            reader.Close();

            // Cargar detalles de cada compra
            foreach (var compra in compras)
            {
                string sqlDetalle = @"
                    SELECT dc.id, dc.compra_id, dc.producto_id, pr.nombre, dc.cantidad
                    FROM detalle_compra dc
                    INNER JOIN productos pr ON dc.producto_id = pr.id
                    WHERE dc.compra_id = @cid";

                using var cmdD = new MySqlConnector.MySqlCommand(sqlDetalle, conn);
                cmdD.Parameters.AddWithValue("@cid", compra.Id);
                using var readerD = cmdD.ExecuteReader();
                while (readerD.Read())
                {
                    compra.Detalles.Add(new DetalleCompra
                    {
                        Id = readerD.GetInt32("id"),
                        CompraId = compra.Id,
                        ProductoId = readerD.GetInt32("producto_id"),
                        NombreProducto = readerD.GetString("nombre"),
                        Cantidad = readerD.GetInt32("cantidad")
                    });
                }
                readerD.Close();
            }
        }
        catch (System.Exception ex)
        {
            Console.WriteLine($"Error al obtener compras: {ex.Message}");
        }
        return compras;
    }
}