using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SEG_INV.Controllers.Models;
using SEG_INV.Models;

namespace SEG_INV.Controllers
{
    public class ComprasController : Controller
    {
        private readonly ConexionBD _conexion;

        public ComprasController(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
            _conexion = new ConexionBD(connectionString);
        }

        // ─────────────────────────────────────────────────────────────────
        // MIDDLEWARE: VERIFICAR SESIÓN Y ROL DE GERENTE
        // ─────────────────────────────────────────────────────────────────
        private bool EsGerente()
        {
            string? rol = HttpContext.Session.GetString("Rol");
            return rol == "GERENTE";
        }

        private IActionResult RedirigirSiNoAutorizado()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                return RedirectToAction("Login", "Account");
            if (!EsGerente())
                return RedirectToAction("Login", "Account");
            return null!;
        }

        // ─────────────────────────────────────────────────────────────────
        // INDEX - DASHBOARD PRINCIPAL DE COMPRAS
        // ─────────────────────────────────────────────────────────────────
        public IActionResult Index()
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            var vm = new ComprasViewModel
            {
                Compras = ObtenerCompras(),
                Proveedores = ObtenerProveedores(),
                Productos = ObtenerProductos()
            };

            ViewBag.NombreUsuario = HttpContext.Session.GetString("Nombre");
            return View(vm);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ PROVEEDORES
        // ═══════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearProveedor(Proveedor proveedor)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Datos del proveedor inválidos.";
                return  RedirectToAction("Gerente", "Home");;
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = @"INSERT INTO proveedores (nombre_empresa, telefono, descripcion)
                               VALUES (@nombre, @telefono, @desc)";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre", proveedor.NombreEmpresa);
                cmd.Parameters.AddWithValue("@telefono", proveedor.Telefono ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", proveedor.Descripcion ?? (object)DBNull.Value);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = $"Proveedor '{proveedor.NombreEmpresa}' creado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear proveedor: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarProveedor(Proveedor proveedor)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (!ModelState.IsValid || proveedor.Id <= 0)
            {
                TempData["Error"] = "Datos del proveedor inválidos.";
                return RedirectToAction("Gerente", "Home");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = @"UPDATE proveedores 
                               SET nombre_empresa=@nombre, telefono=@telefono, descripcion=@desc
                               WHERE id=@id";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre", proveedor.NombreEmpresa);
                cmd.Parameters.AddWithValue("@telefono", proveedor.Telefono ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@desc", proveedor.Descripcion ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@id", proveedor.Id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Proveedor actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar proveedor: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarProveedor(int id)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd = new MySqlCommand("DELETE FROM proveedores WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Proveedor eliminado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo eliminar el proveedor (puede tener compras asociadas): " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ PRODUCTOS
        // ═══════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearProducto(Producto producto)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Datos del producto inválidos.";
                return RedirectToAction("Gerente", "Home");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = "INSERT INTO productos (nombre, inventario_actual) VALUES (@nombre, @inv)";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre", producto.Nombre);
                cmd.Parameters.AddWithValue("@inv", producto.InventarioActual);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = $"Producto '{producto.Nombre}' creado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear producto: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarProducto(Producto producto)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (!ModelState.IsValid || producto.Id <= 0)
            {
                TempData["Error"] = "Datos del producto inválidos.";
                return RedirectToAction("Gerente", "Home");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = "UPDATE productos SET nombre=@nombre, inventario_actual=@inv WHERE id=@id";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre", producto.Nombre);
                cmd.Parameters.AddWithValue("@inv", producto.InventarioActual);
                cmd.Parameters.AddWithValue("@id", producto.Id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Producto actualizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar producto: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarProducto(int id)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd = new MySqlCommand("DELETE FROM productos WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Producto eliminado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "No se pudo eliminar el producto: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ COMPRAS
        // ═══════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearCompra(Compra compra, List<int> productoIds, List<int> cantidades)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (compra.ProveedorId <= 0)
            {
                TempData["Error"] = "Debe seleccionar un proveedor.";
                return RedirectToAction("Gerente", "Home");
            }

            if (productoIds == null || productoIds.Count == 0)
            {
                TempData["Error"] = "Debe agregar al menos un producto al detalle de la compra.";
                return RedirectToAction("Gerente", "Home");
            }

            // Auto-asignar el gerente desde la sesión
            compra.Gerente = HttpContext.Session.GetString("Nombre");

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var transaction = conn.BeginTransaction();

                // 1. Insertar la compra
                string sqlCompra = @"INSERT INTO compras (proveedor_id, fecha_entrega_provable, gerente, estado)
                                     VALUES (@prov, @fecha, @gerente, 'PENDIENTE');
                                     SELECT LAST_INSERT_ID();";
                using var cmdCompra = new MySqlCommand(sqlCompra, conn, transaction);
                cmdCompra.Parameters.AddWithValue("@prov", compra.ProveedorId);
                cmdCompra.Parameters.AddWithValue("@fecha", compra.FechaEntregaProbable.HasValue
                    ? compra.FechaEntregaProbable.Value.ToString("yyyy-MM-dd")
                    : (object)DBNull.Value);
                cmdCompra.Parameters.AddWithValue("@gerente", compra.Gerente ?? (object)DBNull.Value);

                long compraId = Convert.ToInt64(cmdCompra.ExecuteScalar());

                // 2. Insertar el detalle
                string sqlDetalle = "INSERT INTO detalle_compra (compra_id, producto_id, cantidad) VALUES (@cid, @pid, @qty)";
                for (int i = 0; i < productoIds.Count; i++)
                {
                    if (productoIds[i] <= 0) continue;
                    using var cmdDetalle = new MySqlCommand(sqlDetalle, conn, transaction);
                    cmdDetalle.Parameters.AddWithValue("@cid", compraId);
                    cmdDetalle.Parameters.AddWithValue("@pid", productoIds[i]);
                    cmdDetalle.Parameters.AddWithValue("@qty", i < cantidades.Count ? cantidades[i] : 1);
                    cmdDetalle.ExecuteNonQuery();
                }

                transaction.Commit();
                TempData["Exito"] = $"Compra #{compraId} creada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear la compra: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarCompra(Compra compra)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (compra.Id <= 0 || compra.ProveedorId <= 0)
            {
                TempData["Error"] = "Datos de compra inválidos.";
                return RedirectToAction("Gerente", "Home");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = @"UPDATE compras 
                               SET proveedor_id=@prov, fecha_entrega_provable=@fecha, gerente=@gerente, estado=@estado
                               WHERE id=@id";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@prov", compra.ProveedorId);
                cmd.Parameters.AddWithValue("@fecha", compra.FechaEntregaProbable.HasValue
                    ? compra.FechaEntregaProbable.Value.ToString("yyyy-MM-dd")
                    : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@gerente", compra.Gerente ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@estado", compra.Estado);
                cmd.Parameters.AddWithValue("@id", compra.Id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Compra actualizada correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar compra: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarCompra(int id)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd = new MySqlCommand("DELETE FROM compras WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Compra eliminada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar la compra: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ DETALLE DE COMPRA - EDITAR LÍNEAS INDIVIDUALES
        // ═══════════════════════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarDetalle(int id, int productoId, int cantidad)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = "UPDATE detalle_compra SET producto_id=@pid, cantidad=@qty WHERE id=@id";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@pid", productoId);
                cmd.Parameters.AddWithValue("@qty", cantidad);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Detalle actualizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar detalle: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarDetalle(int id)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd = new MySqlCommand("DELETE FROM detalle_compra WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Línea de detalle eliminada.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar detalle: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AgregarDetalle(int compraId, int productoId, int cantidad)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (compraId <= 0 || productoId <= 0 || cantidad <= 0)
            {
                TempData["Error"] = "Datos de detalle inválidos.";
                return RedirectToAction("Gerente", "Home");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = "INSERT INTO detalle_compra (compra_id, producto_id, cantidad) VALUES (@cid, @pid, @qty)";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@cid", compraId);
                cmd.Parameters.AddWithValue("@pid", productoId);
                cmd.Parameters.AddWithValue("@qty", cantidad);
                cmd.ExecuteNonQuery();
                TempData["Exito"] = "Producto agregado al detalle.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al agregar detalle: " + ex.Message;
            }

            return RedirectToAction("Gerente", "Home");
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ MÉTODOS PRIVADOS DE ACCESO A DATOS
        // ═══════════════════════════════════════════════════════════════════

        private List<Proveedor> ObtenerProveedores()
        {
            var lista = new List<Proveedor>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd = new MySqlCommand("SELECT id, nombre_empresa, telefono, descripcion, fecha_creacion FROM proveedores ORDER BY nombre_empresa", conn);
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
            catch { /* log */ }
            return lista;
        }

        private List<Producto> ObtenerProductos()
        {
            var lista = new List<Producto>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd = new MySqlCommand("SELECT id, nombre, inventario_actual, fecha_creacion FROM productos ORDER BY nombre", conn);
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
            catch { /* log */ }
            return lista;
        }

        private List<Compra> ObtenerCompras()
        {
            var compras = new List<Compra>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                // Cargar compras con nombre de proveedor
                string sqlCompras = @"
                    SELECT c.id, c.proveedor_id, p.nombre_empresa, c.fecha_entrega_provable, 
                           c.gerente, c.estado, c.fecha_creacion
                    FROM compras c
                    INNER JOIN proveedores p ON c.proveedor_id = p.id
                    ORDER BY c.fecha_creacion DESC";

                using var cmd = new MySqlCommand(sqlCompras, conn);
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

                    using var cmdD = new MySqlCommand(sqlDetalle, conn);
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
            catch { /* log */ }
            return compras;
        }
    }
}