using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SEG_INV.Controllers.Models;
using SEG_INV.Models;

namespace SEG_INV.Controllers
{
    public class BodegaController : Controller
    {
        private readonly ConexionBD _conexion;

        public BodegaController(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
            _conexion = new ConexionBD(connectionString);
        }

        // ─────────────────────────────────────────────────────────────────
        // MIDDLEWARE: VERIFICAR SESIÓN Y ROL BODEGA
        // ─────────────────────────────────────────────────────────────────
        private bool EsBodega()
        {
            string? rol = HttpContext.Session.GetString("Rol");
            return rol == "BODEGA" || rol == "GERENTE"; // gerente también puede ver
        }

        private IActionResult? RedirigirSiNoAutorizado()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                return RedirectToAction("Login", "Account");
            if (!EsBodega())
                return RedirectToAction("Login", "Account");
            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // INDEX - DASHBOARD DE BODEGA
        // ─────────────────────────────────────────────────────────────────
        public IActionResult Index()
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            var vm = new BodegaViewModel
            {
                PedidosPendientes = ObtenerPedidosPendientes(),
                RecepcionesRegistradas = ObtenerRecepciones()
            };

            ViewBag.NombreUsuario = HttpContext.Session.GetString("Nombre");
            ViewBag.Rol = HttpContext.Session.GetString("Rol");

            return View("~/Views/Home/Index_Bodega.cshtml", vm);
        }

        // ─────────────────────────────────────────────────────────────────
        // REGISTRAR RECEPCIÓN
        // ─────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RegistrarRecepcion(RecepcionBodega recepcion)
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            if (recepcion.CompraId <= 0)
            {
                TempData["Error"] = "Debe seleccionar un pedido válido.";
                return RedirectToAction(nameof(Index));
            }

            // Auto-completar quien recibe con el usuario en sesión si está vacío
            if (string.IsNullOrWhiteSpace(recepcion.QuienRecibe))
                recepcion.QuienRecibe = HttpContext.Session.GetString("Nombre");

            // Si el pedido no llegó, las novedades son obligatorias
            if (!recepcion.LlegoCompleto && string.IsNullOrWhiteSpace(recepcion.Novedades))
            {
                TempData["Error"] = "Debe describir las novedades cuando el pedido no llegó o llegó incompleto.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var transaction = conn.BeginTransaction();

                // 1. Insertar la recepción
                string sqlInsert = @"
                    INSERT INTO recepciones_bodega 
                        (compra_id, fecha_recibido, hora, factura, contenido, cantidad_bultos, quien_recibe, novedades)
                    VALUES 
                        (@compraId, @fecha, @hora, @factura, @contenido, @bultos, @quien, @novedades)";

                using var cmd = new MySqlCommand(sqlInsert, conn, transaction);
                cmd.Parameters.AddWithValue("@compraId", recepcion.CompraId);
                cmd.Parameters.AddWithValue("@fecha",
                    recepcion.FechaRecibido.HasValue
                        ? recepcion.FechaRecibido.Value.ToString("yyyy-MM-dd")
                        : DateTime.Today.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@hora",
                    recepcion.Hora.HasValue
                        ? recepcion.Hora.Value.ToString(@"hh\:mm\:ss")
                        : DateTime.Now.ToString("HH:mm:ss"));
                cmd.Parameters.AddWithValue("@factura", recepcion.Factura ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@contenido", recepcion.Contenido ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@bultos", recepcion.CantidadBultos.HasValue
                    ? recepcion.CantidadBultos.Value
                    : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@quien", recepcion.QuienRecibe ?? (object)DBNull.Value);

                // Construir novedades incluyendo si llegó o no
                string novedadesTexto = recepcion.LlegoCompleto
                    ? (string.IsNullOrWhiteSpace(recepcion.Novedades) ? "Sin novedades" : recepcion.Novedades)
                    : $"[PEDIDO NO RECIBIDO / INCOMPLETO] {recepcion.Novedades}";
                cmd.Parameters.AddWithValue("@novedades", novedadesTexto);

                cmd.ExecuteNonQuery();

                // 2. Actualizar estado de la compra según si llegó o no
                string nuevoEstado = recepcion.LlegoCompleto ? "RECIBIDO" : "NOVEDAD";
                string sqlUpdateCompra = "UPDATE compras SET estado = @estado WHERE id = @id";
                using var cmdUpdate = new MySqlCommand(sqlUpdateCompra, conn, transaction);
                cmdUpdate.Parameters.AddWithValue("@estado", nuevoEstado);
                cmdUpdate.Parameters.AddWithValue("@id", recepcion.CompraId);
                cmdUpdate.ExecuteNonQuery();

                transaction.Commit();

                TempData["Exito"] = recepcion.LlegoCompleto
                    ? $"Recepción del pedido #{recepcion.CompraId} registrada correctamente."
                    : $"Pedido #{recepcion.CompraId} marcado con novedad. Gerente notificado en el sistema.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al registrar la recepción: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ─────────────────────────────────────────────────────────────────
        // ELIMINAR RECEPCIÓN (solo gerente)
        // ─────────────────────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarRecepcion(int id, int compraId)
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            // Solo el gerente puede eliminar
            if (HttpContext.Session.GetString("Rol") != "GERENTE")
            {
                TempData["Error"] = "No tiene permisos para eliminar recepciones.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var transaction = conn.BeginTransaction();

                // Eliminar recepción
                using var cmd = new MySqlCommand("DELETE FROM recepciones_bodega WHERE id = @id", conn, transaction);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                // Revertir estado de la compra a PENDIENTE
                using var cmdUpdate = new MySqlCommand("UPDATE compras SET estado = 'PENDIENTE' WHERE id = @id", conn, transaction);
                cmdUpdate.Parameters.AddWithValue("@id", compraId);
                cmdUpdate.ExecuteNonQuery();

                transaction.Commit();
                TempData["Exito"] = "Recepción eliminada y pedido revertido a PENDIENTE.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar la recepción: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ MÉTODOS PRIVADOS DE ACCESO A DATOS
        // ═══════════════════════════════════════════════════════════════════

        private List<CompraPendiente> ObtenerPedidosPendientes()
        {
            var lista = new List<CompraPendiente>();
            try
            {
                using var conn = _conexion.ObtenerConexion();

                // Traer compras PENDIENTES y con NOVEDAD (por si quieren re-registrar)
                string sql = @"
                    SELECT c.id, p.nombre_empresa, c.fecha_entrega_provable, c.estado,
                           (SELECT COUNT(*) FROM recepciones_bodega rb WHERE rb.compra_id = c.id) AS tiene_recepcion,
                           (SELECT rb2.id FROM recepciones_bodega rb2 WHERE rb2.compra_id = c.id LIMIT 1) AS recepcion_id
                    FROM compras c
                    INNER JOIN proveedores p ON c.proveedor_id = p.id
                    WHERE c.estado IN ('PENDIENTE', 'NOVEDAD')
                    ORDER BY c.fecha_entrega_provable ASC, c.id DESC";

                using var cmd = new MySqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new CompraPendiente
                    {
                        Id = reader.GetInt32("id"),
                        NombreProveedor = reader.GetString("nombre_empresa"),
                        FechaEntregaProbable = reader.IsDBNull(reader.GetOrdinal("fecha_entrega_provable"))
                            ? null : reader.GetDateTime("fecha_entrega_provable"),
                        Estado = reader.GetString("estado"),
                        TieneRecepcion = reader.GetInt32("tiene_recepcion") > 0,
                        RecepcionId = reader.IsDBNull(reader.GetOrdinal("recepcion_id"))
                            ? null : reader.GetInt32("recepcion_id")
                    });
                }
                reader.Close();

                // Cargar detalles de cada compra pendiente
                foreach (var compra in lista)
                {
                    string sqlDet = @"
                        SELECT dc.id, dc.producto_id, pr.nombre, dc.cantidad
                        FROM detalle_compra dc
                        INNER JOIN productos pr ON dc.producto_id = pr.id
                        WHERE dc.compra_id = @cid";
                    using var cmdD = new MySqlCommand(sqlDet, conn);
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
            return lista;
        }

        private List<RecepcionBodega> ObtenerRecepciones()
        {
            var lista = new List<RecepcionBodega>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = @"
                    SELECT rb.id, rb.compra_id, rb.fecha_recibido, rb.hora, rb.factura,
                           rb.contenido, rb.cantidad_bultos, rb.quien_recibe, rb.novedades,
                           rb.fecha_registro, p.nombre_empresa, c.estado
                    FROM recepciones_bodega rb
                    INNER JOIN compras c ON rb.compra_id = c.id
                    INNER JOIN proveedores p ON c.proveedor_id = p.id
                    ORDER BY rb.fecha_registro DESC
                    LIMIT 50";

                using var cmd = new MySqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new RecepcionBodega
                    {
                        Id = reader.GetInt32("id"),
                        CompraId = reader.GetInt32("compra_id"),
                        NombreProveedor = reader.GetString("nombre_empresa"),
                        EstadoCompra = reader.GetString("estado"),
                        FechaRecibido = reader.IsDBNull(reader.GetOrdinal("fecha_recibido"))
                            ? null : reader.GetDateTime("fecha_recibido"),
                        Hora = reader.IsDBNull(reader.GetOrdinal("hora"))
                            ? null : reader.GetTimeSpan("hora"),
                        Factura = reader.IsDBNull(reader.GetOrdinal("factura"))
                            ? null : reader.GetString("factura"),
                        Contenido = reader.IsDBNull(reader.GetOrdinal("contenido"))
                            ? null : reader.GetString("contenido"),
                        CantidadBultos = reader.IsDBNull(reader.GetOrdinal("cantidad_bultos"))
                            ? null : reader.GetInt32("cantidad_bultos"),
                        QuienRecibe = reader.IsDBNull(reader.GetOrdinal("quien_recibe"))
                            ? null : reader.GetString("quien_recibe"),
                        Novedades = reader.IsDBNull(reader.GetOrdinal("novedades"))
                            ? null : reader.GetString("novedades"),
                        FechaRegistro = reader.IsDBNull(reader.GetOrdinal("fecha_registro"))
                            ? null : reader.GetDateTime("fecha_registro"),
                        LlegoCompleto = !reader.GetString("novedades").Contains("[PEDIDO NO RECIBIDO")
                    });
                }
            }
            catch { /* log */ }
            return lista;
        }
    }
}