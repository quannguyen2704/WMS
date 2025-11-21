using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Models;
using WMS.Repository;
using X.PagedList;
using X.PagedList.Extensions;
using System.Linq;
using System.IO;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    [Route("Admin/[controller]/[action]")]
    public class ProductionController : Controller
    {
        private readonly DataContext _db;
        public ProductionController(DataContext db) => _db = db;

        // ======================= DANH SÁCH =======================
        [HttpGet]
        public async Task<IActionResult> Index(string? keyword, int? statusFilter, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10)
        {
            var query = _db.ProductionOrders
                .Include(p => p.Product)
                .Include(p => p.Materials).ThenInclude(m => m.Product)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(p => p.OrderNumber.Contains(keyword) || p.Product.ProductName.Contains(keyword));

            if (statusFilter.HasValue)
                query = query.Where(p => (int)p.Status == statusFilter.Value);

            if (fromDate.HasValue)
                query = query.Where(p => p.StartDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(p => p.StartDate <= toDate.Value);

            query = query.OrderByDescending(p => p.StartDate);

            var list = await query.ToListAsync();

            // 🧮 Tổng hợp nhanh
            ViewBag.TotalProductValue = list.Sum(o => o.Quantity * o.UnitPrice);
            ViewBag.TotalMaterialCost = list.Sum(o => o.Materials.Sum(m => m.PlannedQuantity * m.UnitPrice));
            ViewBag.TotalProfit = (decimal)ViewBag.TotalProductValue - (decimal)ViewBag.TotalMaterialCost;

            var paged = list.ToPagedList(page, pageSize);
            ViewBag.Keyword = keyword;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            return View(paged);
        }

        // ======================= TẠO MỚI (GET) =======================
        [HttpGet]
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        // ======================= TẠO MỚI (POST) =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductionOrderModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                TempData["ErrorMessage"] = "❌ Dữ liệu không hợp lệ!";
                return View(model);
            }

            // ✅ Sinh mã tự động
            string prefix = "SX" + DateTime.Now.ToString("yyyyMMdd");
            int countToday = await _db.ProductionOrders.CountAsync(o => o.OrderNumber.StartsWith(prefix)) + 1;
            model.OrderNumber = $"{prefix}-{countToday:D3}";

            model.Status = ProductionStatus.IN_PROGRESS;
            model.StartDate = DateTime.Now;

            _db.ProductionOrders.Add(model);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ Đã tạo lệnh sản xuất {model.OrderNumber}!";
            return RedirectToAction(nameof(Index));
        }

        // ======================= CHỈNH SỬA (GET) =======================
        [HttpGet("{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _db.ProductionOrders
                .Include(p => p.Product)
                .Include(p => p.Materials).ThenInclude(m => m.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy lệnh sản xuất!";
                return RedirectToAction(nameof(Index));
            }

            LoadDropdowns();
            return View(order);
        }

        // ======================= CHỈNH SỬA (POST) =======================
        [HttpPost("{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductionOrderModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                TempData["ErrorMessage"] = "❌ Dữ liệu không hợp lệ!";
                return View(model);
            }

            var existing = await _db.ProductionOrders
                .Include(o => o.Materials)
                .Include(o => o.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (existing == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy lệnh sản xuất!";
                return RedirectToAction(nameof(Index));
            }

            // 🧩 Cập nhật cơ bản
            existing.Quantity = model.Quantity;
            existing.Notes = model.Notes;
            existing.Status = model.Status;
            existing.UnitPrice = model.UnitPrice;
            existing.PlannedEndDate = model.PlannedEndDate;

            // ✅ Ghi lại EndDate khi hoàn thành
            if (existing.Status == ProductionStatus.DONE && existing.EndDate == null)
            {
                existing.EndDate = DateTime.Now;
            }

            // 🧾 Cập nhật nguyên liệu
            _db.ProductionOrderMaterials.RemoveRange(existing.Materials);
            existing.Materials.Clear();
            foreach (var mat in model.Materials)
            {
                existing.Materials.Add(new ProductionOrderMaterialModel
                {
                    MaterialId = mat.MaterialId,
                    PlannedQuantity = mat.PlannedQuantity,
                    Unit = mat.Unit,
                    UnitPrice = mat.UnitPrice
                });
            }

            // ✅ Nếu hoàn thành → cập nhật kho
            if (existing.Status == ProductionStatus.DONE)
            {
                bool imported = await _db.WarehouseTransaction.AnyAsync(t =>
                    t.Notes.Contains(existing.OrderNumber) &&
                    t.TransactionType == "Import" &&
                    t.ProductId == existing.ProductId);

                if (!imported)
                {
                    _db.WarehouseTransaction.Add(new WarehouseTransactionModel
                    {
                        ProductId = existing.ProductId,
                        Quantity = existing.Quantity,
                        TransactionType = "Import",
                        TransactionDate = DateTime.Now,
                        Unit = existing.Product?.ProductUnit,
                        UnitPrice = existing.UnitPrice,
                        Notes = $"Hoàn thành SX {existing.OrderNumber}"
                    });

                    foreach (var mat in existing.Materials)
                    {
                        _db.WarehouseTransaction.Add(new WarehouseTransactionModel
                        {
                            ProductId = mat.MaterialId,
                            Quantity = mat.PlannedQuantity,
                            TransactionType = "Export",
                            TransactionDate = DateTime.Now,
                            Unit = mat.Unit,
                            UnitPrice = mat.UnitPrice,
                            Notes = $"Tiêu hao cho SX {existing.OrderNumber}"
                        });
                    }
                }
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ Cập nhật thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ======================= XÓA (DELETE) =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.ProductionOrders
                .Include(o => o.Materials)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy lệnh sản xuất!";
                return RedirectToAction(nameof(Index));
            }

            // Xóa các chi tiết nguyên liệu liên quan trước
            _db.ProductionOrderMaterials.RemoveRange(order.Materials);

            // Nếu lệnh đã hoàn thành (DONE), ta cần kiểm tra và xóa các giao dịch kho liên quan.
            if (order.Status == ProductionStatus.DONE)
            {
                // Tìm giao dịch nhập kho thành phẩm (thành phẩm đã được nhập)
                var productImportTransaction = await _db.WarehouseTransaction
                    .FirstOrDefaultAsync(t =>
                        t.ProductId == order.ProductId &&
                        t.TransactionType == "Import" &&
                        t.Notes.Contains(order.OrderNumber));

                if (productImportTransaction != null)
                {
                    // Xóa giao dịch nhập kho thành phẩm (giảm tồn kho)
                    _db.WarehouseTransaction.Remove(productImportTransaction);
                }

                // Tìm các giao dịch xuất kho nguyên liệu (nguyên liệu đã bị tiêu hao)
                var materialExportTransactions = await _db.WarehouseTransaction
                    .Where(t =>
                        t.TransactionType == "Export" &&
                        t.Notes.Contains(order.OrderNumber))
                    .ToListAsync();

                if (materialExportTransactions.Any())
                {
                    // Xóa các giao dịch xuất kho nguyên liệu (cộng lại tồn kho)
                    _db.WarehouseTransaction.RemoveRange(materialExportTransactions);
                }

                // Lưu ý: Việc cập nhật Product.ProductQuantity (tồn kho ban đầu) không cần thiết ở đây
                // vì hệ thống của bạn tính tồn kho theo tổng giao dịch (Initial + Import - Export). 
                // Chỉ cần xóa giao dịch là đủ để đảo ngược.
            }

            // Xóa lệnh sản xuất chính
            _db.ProductionOrders.Remove(order);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ Đã xóa lệnh sản xuất thành công!";
            return RedirectToAction(nameof(Index));
        }


        // ======================= XUẤT EXCEL =======================
        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            var orders = await _db.ProductionOrders
                .Include(p => p.Product)
                .Include(p => p.Materials).ThenInclude(m => m.Product)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Báo cáo sản xuất");

            // ====== Header ======
            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã lệnh";
            ws.Cell(1, 3).Value = "Thành phẩm";
            ws.Cell(1, 4).Value = "SL Thành phẩm";
            ws.Cell(1, 5).Value = "Đơn vị";
            ws.Cell(1, 6).Value = "Đơn giá (VNĐ)";
            ws.Cell(1, 7).Value = "Tổng giá trị (VNĐ)";
            ws.Cell(1, 8).Value = "Chi phí nguyên liệu (VNĐ)";
            ws.Cell(1, 9).Value = "Lợi nhuận (VNĐ)";
            ws.Cell(1, 10).Value = "Ngày bắt đầu";
            ws.Cell(1, 11).Value = "Ngày dự kiến";
            ws.Cell(1, 12).Value = "Ngày hoàn thành";
            ws.Cell(1, 13).Value = "Chi tiết nguyên liệu (Tên | SL | Đơn giá | Thành tiền)";

            var header = ws.Range(1, 1, 1, 13);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#007bff");
            header.Style.Font.FontColor = XLColor.White;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            int row = 2, stt = 1;
            foreach (var o in orders)
            {
                decimal material = o.Materials.Sum(m => m.PlannedQuantity * m.UnitPrice);
                decimal value = o.Quantity * o.UnitPrice;
                decimal profit = value - material;

                // 📋 Gộp thông tin nguyên liệu
                string materialDetail = string.Join("\n",
                    o.Materials.Select(m =>
                        $"{m.Product?.ProductName} | {m.PlannedQuantity} {m.Unit} | {m.UnitPrice:N0} | {(m.PlannedQuantity * m.UnitPrice):N0}"
                    ));

                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = o.OrderNumber;
                ws.Cell(row, 3).Value = o.Product?.ProductName;
                ws.Cell(row, 4).Value = o.Quantity;
                ws.Cell(row, 5).Value = o.Product?.ProductUnit;
                ws.Cell(row, 6).Value = o.UnitPrice;
                ws.Cell(row, 7).Value = value;
                ws.Cell(row, 8).Value = material;
                ws.Cell(row, 9).Value = profit;
                ws.Cell(row, 10).Value = o.StartDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 11).Value = o.PlannedEndDate?.ToString("dd/MM/yyyy") ?? "-";
                ws.Cell(row, 12).Value = o.EndDate?.ToString("dd/MM/yyyy HH:mm") ?? "-";
                ws.Cell(row, 13).Value = materialDetail;

                ws.Cell(row, 13).Style.Alignment.WrapText = true;
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.Columns(6, 9).Style.NumberFormat.Format = "#,##0";

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"ProductionReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ======================= LOAD DROPDOWNS =======================
        private void LoadDropdowns()
        {
            ViewBag.Products = _db.Products
                .Where(p => p.ProductType == ProductType.FinishedProduct)
                .ToList();

            ViewBag.Materials = _db.Products
                .Where(p => p.ProductType == ProductType.Material)
                .ToList();
        }

        // ======================= API KIỂM TRA TỒN KHO =======================
        [HttpGet]
        public IActionResult GetInventoryStock(int id)
        {
            // Lấy sản phẩm
            var product = _db.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
                return Json(new { success = false, message = "❌ Không tìm thấy sản phẩm." });

            // 🔍 Tính tồn kho thực tế (Initial + Import - Export)
            var totalImported = _db.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Import")
                .Sum(t => (decimal?)t.Quantity) ?? 0;

            var totalExported = _db.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Export")
                .Sum(t => (decimal?)t.Quantity) ?? 0;

            var currentStock = product.ProductQuantity + totalImported - totalExported;

            return Json(new
            {
                success = true,
                stock = currentStock,
                unit = product.ProductUnit
            });
        }
    }
}
