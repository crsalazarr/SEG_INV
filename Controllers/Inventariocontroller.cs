using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;
using SEG_INV.Controllers.Models;
using SEG_INV.Models;

namespace SEG_INV.Controllers
{
    public class InventarioController : Controller
    {
        private readonly ConexionBD _conexion;

        public InventarioController(IConfiguration configuration)
        {
            string connectionString = configuration.GetConnectionString("MySQLConnection")
                ?? throw new InvalidOperationException("Connection string 'MySQLConnection' not found.");
            _conexion = new ConexionBD(connectionString);
        }

        // ─────────────────────────────────────────────────────────────────
        // VERIFICAR SESIÓN Y ROL
        // ─────────────────────────────────────────────────────────────────
        private IActionResult? RedirigirSiNoAutorizado()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("Usuario")))
                return RedirectToAction("Login", "Account");

            string? rol = HttpContext.Session.GetString("Rol");
            if (rol != "INVENTARIO" && rol != "GERENTE")
                return RedirectToAction("Login", "Account");

            return null;
        }

        // ─────────────────────────────────────────────────────────────────
        // INDEX - DASHBOARD DE INVENTARIO
        // ─────────────────────────────────────────────────────────────────
        public IActionResult Index()
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            var vm = new InventarioViewModel
            {
                Productos         = ObtenerProductosConEstado(),
                PedidosPendientes = ObtenerPedidosPendientes(),
                PedidosRecibidos  = ObtenerPedidosRecibidos(),
                PedidosNovedad    = ObtenerPedidosNovedad()
            };

            vm.TotalPendientes = vm.PedidosPendientes.Count;
            vm.TotalRecibidos  = vm.PedidosRecibidos.Count;
            vm.TotalNovedad    = vm.PedidosNovedad.Count;
            vm.TotalProductos  = vm.Productos.Count;

            ViewBag.NombreUsuario = HttpContext.Session.GetString("Nombre");
            return View("~/Views/Home/Index_Inventario.cshtml", vm);
        }

        // ─────────────────────────────────────────────────────────────────
        // EXPORTAR A EXCEL
        // ─────────────────────────────────────────────────────────────────
        public IActionResult ExportarExcel()
        {
            var redirect = RedirigirSiNoAutorizado();
            if (redirect != null) return redirect;

            var productos         = ObtenerProductosConEstado();
            var pedidosPendientes = ObtenerPedidosPendientes();
            var pedidosRecibidos  = ObtenerPedidosRecibidos();
            var pedidosNovedad    = ObtenerPedidosNovedad();

            using var wb = new XLWorkbook();

            // ── Colores ──────────────────────────────────────────────────
            var azulOscuro = XLColor.FromHtml("#1E3A5F");
            var azulMedio  = XLColor.FromHtml("#2E6DA4");
            var azulClaro  = XLColor.FromHtml("#D6E4F0");
            var verdOscuro = XLColor.FromHtml("#1E5C2E");
            var verdMedio  = XLColor.FromHtml("#27AE60");
            var verdClaro  = XLColor.FromHtml("#D5F5E3");
            var ambarOsc   = XLColor.FromHtml("#7D5A00");
            var ambarMed   = XLColor.FromHtml("#D4A017");
            var ambarClar  = XLColor.FromHtml("#FEF9E7");
            var moradoOsc  = XLColor.FromHtml("#4A0080");
            var moradoMed  = XLColor.FromHtml("#8E44AD");
            var moradoClar = XLColor.FromHtml("#F5EEF8");
            var rojoClar   = XLColor.FromHtml("#FDEDEC");
            var rojoFont   = XLColor.FromHtml("#C0392B");
            var grisClar   = XLColor.FromHtml("#F2F4F4");
            var blanco     = XLColor.White;

            // ════════════════════════════════════════════════════════════
            // HOJA 1 — POR PRODUCTO
            // ════════════════════════════════════════════════════════════
            var ws1 = wb.Worksheets.Add("Por Producto");

            // Título
            ws1.Range("A1:H1").Merge();
            ws1.Cell("A1").Value = "SEGUIMIENTO DE INVENTARIO — Estado por Producto";
            ws1.Cell("A1").Style.Font.Bold = true;
            ws1.Cell("A1").Style.Font.FontSize = 14;
            ws1.Cell("A1").Style.Font.FontColor = blanco;
            ws1.Cell("A1").Style.Fill.BackgroundColor = azulOscuro;
            ws1.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws1.Cell("A1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws1.Row(1).Height = 28;

            // Subtítulo
            ws1.Range("A2:H2").Merge();
            ws1.Cell("A2").Value = $"Generado el: {DateTime.Now:dd/MM/yyyy  HH:mm} hs";
            ws1.Cell("A2").Style.Font.Italic = true;
            ws1.Cell("A2").Style.Font.FontColor = XLColor.FromHtml("#555555");
            ws1.Cell("A2").Style.Fill.BackgroundColor = XLColor.FromHtml("#EBF5FB");
            ws1.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Encabezados
            string[] enc1 = { "Producto", "Stock Actual", "Unid. Recibidas",
                               "Unid. Pendientes", "Total Pedidos", "% Recibido",
                               "Pedidos Activos", "Estado" };
            for (int i = 0; i < enc1.Length; i++)
            {
                var c = ws1.Cell(4, i + 1);
                c.Value = enc1[i];
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = blanco;
                c.Style.Fill.BackgroundColor = azulMedio;
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = azulOscuro;
            }
            ws1.Row(4).Height = 22;

            int fila = 5;
            foreach (var prod in productos)
            {
                int total = prod.UnidadesRecibidas + prod.UnidadesPendientes;
                double pct = total > 0 ? (double)prod.UnidadesRecibidas / total * 100 : 0;

                ws1.Cell(fila, 1).Value = prod.NombreProducto;
                ws1.Cell(fila, 2).Value = prod.InventarioActual;
                ws1.Cell(fila, 3).Value = prod.UnidadesRecibidas;
                ws1.Cell(fila, 4).Value = prod.UnidadesPendientes;
                ws1.Cell(fila, 5).Value = total;
                ws1.Cell(fila, 6).Value = Math.Round(pct, 1);
                ws1.Cell(fila, 6).Style.NumberFormat.Format = "0.0\"%\"";
                ws1.Cell(fila, 7).Value = prod.PedidosActivos.Count;
                ws1.Cell(fila, 8).Value = prod.UnidadesPendientes > 0 ? "Con pendientes" : "Al día";

                // Alineaciones numéricas
                for (int col = 2; col <= 7; col++)
                    ws1.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Fondo alternado
                var bg = (fila % 2 == 0) ? grisClar : blanco;
                ws1.Range(fila, 1, fila, 8).Style.Fill.BackgroundColor = bg;

                // Color estado
                if (prod.UnidadesPendientes > 0)
                {
                    ws1.Cell(fila, 4).Style.Font.FontColor = ambarMed;
                    ws1.Cell(fila, 4).Style.Font.Bold = true;
                    ws1.Cell(fila, 8).Style.Font.FontColor = ambarOsc;
                    ws1.Cell(fila, 8).Style.Font.Bold = true;
                }
                else
                {
                    ws1.Cell(fila, 8).Style.Font.FontColor = verdOscuro;
                }

                // Borde inferior
                ws1.Range(fila, 1, fila, 8).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                ws1.Range(fila, 1, fila, 8).Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");

                fila++;
            }

            // Fila de totales
            ws1.Range(fila, 1, fila, 8).Style.Fill.BackgroundColor = azulOscuro;
            ws1.Range(fila, 1, fila, 8).Style.Font.FontColor = blanco;
            ws1.Range(fila, 1, fila, 8).Style.Font.Bold = true;
            ws1.Cell(fila, 1).Value = "TOTALES";
            ws1.Cell(fila, 2).FormulaA1 = $"=SUM(B5:B{fila - 1})";
            ws1.Cell(fila, 3).FormulaA1 = $"=SUM(C5:C{fila - 1})";
            ws1.Cell(fila, 4).FormulaA1 = $"=SUM(D5:D{fila - 1})";
            ws1.Cell(fila, 5).FormulaA1 = $"=SUM(E5:E{fila - 1})";
            ws1.Cell(fila, 7).FormulaA1 = $"=SUM(G5:G{fila - 1})";
            ws1.Row(fila).Height = 22;
            for (int col = 2; col <= 7; col++)
                ws1.Cell(fila, col).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws1.Columns().AdjustToContents();
            ws1.SheetView.FreezeRows(4);

            // ════════════════════════════════════════════════════════════
            // HOJA 2 — PEDIDOS PENDIENTES
            // ════════════════════════════════════════════════════════════
            var ws2 = wb.Worksheets.Add("Pendientes");

            ws2.Range("A1:G1").Merge();
            ws2.Cell("A1").Value = "⏳  PEDIDOS PENDIENTES POR LLEGAR";
            ws2.Cell("A1").Style.Font.Bold = true;
            ws2.Cell("A1").Style.Font.FontSize = 14;
            ws2.Cell("A1").Style.Font.FontColor = blanco;
            ws2.Cell("A1").Style.Fill.BackgroundColor = ambarMed;
            ws2.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws2.Cell("A1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws2.Row(1).Height = 28;

            ws2.Range("A2:G2").Merge();
            ws2.Cell("A2").Value = $"Total pendientes: {pedidosPendientes.Count}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm} hs";
            ws2.Cell("A2").Style.Font.Italic = true;
            ws2.Cell("A2").Style.Fill.BackgroundColor = ambarClar;
            ws2.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            string[] enc2 = { "# Pedido", "Proveedor", "Fecha Entrega Estimada",
                               "Urgencia", "Productos", "Cantidades", "Estado" };
            for (int i = 0; i < enc2.Length; i++)
            {
                var c = ws2.Cell(4, i + 1);
                c.Value = enc2[i];
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = blanco;
                c.Style.Fill.BackgroundColor = ambarMed;
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = ambarOsc;
            }
            ws2.Row(4).Height = 22;

            fila = 5;
            foreach (var p in pedidosPendientes)
            {
                bool esVencido = p.FechaEntregaProbable.HasValue
                                 && p.FechaEntregaProbable.Value.Date < DateTime.Today;
                bool esHoy     = p.FechaEntregaProbable.HasValue
                                 && p.FechaEntregaProbable.Value.Date == DateTime.Today;

                string urgencia = esVencido ? "VENCIDO" :
                                  esHoy     ? "HOY" :
                                  p.FechaEntregaProbable.HasValue
                                      ? $"en {(p.FechaEntregaProbable.Value.Date - DateTime.Today).Days} día(s)"
                                      : "Sin fecha";

                ws2.Cell(fila, 1).Value = p.Id;
                ws2.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws2.Cell(fila, 2).Value = p.NombreProveedor;
                ws2.Cell(fila, 3).Value = p.FechaEntregaProbable.HasValue
                                           ? p.FechaEntregaProbable.Value.ToString("dd/MM/yyyy")
                                           : "Sin fecha";
                ws2.Cell(fila, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws2.Cell(fila, 4).Value = urgencia;
                ws2.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws2.Cell(fila, 5).Value = string.Join("\n", p.Detalles.Select(d => d.NombreProducto));
                ws2.Cell(fila, 6).Value = string.Join("\n", p.Detalles.Select(d => d.Cantidad.ToString()));
                ws2.Cell(fila, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws2.Cell(fila, 7).Value = "PENDIENTE";
                ws2.Cell(fila, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws2.Cell(fila, 7).Style.Font.FontColor = ambarOsc;
                ws2.Cell(fila, 7).Style.Font.Bold = true;

                if (esVencido)
                {
                    ws2.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor = rojoClar;
                    ws2.Cell(fila, 4).Style.Font.FontColor = rojoFont;
                    ws2.Cell(fila, 4).Style.Font.Bold = true;
                }
                else if (esHoy)
                {
                    ws2.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor = ambarClar;
                    ws2.Cell(fila, 4).Style.Font.FontColor = ambarOsc;
                    ws2.Cell(fila, 4).Style.Font.Bold = true;
                }
                else
                {
                    ws2.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor =
                        (fila % 2 == 0) ? grisClar : blanco;
                }

                ws2.Row(fila).Style.Alignment.WrapText = true;
                ws2.Range(fila, 1, fila, 7).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                ws2.Range(fila, 1, fila, 7).Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");

                fila++;
            }

            ws2.Columns().AdjustToContents();
            ws2.SheetView.FreezeRows(4);

            // ════════════════════════════════════════════════════════════
            // HOJA 3 — PEDIDOS RECIBIDOS
            // ════════════════════════════════════════════════════════════
            var ws3 = wb.Worksheets.Add("Recibidos");

            ws3.Range("A1:I1").Merge();
            ws3.Cell("A1").Value = "✅  PEDIDOS RECIBIDOS";
            ws3.Cell("A1").Style.Font.Bold = true;
            ws3.Cell("A1").Style.Font.FontSize = 14;
            ws3.Cell("A1").Style.Font.FontColor = blanco;
            ws3.Cell("A1").Style.Fill.BackgroundColor = verdOscuro;
            ws3.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws3.Cell("A1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws3.Row(1).Height = 28;

            ws3.Range("A2:I2").Merge();
            ws3.Cell("A2").Value = $"Total recibidos: {pedidosRecibidos.Count}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm} hs";
            ws3.Cell("A2").Style.Font.Italic = true;
            ws3.Cell("A2").Style.Fill.BackgroundColor = verdClaro;
            ws3.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            string[] enc3 = { "# Compra", "Proveedor", "Fecha Recibido", "Hora",
                               "Factura", "Bultos", "Recibió", "Productos", "Observaciones" };
            for (int i = 0; i < enc3.Length; i++)
            {
                var c = ws3.Cell(4, i + 1);
                c.Value = enc3[i];
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = blanco;
                c.Style.Fill.BackgroundColor = verdMedio;
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = verdOscuro;
            }
            ws3.Row(4).Height = 22;

            fila = 5;
            foreach (var rec in pedidosRecibidos)
            {
                ws3.Cell(fila, 1).Value = rec.CompraId;
                ws3.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws3.Cell(fila, 2).Value = rec.NombreProveedor;
                ws3.Cell(fila, 3).Value = rec.FechaRecibido.HasValue
                                           ? rec.FechaRecibido.Value.ToString("dd/MM/yyyy") : "—";
                ws3.Cell(fila, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws3.Cell(fila, 4).Value = rec.Hora.HasValue
                                           ? rec.Hora.Value.ToString(@"hh\:mm") : "—";
                ws3.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws3.Cell(fila, 5).Value = rec.Factura ?? "—";
                ws3.Cell(fila, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws3.Cell(fila, 6).Value = rec.CantidadBultos.HasValue
                                           ? rec.CantidadBultos.Value.ToString() : "—";
                ws3.Cell(fila, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws3.Cell(fila, 7).Value = rec.QuienRecibe ?? "—";
                ws3.Cell(fila, 8).Value = string.Join("\n",
                    rec.Productos.Select(p => $"{p.NombreProducto}  x{p.Cantidad}"));
                ws3.Cell(fila, 9).Value = (rec.Novedades == null || rec.Novedades == "Sin novedades")
                                           ? "—" : rec.Novedades;

                ws3.Range(fila, 1, fila, 9).Style.Fill.BackgroundColor =
                    (fila % 2 == 0) ? verdClaro : blanco;
                ws3.Row(fila).Style.Alignment.WrapText = true;
                ws3.Range(fila, 1, fila, 9).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                ws3.Range(fila, 1, fila, 9).Style.Border.BottomBorderColor = XLColor.FromHtml("#CCCCCC");

                fila++;
            }

            // Total recibidos
            ws3.Range(fila, 1, fila, 9).Style.Fill.BackgroundColor = verdOscuro;
            ws3.Range(fila, 1, fila, 9).Style.Font.FontColor = blanco;
            ws3.Range(fila, 1, fila, 9).Style.Font.Bold = true;
            ws3.Cell(fila, 1).Value = $"TOTAL: {pedidosRecibidos.Count} recepciones";
            ws3.Row(fila).Height = 22;

            ws3.Columns().AdjustToContents();
            ws3.SheetView.FreezeRows(4);

            // ════════════════════════════════════════════════════════════
            // HOJA 4 — NOVEDADES
            // ════════════════════════════════════════════════════════════
            var ws4 = wb.Worksheets.Add("Novedades");

            ws4.Range("A1:G1").Merge();
            ws4.Cell("A1").Value = "⚠️  PEDIDOS CON NOVEDAD";
            ws4.Cell("A1").Style.Font.Bold = true;
            ws4.Cell("A1").Style.Font.FontSize = 14;
            ws4.Cell("A1").Style.Font.FontColor = blanco;
            ws4.Cell("A1").Style.Fill.BackgroundColor = moradoOsc;
            ws4.Cell("A1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws4.Cell("A1").Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws4.Row(1).Height = 28;

            ws4.Range("A2:G2").Merge();
            ws4.Cell("A2").Value = $"Total novedades: {pedidosNovedad.Count}  |  Generado: {DateTime.Now:dd/MM/yyyy HH:mm} hs";
            ws4.Cell("A2").Style.Font.Italic = true;
            ws4.Cell("A2").Style.Fill.BackgroundColor = moradoClar;
            ws4.Cell("A2").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            string[] enc4 = { "# Compra", "Proveedor", "Fecha Recibido",
                               "Factura", "Productos", "Descripción Novedad", "Reportó" };
            for (int i = 0; i < enc4.Length; i++)
            {
                var c = ws4.Cell(4, i + 1);
                c.Value = enc4[i];
                c.Style.Font.Bold = true;
                c.Style.Font.FontColor = blanco;
                c.Style.Fill.BackgroundColor = moradoMed;
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = moradoOsc;
            }
            ws4.Row(4).Height = 22;

            fila = 5;
            foreach (var rec in pedidosNovedad)
            {
                string novedad = rec.Novedades?
                    .Replace("[PEDIDO NO RECIBIDO / INCOMPLETO] ", "") ?? "—";

                ws4.Cell(fila, 1).Value = rec.CompraId;
                ws4.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws4.Cell(fila, 2).Value = rec.NombreProveedor;
                ws4.Cell(fila, 3).Value = rec.FechaRecibido.HasValue
                                           ? rec.FechaRecibido.Value.ToString("dd/MM/yyyy") : "—";
                ws4.Cell(fila, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws4.Cell(fila, 4).Value = rec.Factura ?? "—";
                ws4.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws4.Cell(fila, 5).Value = string.Join("\n",
                    rec.Productos.Select(p => $"{p.NombreProducto}  x{p.Cantidad}"));
                ws4.Cell(fila, 6).Value = novedad;
                ws4.Cell(fila, 6).Style.Font.FontColor = moradoOsc;
                ws4.Cell(fila, 6).Style.Font.Bold = true;
                ws4.Cell(fila, 7).Value = rec.QuienRecibe ?? "—";

                ws4.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor = moradoClar;
                ws4.Row(fila).Style.Alignment.WrapText = true;
                ws4.Range(fila, 1, fila, 7).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
                ws4.Range(fila, 1, fila, 7).Style.Border.BottomBorderColor = XLColor.FromHtml("#D7BDE2");

                fila++;
            }

            // Total novedades
            ws4.Range(fila, 1, fila, 7).Style.Fill.BackgroundColor = moradoOsc;
            ws4.Range(fila, 1, fila, 7).Style.Font.FontColor = blanco;
            ws4.Range(fila, 1, fila, 7).Style.Font.Bold = true;
            ws4.Cell(fila, 1).Value = $"TOTAL: {pedidosNovedad.Count} novedad(es)";
            ws4.Row(fila).Height = 22;

            ws4.Columns().AdjustToContents();
            ws4.SheetView.FreezeRows(4);

            // ── Retornar el archivo ──────────────────────────────────────
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"Inventario_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
            return File(
                stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName
            );
        }

        // ═══════════════════════════════════════════════════════════════════
        // ██ MÉTODOS DE ACCESO A DATOS
        // ═══════════════════════════════════════════════════════════════════

        private List<ProductoInventario> ObtenerProductosConEstado()
        {
            var lista = new List<ProductoInventario>();
            try
            {
                using var conn = _conexion.ObtenerConexion();

                string sqlProd = "SELECT id, nombre, inventario_actual FROM productos ORDER BY nombre";
                using var cmdP = new MySqlCommand(sqlProd, conn);
                using var rdrP = cmdP.ExecuteReader();
                while (rdrP.Read())
                {
                    lista.Add(new ProductoInventario
                    {
                        ProductoId       = rdrP.GetInt32("id"),
                        NombreProducto   = rdrP.GetString("nombre"),
                        InventarioActual = rdrP.IsDBNull(rdrP.GetOrdinal("inventario_actual"))
                                            ? 0 : rdrP.GetInt32("inventario_actual")
                    });
                }
                rdrP.Close();

                foreach (var prod in lista)
                {
                    string sqlRec = @"
                        SELECT COALESCE(SUM(dc.cantidad), 0) AS total
                        FROM detalle_compra dc
                        INNER JOIN compras c ON dc.compra_id = c.id
                        WHERE dc.producto_id = @pid AND c.estado = 'RECIBIDO'";
                    using var cmdR = new MySqlCommand(sqlRec, conn);
                    cmdR.Parameters.AddWithValue("@pid", prod.ProductoId);
                    prod.UnidadesRecibidas = Convert.ToInt32(cmdR.ExecuteScalar());

                    string sqlPend = @"
                        SELECT COALESCE(SUM(dc.cantidad), 0) AS total
                        FROM detalle_compra dc
                        INNER JOIN compras c ON dc.compra_id = c.id
                        WHERE dc.producto_id = @pid AND c.estado = 'PENDIENTE'";
                    using var cmdPend = new MySqlCommand(sqlPend, conn);
                    cmdPend.Parameters.AddWithValue("@pid", prod.ProductoId);
                    prod.UnidadesPendientes = Convert.ToInt32(cmdPend.ExecuteScalar());

                    string sqlDet = @"
                        SELECT c.id, p.nombre_empresa, dc.cantidad, c.estado,
                               c.fecha_entrega_provable, rb.fecha_recibido,
                               rb.factura, rb.quien_recibe, rb.novedades
                        FROM detalle_compra dc
                        INNER JOIN compras c ON dc.compra_id = c.id
                        INNER JOIN proveedores p ON c.proveedor_id = p.id
                        LEFT JOIN recepciones_bodega rb ON rb.compra_id = c.id
                        WHERE dc.producto_id = @pid
                        ORDER BY c.fecha_creacion DESC";
                    using var cmdDet = new MySqlCommand(sqlDet, conn);
                    cmdDet.Parameters.AddWithValue("@pid", prod.ProductoId);
                    using var rdrDet = cmdDet.ExecuteReader();
                    while (rdrDet.Read())
                    {
                        prod.PedidosActivos.Add(new PedidoPorProducto
                        {
                            CompraId             = rdrDet.GetInt32("id"),
                            NombreProveedor      = rdrDet.GetString("nombre_empresa"),
                            Cantidad             = rdrDet.GetInt32("cantidad"),
                            Estado               = rdrDet.GetString("estado"),
                            FechaEntregaProbable = rdrDet.IsDBNull(rdrDet.GetOrdinal("fecha_entrega_provable"))
                                                    ? null : rdrDet.GetDateTime("fecha_entrega_provable"),
                            FechaRecibido        = rdrDet.IsDBNull(rdrDet.GetOrdinal("fecha_recibido"))
                                                    ? null : rdrDet.GetDateTime("fecha_recibido"),
                            Factura              = rdrDet.IsDBNull(rdrDet.GetOrdinal("factura"))
                                                    ? null : rdrDet.GetString("factura"),
                            QuienRecibio         = rdrDet.IsDBNull(rdrDet.GetOrdinal("quien_recibe"))
                                                    ? null : rdrDet.GetString("quien_recibe"),
                            Novedades            = rdrDet.IsDBNull(rdrDet.GetOrdinal("novedades"))
                                                    ? null : rdrDet.GetString("novedades")
                        });
                    }
                    rdrDet.Close();
                }
            }
            catch { /* log */ }
            return lista;
        }

        private List<CompraPendiente> ObtenerPedidosPendientes()
        {
            var lista = new List<CompraPendiente>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = @"
                    SELECT c.id, p.nombre_empresa, c.fecha_entrega_provable, c.estado
                    FROM compras c
                    INNER JOIN proveedores p ON c.proveedor_id = p.id
                    WHERE c.estado = 'PENDIENTE'
                    ORDER BY c.fecha_entrega_provable ASC";
                using var cmd = new MySqlCommand(sql, conn);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    lista.Add(new CompraPendiente
                    {
                        Id                   = rdr.GetInt32("id"),
                        NombreProveedor      = rdr.GetString("nombre_empresa"),
                        FechaEntregaProbable = rdr.IsDBNull(rdr.GetOrdinal("fecha_entrega_provable"))
                                               ? null : rdr.GetDateTime("fecha_entrega_provable"),
                        Estado               = rdr.GetString("estado")
                    });
                }
                rdr.Close();

                foreach (var compra in lista)
                {
                    string sqlDet = @"
                        SELECT dc.producto_id, pr.nombre, dc.cantidad
                        FROM detalle_compra dc
                        INNER JOIN productos pr ON dc.producto_id = pr.id
                        WHERE dc.compra_id = @cid";
                    using var cmdD = new MySqlCommand(sqlDet, conn);
                    cmdD.Parameters.AddWithValue("@cid", compra.Id);
                    using var rdrD = cmdD.ExecuteReader();
                    while (rdrD.Read())
                    {
                        compra.Detalles.Add(new DetalleCompra
                        {
                            CompraId       = compra.Id,
                            ProductoId     = rdrD.GetInt32("producto_id"),
                            NombreProducto = rdrD.GetString("nombre"),
                            Cantidad       = rdrD.GetInt32("cantidad")
                        });
                    }
                    rdrD.Close();
                }
            }
            catch { /* log */ }
            return lista;
        }

        private List<RecepcionDetalle> ObtenerPedidosRecibidos()
        {
            return ObtenerRecepcionesPorEstado("RECIBIDO");
        }

        private List<RecepcionDetalle> ObtenerPedidosNovedad()
        {
            return ObtenerRecepcionesPorEstado("NOVEDAD");
        }

        private List<RecepcionDetalle> ObtenerRecepcionesPorEstado(string estado)
        {
            var lista = new List<RecepcionDetalle>();
            try
            {
                using var conn = _conexion.ObtenerConexion();
                string sql = @"
                    SELECT rb.id, rb.compra_id, p.nombre_empresa, rb.fecha_recibido, rb.hora,
                           rb.factura, rb.contenido, rb.cantidad_bultos, rb.quien_recibe,
                           rb.novedades, rb.fecha_registro, c.estado
                    FROM recepciones_bodega rb
                    INNER JOIN compras c ON rb.compra_id = c.id
                    INNER JOIN proveedores p ON c.proveedor_id = p.id
                    WHERE c.estado = @estado
                    ORDER BY rb.fecha_recibido DESC, rb.id DESC";

                using var cmd = new MySqlCommand(sql, conn);
                cmd.Parameters.AddWithValue("@estado", estado);
                using var rdr = cmd.ExecuteReader();
                while (rdr.Read())
                {
                    lista.Add(new RecepcionDetalle
                    {
                        Id              = rdr.GetInt32("id"),
                        CompraId        = rdr.GetInt32("compra_id"),
                        NombreProveedor = rdr.GetString("nombre_empresa"),
                        FechaRecibido   = rdr.IsDBNull(rdr.GetOrdinal("fecha_recibido"))
                                          ? null : rdr.GetDateTime("fecha_recibido"),
                        Hora            = rdr.IsDBNull(rdr.GetOrdinal("hora"))
                                          ? null : rdr.GetTimeSpan("hora"),
                        Factura         = rdr.IsDBNull(rdr.GetOrdinal("factura"))
                                          ? null : rdr.GetString("factura"),
                        Contenido       = rdr.IsDBNull(rdr.GetOrdinal("contenido"))
                                          ? null : rdr.GetString("contenido"),
                        CantidadBultos  = rdr.IsDBNull(rdr.GetOrdinal("cantidad_bultos"))
                                          ? null : rdr.GetInt32("cantidad_bultos"),
                        QuienRecibe     = rdr.IsDBNull(rdr.GetOrdinal("quien_recibe"))
                                          ? null : rdr.GetString("quien_recibe"),
                        Novedades       = rdr.IsDBNull(rdr.GetOrdinal("novedades"))
                                          ? null : rdr.GetString("novedades"),
                        FechaRegistro   = rdr.IsDBNull(rdr.GetOrdinal("fecha_registro"))
                                          ? null : rdr.GetDateTime("fecha_registro"),
                        EstadoCompra    = rdr.GetString("estado")
                    });
                }
                rdr.Close();

                foreach (var rec in lista)
                {
                    string sqlDet = @"
                        SELECT dc.producto_id, pr.nombre, dc.cantidad
                        FROM detalle_compra dc
                        INNER JOIN productos pr ON dc.producto_id = pr.id
                        WHERE dc.compra_id = @cid";
                    using var cmdD = new MySqlCommand(sqlDet, conn);
                    cmdD.Parameters.AddWithValue("@cid", rec.CompraId);
                    using var rdrD = cmdD.ExecuteReader();
                    while (rdrD.Read())
                    {
                        rec.Productos.Add(new DetalleCompra
                        {
                            CompraId       = rec.CompraId,
                            ProductoId     = rdrD.GetInt32("producto_id"),
                            NombreProducto = rdrD.GetString("nombre"),
                            Cantidad       = rdrD.GetInt32("cantidad")
                        });
                    }
                    rdrD.Close();
                }
            }
            catch { /* log */ }
            return lista;
        }
    }
}