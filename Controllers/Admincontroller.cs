using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SEG_INV.Controllers.Models;
using SEG_INV.Models;

namespace SEG_INV.Controllers
{
    public class AdminController : Controller
    {
        private readonly ConexionBD _conexion;

        public AdminController(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
            _conexion = new ConexionBD(connectionString);
        }

        private bool EsAdmin() =>
            HttpContext.Session.GetString("Rol") == "ADMIN";

        private IActionResult? RedirigirSiNoAutorizado()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                return RedirectToAction("Login", "Account");
            if (!EsAdmin())
                return RedirectToAction("Login", "Account");
            return null;
        }

        public IActionResult Index()
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            var vm = new AdminViewModel
            {
                Usuarios     = ObtenerUsuarios(),
                Estadisticas = ObtenerEstadisticas(),
                Logs         = ObtenerLogs(20)
            };

            ViewBag.NombreUsuario = HttpContext.Session.GetString("Nombre");
            return View("~/Views/Home/Index_Admin.cshtml", vm);
        }

        // ══════════════════════════════════════════════════
        // CORRECCIÓN: parámetros sueltos en lugar de objeto
        // ══════════════════════════════════════════════════

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CrearUsuario(
            string Nombre,
            string Usuario,
            string Contrasena,
            string Rol,
            string Activo)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (string.IsNullOrWhiteSpace(Nombre)     ||
                string.IsNullOrWhiteSpace(Usuario)    ||
                string.IsNullOrWhiteSpace(Contrasena) ||
                string.IsNullOrWhiteSpace(Rol))
            {
                TempData["Error"] = "Todos los campos son requeridos para crear un usuario.";
                return RedirectToAction("Index");
            }

            // Parseo robusto: acepta "true", "True", "1"
            bool activo = Activo == "true" || Activo == "True" || Activo == "1";

            if (ExisteUsuario(Usuario.Trim()))
            {
                TempData["Error"] = $"El usuario '{Usuario}' ya existe.";
                return RedirectToAction("Index");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();
                const string sql = @"INSERT INTO usuarios (nombre, usuario, contrasena, rol, activo)
                                     VALUES (@nombre, @usuario, @contrasena, @rol, @activo)";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre",     Nombre.Trim());
                cmd.Parameters.AddWithValue("@usuario",    Usuario.Trim());
                cmd.Parameters.AddWithValue("@contrasena", Contrasena);
                cmd.Parameters.AddWithValue("@rol",        Rol);
                cmd.Parameters.AddWithValue("@activo",     activo ? 1 : 0);
                cmd.ExecuteNonQuery();

                RegistrarLog("CREAR", "Usuario", null, $"Usuario '{Usuario}' ({Rol}) creado.");
                TempData["Exito"] = $"Usuario '{Nombre}' creado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al crear usuario: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EditarUsuario(
            int    Id,
            string Nombre,
            string Usuario,
            string? Contrasena,
            string Rol,
            string Activo)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (Id <= 0                            ||
                string.IsNullOrWhiteSpace(Nombre)  ||
                string.IsNullOrWhiteSpace(Usuario) ||
                string.IsNullOrWhiteSpace(Rol))
            {
                TempData["Error"] = "Datos del usuario inválidos.";
                return RedirectToAction("Index");
            }

            bool activo = Activo == "true" || Activo == "True" || Activo == "1";

            try
            {
                using var conn = _conexion.ObtenerConexion();

                string sql = !string.IsNullOrWhiteSpace(Contrasena)
                    ? @"UPDATE usuarios
                        SET nombre=@nombre, usuario=@usuario, contrasena=@contrasena,
                            rol=@rol, activo=@activo
                        WHERE id=@id"
                    : @"UPDATE usuarios
                        SET nombre=@nombre, usuario=@usuario, rol=@rol, activo=@activo
                        WHERE id=@id";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@nombre",  Nombre.Trim());
                cmd.Parameters.AddWithValue("@usuario", Usuario.Trim());
                cmd.Parameters.AddWithValue("@rol",     Rol);
                cmd.Parameters.AddWithValue("@activo",  activo ? 1 : 0);
                cmd.Parameters.AddWithValue("@id",      Id);

                if (!string.IsNullOrWhiteSpace(Contrasena))
                    cmd.Parameters.AddWithValue("@contrasena", Contrasena);

                int rows = cmd.ExecuteNonQuery();

                if (rows == 0)
                {
                    TempData["Error"] = "No se encontró el usuario a actualizar.";
                    return RedirectToAction("Index");
                }

                RegistrarLog("EDITAR", "Usuario", Id, $"Usuario '{Usuario}' actualizado.");
                TempData["Exito"] = "Usuario actualizado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al actualizar usuario: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult EliminarUsuario(int id)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            if (HttpContext.Session.GetInt32("UsuarioId") == id)
            {
                TempData["Error"] = "No puedes eliminar tu propia cuenta.";
                return RedirectToAction("Index");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();

                string? nombreUsuario;
                using (var cmdQ = new MySqlCommand("SELECT usuario FROM usuarios WHERE id=@id", conn))
                {
                    cmdQ.Parameters.AddWithValue("@id", id);
                    nombreUsuario = cmdQ.ExecuteScalar()?.ToString();
                }

                if (nombreUsuario == null)
                {
                    TempData["Error"] = "Usuario no encontrado.";
                    return RedirectToAction("Index");
                }

                using var cmd = new MySqlCommand("DELETE FROM usuarios WHERE id=@id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                RegistrarLog("ELIMINAR", "Usuario", id, $"Usuario '{nombreUsuario}' eliminado.");
                TempData["Exito"] = $"Usuario '{nombreUsuario}' eliminado correctamente.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al eliminar usuario: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ToggleActivoUsuario(int id)
        {
            if (RedirigirSiNoAutorizado() is IActionResult r) return r;

            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd  = new MySqlCommand(
                    "UPDATE usuarios SET activo = IF(activo = 1, 0, 1) WHERE id = @id", conn);
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                RegistrarLog("EDITAR", "Usuario", id, "Estado activo/inactivo alternado.");
                TempData["Exito"] = "Estado del usuario actualizado.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Error al cambiar estado: " + ex.Message;
            }

            return RedirectToAction("Index");
        }

        // ══════════════════════════════════════════════════
        // MÉTODOS PRIVADOS
        // ══════════════════════════════════════════════════

        private List<UsuarioAdmin> ObtenerUsuarios()
        {
            var lista = new List<UsuarioAdmin>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                const string sql = @"SELECT id, nombre, usuario, rol, activo, fecha_creacion
                                     FROM usuarios ORDER BY rol, nombre";
                using var cmd    = new MySqlCommand(sql, conn);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(new UsuarioAdmin
                    {
                        Id            = reader.GetInt32("id"),
                        Nombre        = reader.GetString("nombre"),
                        Usuario       = reader.GetString("usuario"),
                        Rol           = reader.GetString("rol"),
                        Activo        = reader.GetInt32("activo") == 1,
                        FechaCreacion = reader.IsDBNull(reader.GetOrdinal("fecha_creacion"))
                                        ? null : reader.GetDateTime("fecha_creacion")
                    });
            }
            catch { }
            return lista;
        }

        private AdminEstadisticas ObtenerEstadisticas()
        {
            var stats = new AdminEstadisticas();
            try
            {
                using var conn = _conexion.ObtenerConexion();

                using (var cmd = new MySqlCommand(
                    "SELECT COUNT(*) AS total, SUM(activo=1) AS activos, SUM(activo=0) AS inactivos FROM usuarios", conn))
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        stats.TotalUsuarios     = r.GetInt32("total");
                        stats.UsuariosActivos   = r.IsDBNull(r.GetOrdinal("activos"))   ? 0 : Convert.ToInt32(r["activos"]);
                        stats.UsuariosInactivos = r.IsDBNull(r.GetOrdinal("inactivos")) ? 0 : Convert.ToInt32(r["inactivos"]);
                    }

                using (var cmd = new MySqlCommand(
                    @"SELECT COUNT(*) AS total,
                             SUM(estado='PENDIENTE') AS pendientes,
                             SUM(estado='RECIBIDO')  AS recibidas,
                             SUM(estado='NOVEDAD')   AS novedad
                      FROM compras", conn))
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        stats.TotalCompras      = r.GetInt32("total");
                        stats.ComprasPendientes = r.IsDBNull(r.GetOrdinal("pendientes")) ? 0 : Convert.ToInt32(r["pendientes"]);
                        stats.ComprasRecibidas  = r.IsDBNull(r.GetOrdinal("recibidas"))  ? 0 : Convert.ToInt32(r["recibidas"]);
                        stats.ComprasNovedad    = r.IsDBNull(r.GetOrdinal("novedad"))    ? 0 : Convert.ToInt32(r["novedad"]);
                    }

                using (var cmd = new MySqlCommand(
                    @"SELECT COUNT(*) AS total,
                             SUM(inventario_actual = 0) AS sin_stock,
                             SUM(inventario_actual > 0 AND inventario_actual <= 5) AS criticos
                      FROM productos", conn))
                using (var r = cmd.ExecuteReader())
                    if (r.Read())
                    {
                        stats.TotalProductos    = r.GetInt32("total");
                        stats.ProductosSinStock = r.IsDBNull(r.GetOrdinal("sin_stock")) ? 0 : Convert.ToInt32(r["sin_stock"]);
                        stats.ProductosCriticos = r.IsDBNull(r.GetOrdinal("criticos"))  ? 0 : Convert.ToInt32(r["criticos"]);
                    }

                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM proveedores", conn))
                    stats.TotalProveedores = Convert.ToInt32(cmd.ExecuteScalar());

                using (var cmd = new MySqlCommand("SELECT COUNT(*) FROM recepciones_bodega", conn))
                    stats.TotalRecepciones = Convert.ToInt32(cmd.ExecuteScalar());
            }
            catch { }
            return stats;
        }

        private List<LogActividad> ObtenerLogs(int limite = 20)
        {
            var lista = new List<LogActividad>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                const string sql = @"SELECT id, accion, entidad, entidad_id, descripcion,
                                            realizado_por, fecha_hora
                                     FROM log_actividad
                                     ORDER BY fecha_hora DESC
                                     LIMIT @limite";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@limite", limite);
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                    lista.Add(new LogActividad
                    {
                        Id           = reader.GetInt32("id"),
                        Accion       = reader.GetString("accion"),
                        Entidad      = reader.GetString("entidad"),
                        EntidadId    = reader.IsDBNull(reader.GetOrdinal("entidad_id")) ? null : reader.GetInt32("entidad_id"),
                        Descripcion  = reader.GetString("descripcion"),
                        RealizadoPor = reader.GetString("realizado_por"),
                        FechaHora    = reader.GetDateTime("fecha_hora")
                    });
            }
            catch { }
            return lista;
        }

        private void RegistrarLog(string accion, string entidad, int? entidadId, string descripcion)
        {
            try
            {
                string realizadoPor = HttpContext.Session.GetString("Usuario") ?? "sistema";
                using var conn = _conexion.ObtenerConexion();
                const string sql = @"INSERT INTO log_actividad
                                         (accion, entidad, entidad_id, descripcion, realizado_por, fecha_hora)
                                     VALUES (@accion, @entidad, @eid, @desc, @por, NOW())";
                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@accion",  accion);
                cmd.Parameters.AddWithValue("@entidad", entidad);
                cmd.Parameters.AddWithValue("@eid",     entidadId.HasValue ? (object)entidadId.Value : DBNull.Value);
                cmd.Parameters.AddWithValue("@desc",    descripcion);
                cmd.Parameters.AddWithValue("@por",     realizadoPor);
                cmd.ExecuteNonQuery();
            }
            catch { }
        }

        private bool ExisteUsuario(string nombreUsuario, int excluirId = 0)
        {
            try
            {
                using var conn = _conexion.ObtenerConexion();
                using var cmd  = new MySqlCommand(
                    "SELECT COUNT(*) FROM usuarios WHERE usuario=@u AND id <> @id", conn);
                cmd.Parameters.AddWithValue("@u",  nombreUsuario);
                cmd.Parameters.AddWithValue("@id", excluirId);
                return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
            }
            catch { return false; }
        }
    }
}