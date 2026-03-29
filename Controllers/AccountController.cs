using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SEG_INV.Controllers.Models;
using Microsoft.AspNetCore.Http;

namespace SEG_INV.Controllers
{
    public class AccountController : Controller
    {
        private readonly ConexionBD _conexion;

        public AccountController(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
            _conexion = new ConexionBD(connectionString);
        }

        // ================================
        // GET: LOGIN
        // ================================
        [HttpGet]
        public IActionResult Login()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
            {
                string rol = HttpContext.Session.GetString("Rol") ?? "";
                return RedireccionPorRol(rol);
            }

            if (Request.Cookies.TryGetValue("UsuarioRecordado", out var usuarioRecordado))
                ViewBag.UsuarioRecordado = usuarioRecordado;

            return View("~/Views/Home/Index.cshtml");
        }

        // ================================
        // POST: LOGIN
        // ================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Login(string username, string password, bool remember = false)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                TempData["ErrorMessage"] = "Usuario y contraseña son requeridos.";
                return View("~/Views/Home/Index.cshtml");
            }

            try
            {
                using var conn = _conexion.ObtenerConexion();

                string query = @"
                    SELECT id, nombre, usuario, contrasena, rol
                    FROM usuarios
                    WHERE usuario = @usuario
                    LIMIT 1";

                using var cmd = new MySqlCommand(query, conn);
                cmd.Parameters.AddWithValue("@usuario", username);

                using var reader = cmd.ExecuteReader();

                if (!reader.Read())
                {
                    TempData["ErrorMessage"] = "Usuario no encontrado.";
                    return View("~/Views/Home/Index.cshtml");
                }

                string storedPassword = reader["contrasena"]?.ToString() ?? "";

                if (storedPassword != password)
                {
                    TempData["ErrorMessage"] = "Contraseña incorrecta.";
                    return View("~/Views/Home/Index.cshtml");
                }

                // ✅ LOGIN CORRECTO
                int userId = reader.GetInt32("id");
                string nombre = reader["nombre"]?.ToString() ?? "";
                string rol = reader["rol"]?.ToString() ?? "";

                HttpContext.Session.SetInt32("UsuarioId", userId);
                HttpContext.Session.SetString("Usuario", username);
                HttpContext.Session.SetString("Nombre", nombre);
                HttpContext.Session.SetString("Rol", rol);

                if (remember)
                {
                    Response.Cookies.Append("UsuarioRecordado", username, new CookieOptions
                    {
                        Expires = DateTimeOffset.Now.AddDays(7),
                        HttpOnly = true,
                        Secure = false,
                        SameSite = SameSiteMode.Lax
                    });
                }
                else
                {
                    Response.Cookies.Delete("UsuarioRecordado");
                }

                TempData["SuccessMessage"] = $"¡Bienvenido {nombre}!";

                return RedireccionPorRol(rol);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = "Error en la conexión: " + ex.Message;
                return View("~/Views/Home/Index.cshtml");
            }
        }

        // ================================
        // REDIRECCIÓN SEGÚN ROL
        // ================================
        private IActionResult RedireccionPorRol(string rol)
        {
            switch (rol)
            {
                case "GERENTE":
    return RedirectToAction("Gerente", "Home");


                case "BODEGA":
                    return RedirectToAction("Index", "Bodega");

                case "INVENTARIO":
                    return RedirectToAction("Index", "Inventario");
                case "ADMIN":
                     return RedirectToAction("Index", "Admin");

                default:
                    return RedirectToAction("Login");
            }
        }

        // ================================
        // LOGOUT
        // ================================
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete("UsuarioRecordado");
            return RedirectToAction("Login", "Account");
        }

        // ================================
        // VERIFICAR SESIÓN
        // ================================
        public bool IsLoggedIn()
        {
            return !string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario"));
        }

        // ================================
        // OBTENER USUARIO ACTUAL
        // ================================
        public (int? id, string usuario, string nombre, string rol) GetCurrentUser()
        {
            return (
                HttpContext.Session.GetInt32("UsuarioId"),
                HttpContext.Session.GetString("Usuario") ?? "",
                HttpContext.Session.GetString("Nombre") ?? "",
                HttpContext.Session.GetString("Rol") ?? ""
            );
        }
    }
}
