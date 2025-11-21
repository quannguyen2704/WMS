using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Models;
using WMS.Repository;
using ClosedXML.Excel;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class PurchaseController : Controller
    {
        private readonly DataContext _db;
        public PurchaseController(DataContext db) => _db = db;

        // 📋 Danh sách đơn mua
        public async Task<IActionResult> Index(string keyword, string fromDate, string toDate)
        {
            var purchasesQuery = _db.PurchaseOrders
                .Include(p => p.Product)
                .Include(p => p.Supplier)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                purchasesQuery = purchasesQuery.Where(p =>
                    p.PurchaseNumber.Contains(keyword) ||
                    (p.Product != null && p.Product.ProductName.Contains(keyword)));
                ViewBag.Keyword = keyword;
            }

            if (DateTime.TryParse(fromDate, out DateTime dateFrom))
            {
                purchasesQuery = purchasesQuery.Where(p => p.OrderDate.Date >= dateFrom.Date);
                ViewBag.FromDate = fromDate;
            }

            if (DateTime.TryParse(toDate, out DateTime dateTo))
            {
                purchasesQuery = purchasesQuery.Where(p => p.OrderDate.Date <= dateTo.Date);
                ViewBag.ToDate = toDate;
            }

            var purchases = await purchasesQuery
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            ViewBag.TotalPurchaseValue = purchases.Sum(p => p.Quantity * (p.UnitPrice));
            return View(purchases);
        }

        // ➕ Tạo mới
        [HttpGet]
        public IActionResult Create()
        {
            LoadDropdowns();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PurchaseOrderModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                TempData["ErrorMessage"] = "❌ Dữ liệu không hợp lệ, vui lòng kiểm tra lại!";
                return View(model);
            }

            if (model.SupplierId == 0 || model.ProductId == 0)
            {
                LoadDropdowns();
                TempData["ErrorMessage"] = "❌ Vui lòng chọn nhà cung cấp và nguyên liệu!";
                return View(model);
            }

            // ✅ Sinh mã đơn tự động dạng: POyyyyMMdd-001
            string todayPrefix = "PO" + DateTime.Now.ToString("yyyyMMdd");
            int countToday = await _db.PurchaseOrders.CountAsync(p => p.PurchaseNumber.StartsWith(todayPrefix));
            model.PurchaseNumber = $"{todayPrefix}-{(countToday + 1):D3}";

            model.OrderDate = DateTime.Now;
            model.Status = PurchaseStatus.CREATED;

            _db.PurchaseOrders.Add(model);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ Đã tạo đơn mua {model.PurchaseNumber} thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ✏️ Chỉnh sửa
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _db.PurchaseOrders.FindAsync(id);
            if (order == null) return NotFound();

            LoadDropdowns();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(PurchaseOrderModel model)
        {
            if (!ModelState.IsValid)
            {
                LoadDropdowns();
                return View(model);
            }

            var existing = await _db.PurchaseOrders.FindAsync(model.Id);
            if (existing == null) return NotFound();

            bool statusChangedToReceived = existing.Status != PurchaseStatus.RECEIVED && model.Status == PurchaseStatus.RECEIVED;

            existing.ProductId = model.ProductId;
            existing.SupplierId = model.SupplierId;
            existing.Quantity = model.Quantity;
            existing.Unit = model.Unit;
            existing.UnitPrice = model.UnitPrice;
            existing.Description = model.Description;
            existing.Status = model.Status;

            if (statusChangedToReceived)
            {
                existing.ReceivedDate = DateTime.Now;

                _db.WarehouseTransaction.Add(new WarehouseTransactionModel
                {
                    ProductId = existing.ProductId,
                    SupplierId = existing.SupplierId,
                    Quantity = existing.Quantity,
                    TransactionType = "Import",
                    TransactionDate = DateTime.Now,
                    Unit = existing.Unit,
                    UnitPrice = existing.UnitPrice,
                    Notes = $"Nhập kho từ đơn mua {existing.PurchaseNumber}"
                });
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ Cập nhật đơn mua thành công!";
            return RedirectToAction(nameof(Index));
        }

        // 🗑️ Xóa
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.PurchaseOrders.FindAsync(id);
            if (order != null)
            {
                _db.PurchaseOrders.Remove(order);
                await _db.SaveChangesAsync();
                TempData["SuccessMessage"] = "🗑️ Đã xóa đơn mua!";
            }
            return RedirectToAction(nameof(Index));
        }

        // 📤 Xuất Excel
        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            var orders = await _db.PurchaseOrders
                .Include(p => p.Product)
                .Include(p => p.Supplier)
                .OrderByDescending(p => p.OrderDate)
                .ToListAsync();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Danh sách đơn mua");

            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã đơn";
            ws.Cell(1, 3).Value = "Nguyên liệu";
            ws.Cell(1, 4).Value = "Nhà cung cấp";
            ws.Cell(1, 5).Value = "Số lượng";
            ws.Cell(1, 6).Value = "Đơn vị";
            ws.Cell(1, 7).Value = "Đơn giá (VNĐ)";
            ws.Cell(1, 8).Value = "Thành tiền";
            ws.Cell(1, 9).Value = "Ngày đặt";
            ws.Cell(1, 10).Value = "Ngày nhận";
            ws.Cell(1, 11).Value = "Trạng thái";

            int row = 2;
            int stt = 1;
            foreach (var o in orders)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = o.PurchaseNumber;
                ws.Cell(row, 3).Value = o.Product?.ProductName;
                ws.Cell(row, 4).Value = o.Supplier?.Name;
                ws.Cell(row, 5).Value = o.Quantity;
                ws.Cell(row, 6).Value = o.Unit;
                ws.Cell(row, 7).Value = o.UnitPrice;
                ws.Cell(row, 8).Value = o.Quantity * o.UnitPrice;
                ws.Cell(row, 9).Value = o.OrderDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 10).Value = o.ReceivedDate?.ToString("dd/MM/yyyy") ?? "-";
                ws.Cell(row, 11).Value = o.Status.ToString();
                row++;
            }

            ws.Columns().AdjustToContents();
            using var stream = new MemoryStream();
            wb.SaveAs(stream);
            var content = stream.ToArray();

            return File(
                content,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                $"DanhSachDonMua_{DateTime.Now:yyyyMMddHHmmss}.xlsx"
            );
        }

        // 🔍 API: Lấy tồn kho thực tế từ bảng WarehouseTransaction
        [HttpGet]
        public async Task<IActionResult> GetInventory(int id)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return Json(new { success = false, message = "Không tìm thấy nguyên liệu!" });

            var totalImported = await _db.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Import")
                .SumAsync(t => (decimal?)t.Quantity) ?? 0;

            var totalExported = await _db.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Export")
                .SumAsync(t => (decimal?)t.Quantity) ?? 0;

            var stock = product.ProductQuantity + totalImported - totalExported;

            return Json(new
            {
                success = true,
                name = product.ProductName,
                stock = stock,
                unit = product.ProductUnit
            });
        }

        private void LoadDropdowns()
        {
            ViewBag.Products = _db.Products.Where(p => p.ProductType == ProductType.Material).ToList();
            ViewBag.Suppliers = _db.Supplier.ToList();
        }
    }
}
