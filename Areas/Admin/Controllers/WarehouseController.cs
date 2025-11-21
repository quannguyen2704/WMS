using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using WMS.Models;
using WMS.Repository;
using X.PagedList;
using X.PagedList.Extensions;
using ClosedXML.Excel;
using System.IO;
using System.Reflection; // Cần cho việc lấy giá trị từ JsonResult
using System.Linq;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class WarehouseController : Controller
    {
        private readonly DataContext _dbContext;

        public WarehouseController(DataContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ========================= IMPORT INDEX =========================
        public IActionResult ImportIndex(string? keyword, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10)
        {
            var query = _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .Include(t => t.Supplier)
                .Where(t => t.TransactionType == "Import")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(t => t.Product.ProductName.Contains(keyword));

            if (fromDate.HasValue)
                query = query.Where(t => t.TransactionDate >= fromDate.Value);
            if (toDate.HasValue)
            {
                var end = toDate.Value.AddDays(1);
                query = query.Where(t => t.TransactionDate < end);
            }

            var paged = query.OrderByDescending(t => t.TransactionDate)
                             .ToPagedList(page, pageSize);

            ViewBag.Keyword = keyword;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.TotalCount = paged.TotalItemCount;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = paged.PageCount;

            return View(paged);
        }

        // ========================= EXPORT INDEX =========================
        public IActionResult ExportIndex(string? keyword, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 10)
        {
            var query = _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .Include(t => t.Supplier)
                .Where(t => t.TransactionType == "Export")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(t => t.Product.ProductName.Contains(keyword));

            if (fromDate.HasValue)
                query = query.Where(t => t.TransactionDate >= fromDate.Value);
            if (toDate.HasValue)
            {
                var end = toDate.Value.AddDays(1);
                query = query.Where(t => t.TransactionDate < end);
            }

            var paged = query.OrderByDescending(t => t.TransactionDate)
                             .ToPagedList(page, pageSize);

            ViewBag.Keyword = keyword;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.TotalCount = paged.TotalItemCount;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = paged.PageCount;

            return View(paged);
        }

        // ========================= EXPORT TO EXCEL (CÓ ĐƠN GIÁ + THÀNH TIỀN) =========================
        public async Task<IActionResult> ExportToExcel(string type = "Import", DateTime? dateFilter = null, string? monthFilter = null)
        {
            var query = _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .Include(t => t.Supplier)
                .Where(t => t.TransactionType == type)
                .AsQueryable();

            if (dateFilter.HasValue)
                query = query.Where(t => t.TransactionDate.Date == dateFilter.Value.Date);

            if (!string.IsNullOrEmpty(monthFilter))
            {
                var month = DateTime.Parse(monthFilter);
                query = query.Where(t => t.TransactionDate.Month == month.Month && t.TransactionDate.Year == month.Year);
            }

            var data = await query.OrderByDescending(t => t.TransactionDate).ToListAsync();

            var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Danh sách");

            // Tiêu đề cột
            ws.Cell(1, 1).Value = "STT";
            ws.Cell(1, 2).Value = "Mã SP";
            ws.Cell(1, 3).Value = "Tên sản phẩm";
            ws.Cell(1, 4).Value = "Đơn vị";
            ws.Cell(1, 5).Value = "Số lượng";
            ws.Cell(1, 6).Value = "Đơn giá (VNĐ)";
            ws.Cell(1, 7).Value = "Thành tiền (VNĐ)";
            ws.Cell(1, 8).Value = "Ngày giao dịch";
            ws.Cell(1, 9).Value = "Ghi chú";
            ws.Cell(1, 10).Value = "Loại giao dịch";
            ws.Cell(1, 11).Value = "Nhà cung cấp";

            int row = 2, stt = 1;
            foreach (var item in data)
            {
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = item.Product?.ProductCode;
                ws.Cell(row, 3).Value = item.Product?.ProductName;
                ws.Cell(row, 4).Value = item.Unit;
                ws.Cell(row, 5).Value = item.Quantity;
                ws.Cell(row, 6).Value = item.UnitPrice;
                ws.Cell(row, 7).Value = item.Quantity * item.UnitPrice;
                ws.Cell(row, 8).Value = item.TransactionDate.ToString("dd/MM/yyyy HH:mm");
                ws.Cell(row, 9).Value = item.Notes;
                ws.Cell(row, 10).Value = item.TransactionType;
                ws.Cell(row, 11).Value = item.Supplier?.Name ?? "";
                row++;
            }

            ws.Columns().AdjustToContents();

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"BaoCao_{type}_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ========================= IMPORT FORM =========================
        [HttpGet]
        public IActionResult Import()
        {
            ViewBag.Suppliers = new SelectList(_dbContext.Supplier, "Id", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Import(WarehouseTransactionModel model)
        {
            ModelState.Remove("TransactionType");
            ModelState.Remove("Product");
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                var product = await _dbContext.Products.FindAsync(model.ProductId);
                if (product == null) return NotFound();

                model.TransactionType = "Import";
                model.TransactionDate = DateTime.Now;
                model.Unit = product.ProductUnit;

                // Gợi ý đơn giá nếu chưa có
                if (model.UnitPrice <= 0)
                    model.UnitPrice = product.ProductPrice;

                _dbContext.WarehouseTransaction.Add(model);
                product.ProductQuantity += model.Quantity;
                _dbContext.Products.Update(product);

                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "✅ Nhập kho thành công.";
                return RedirectToAction("ImportIndex");
            }

            ViewBag.Suppliers = new SelectList(_dbContext.Supplier, "Id", "Name", model.SupplierId);
            return View(model);
        }

        // ========================= EXPORT FORM =========================
        [HttpGet]
        public IActionResult Export()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Export(WarehouseTransactionModel model)
        {
            ModelState.Remove("TransactionType");
            ModelState.Remove("Product");
            ModelState.Remove("Supplier");

            if (ModelState.IsValid)
            {
                var product = await _dbContext.Products.FindAsync(model.ProductId);
                if (product == null) return NotFound();

                // 1. TÍNH TOÁN TỒN KHO THỰC TẾ
                // Sử dụng hàm GetInventoryStock để tính toán tồn kho dựa trên logic đã định
                var stockResult = GetInventoryStock(product.Id) as JsonResult;
                decimal currentStock = 0;

                // Phân tích kết quả JSON (sử dụng reflection vì là dynamic object)
                if (stockResult?.Value is { } jsonValue)
                {
                    var stockProperty = jsonValue.GetType().GetProperty("stock");
                    if (stockProperty != null)
                    {
                        // Đảm bảo ép kiểu đúng
                        currentStock = Convert.ToDecimal(stockProperty.GetValue(jsonValue));
                    }
                }

                // 2. KIỂM TRA ĐIỀU KIỆN TỒN KHO
                if (model.Quantity > currentStock)
                {
                    // Thêm lỗi vào ModelState để nó hiển thị trên form
                    ModelState.AddModelError("Quantity", $"Số lượng xuất **{model.Quantity}** vượt quá tồn kho hiện tại **{currentStock} {product.ProductUnit}**.");

                    // Tải lại ViewBag cần thiết cho View
                    ViewBag.Products = _dbContext.Products
                        .Where(p => p.ProductType == ProductType.FinishedProduct)
                        .Select(p => new SelectListItem
                        {
                            Value = p.Id.ToString(),
                            Text = $"{p.ProductName}" // Giữ tên đơn giản cho AJAX
                        }).ToList();

                    // Tải lại các giá trị đã nhập của người dùng
                    TempData["ErrorMessage"] = "❌ Lỗi: Số lượng xuất vượt quá tồn kho thực tế.";
                    return View(model); // Trả lại View với model và lỗi
                }

                // 3. XỬ LÝ GIAO DỊCH (Nếu đủ kho)
                model.TransactionType = "Export";
                model.TransactionDate = DateTime.Now;
                model.SupplierId = null;
                model.Unit = product.ProductUnit;

                // Gợi ý đơn giá nếu chưa có
                if (model.UnitPrice <= 0)
                    model.UnitPrice = product.ProductPrice;

                _dbContext.WarehouseTransaction.Add(model);
                // Giảm ProductQuantity (Đây là cách tính Tồn ban đầu, sẽ được cập nhật lại khi tính Stock)
                product.ProductQuantity -= model.Quantity;
                _dbContext.Products.Update(product);

                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "✅ Xuất kho thành công.";
                return RedirectToAction("ExportIndex");
            }

            // Tải lại ViewBag nếu ModelState không hợp lệ (lỗi validation khác)
            ViewBag.Products = _dbContext.Products
                .Where(p => p.ProductType == ProductType.FinishedProduct)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.ProductName}"
                }).ToList();

            return View(model);
        }
        // ========================= EXPORT EDIT FORM =========================
        [HttpGet]
        public async Task<IActionResult> ExportEdit(int id)
        {
            var transaction = await _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == id && t.TransactionType == "Export");

            if (transaction == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy phiếu xuất kho.";
                return RedirectToAction("ExportIndex");
            }

            ViewBag.Products = _dbContext.Products
                .Where(p => p.ProductType == ProductType.FinishedProduct)
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.ProductName} - Tồn: {p.ProductQuantity}"
                }).ToList();

            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportEdit(WarehouseTransactionModel model)
        {
            ModelState.Remove("Product");
            ModelState.Remove("Supplier");

            var existing = await _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == model.Id && t.TransactionType == "Export");

            if (existing == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy phiếu xuất kho.";
                return RedirectToAction("ExportIndex");
            }

            var product = existing.Product;
            if (product == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy sản phẩm.";
                return RedirectToAction("ExportIndex");
            }

            // 1. TÍNH TOÁN TỒN KHO THỰC TẾ (Không bao gồm số lượng cũ của phiếu này)
            var totalImported = await _dbContext.WarehouseTransaction
                 .Where(t => t.ProductId == product.Id && t.TransactionType == "Import")
                 .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            var totalExported = await _dbContext.WarehouseTransaction
                 .Where(t => t.ProductId == product.Id && t.TransactionType == "Export" && t.Id != existing.Id) // Loại trừ chính phiếu này
                 .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            // Tồn kho trước khi cập nhật
            var currentStock = product.ProductQuantity + totalImported - totalExported;

            // 2. KIỂM TRA ĐIỀU KIỆN TỒN KHO VỚI SỐ LƯỢNG MỚI
            if (model.Quantity > currentStock)
            {
                // Tải lại ViewBag cho View
                ViewBag.Products = _dbContext.Products
                   .Where(p => p.ProductType == ProductType.FinishedProduct)
                   .Select(p => new SelectListItem
                   {
                       Value = p.Id.ToString(),
                       Text = $"{p.ProductName} - Tồn: {p.ProductQuantity}"
                   }).ToList();

                ModelState.AddModelError("Quantity", $"Số lượng mới ({model.Quantity}) vượt quá tồn kho thực tế ({currentStock}).");
                TempData["ErrorMessage"] = "❌ Lỗi: Số lượng xuất vượt quá tồn kho thực tế.";
                return View(model);
            }

            // 3. CẬP NHẬT GIAO DỊCH VÀ TỒN KHO
            // Cập nhật ProductQuantity (cơ sở)
            product.ProductQuantity = currentStock - model.Quantity;

            // 🧾 Cập nhật thông tin phiếu xuất
            existing.Quantity = model.Quantity;
            existing.UnitPrice = model.UnitPrice;
            existing.Notes = model.Notes;
            existing.TransactionDate = DateTime.Now;

            _dbContext.Products.Update(product);
            _dbContext.WarehouseTransaction.Update(existing);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Cập nhật phiếu xuất kho thành công!";
            return RedirectToAction("ExportIndex");
        }


        // ========================= XÓA PHIẾU XUẤT/NHẬP KHO (DELETE) =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var transaction = await _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == id);

            if (transaction == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy giao dịch để xóa.";
                // Quyết định Redirect tới ImportIndex hay ExportIndex dựa trên URL, nhưng dùng ImportIndex làm mặc định
                return RedirectToAction(nameof(ImportIndex));
            }

            var product = transaction.Product;
            if (product == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy sản phẩm liên quan.";
                return RedirectToAction(nameof(ImportIndex));
            }

            // 1. TÍNH TỒN KHO HIỆN TẠI (TRƯỚC KHI XÓA)
            // Loại trừ giao dịch đang xóa (transaction.Id) khỏi cả tổng nhập và tổng xuất
            var totalImported = await _dbContext.WarehouseTransaction
                .Where(t => t.ProductId == product.Id && t.TransactionType == "Import" && t.Id != transaction.Id)
                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            var totalExported = await _dbContext.WarehouseTransaction
                .Where(t => t.ProductId == product.Id && t.TransactionType == "Export" && t.Id != transaction.Id)
                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            // Tồn kho lý thuyết sau khi xóa giao dịch này (chỉ dựa trên các giao dịch còn lại)
            var theoreticalStock = product.ProductQuantity + totalImported - totalExported;

            // 2. ĐẢO NGƯỢC LOGIC TỒN KHO DỰA TRÊN LOẠI GIAO DỊCH ĐANG XÓA
            if (transaction.TransactionType == "Export")
            {
                // Nếu là phiếu Xuất, số lượng đã bị trừ khỏi tồn kho (ProductQuantity). 
                // Khi xóa phiếu này, ta cần cộng lại số lượng đã xuất VÀ đặt lại ProductQuantity bằng Tồn kho lý thuyết.
                // Tuy nhiên, vì công thức tính theoreticalStock đã loại trừ giao dịch này, 
                // ta chỉ cần đặt lại product.ProductQuantity = theoreticalStock.
                product.ProductQuantity = theoreticalStock;
            }
            else if (transaction.TransactionType == "Import")
            {
                // Nếu là phiếu Nhập, số lượng đã được cộng vào tồn kho (ProductQuantity).
                // Khi xóa phiếu này, ta cần TRỪ đi số lượng đã nhập VÀ đặt lại ProductQuantity bằng Tồn kho lý thuyết.

                // Cần kiểm tra liệu tồn kho lý thuyết (sau khi xóa giao dịch nhập này) có bị âm không?
                // Mặc dù lý thuyết này nghe hơi ngược, nhưng do logic tồn kho của bạn: stock = product.ProductQuantity + Tổng Nhập - Tổng Xuất.
                // Khi xóa giao dịch nhập, ProductQuantity cơ sở phải trừ đi số lượng của giao dịch nhập này.

                // Đảm bảo tồn kho không âm sau khi hoàn lại (chỉ kiểm tra nếu ProductQuantity của bạn là tồn kho cơ sở)

                // Trường hợp đơn giản: chỉ cần đặt lại ProductQuantity theo theoreticalStock.
                product.ProductQuantity = theoreticalStock;
            }

            // 3. CẬP NHẬT & XÓA
            _dbContext.Products.Update(product);
            _dbContext.WarehouseTransaction.Remove(transaction);
            await _dbContext.SaveChangesAsync();

            // Chuyển hướng hợp lý hơn: dựa trên loại giao dịch đã xóa
            string redirectAction = transaction.TransactionType == "Export" ? nameof(ExportIndex) : nameof(ImportIndex);

            TempData["SuccessMessage"] = $"🗑️ Đã xóa phiếu {transaction.TransactionType} và cập nhật tồn kho thành công.";
            return RedirectToAction(redirectAction);
        }


        // ========================= IMPORT EDIT FORM =========================
        [HttpGet]
        public async Task<IActionResult> ImportEdit(int id)
        {
            var transaction = await _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .Include(t => t.Supplier)
                .FirstOrDefaultAsync(t => t.Id == id && t.TransactionType == "Import");

            if (transaction == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy phiếu nhập kho.";
                return RedirectToAction("ImportIndex");
            }

            ViewBag.Suppliers = new SelectList(_dbContext.Supplier, "Id", "Name", transaction.SupplierId);
            ViewBag.Products = _dbContext.Products
                .Select(p => new SelectListItem
                {
                    Value = p.Id.ToString(),
                    Text = $"{p.ProductName} - ({p.ProductType}) - Tồn: {p.ProductQuantity}"
                }).ToList();

            return View(transaction);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportEdit(WarehouseTransactionModel model)
        {
            ModelState.Remove("Product");
            ModelState.Remove("Supplier");

            var existing = await _dbContext.WarehouseTransaction
                .Include(t => t.Product)
                .FirstOrDefaultAsync(t => t.Id == model.Id && t.TransactionType == "Import");

            if (existing == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy phiếu nhập kho.";
                return RedirectToAction("ImportIndex");
            }

            var product = existing.Product;
            if (product == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy sản phẩm.";
                return RedirectToAction("ImportIndex");
            }

            // 🔁 Cập nhật tồn kho (trừ số cũ, cộng số mới)
            product.ProductQuantity -= existing.Quantity;
            product.ProductQuantity += model.Quantity;

            // 🧾 Cập nhật thông tin phiếu nhập
            existing.Quantity = model.Quantity;
            existing.UnitPrice = model.UnitPrice;
            existing.Notes = model.Notes;
            existing.SupplierId = model.SupplierId;
            existing.TransactionDate = DateTime.Now;

            _dbContext.Products.Update(product);
            _dbContext.WarehouseTransaction.Update(existing);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Cập nhật phiếu nhập kho thành công!";
            return RedirectToAction("ImportIndex");
        }


        // ========================= AJAX: LẤY TỒN KHO TỪ INVENTORY =========================
        [HttpGet]
        public IActionResult GetInventoryStock(int id)
        {
            // Lấy thông tin sản phẩm
            var product = _dbContext.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
                return Json(new { success = false, message = "Không tìm thấy sản phẩm." });

            // Tổng nhập
            var totalImported = _dbContext.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Import")
                .Sum(t => (decimal?)t.Quantity) ?? 0;

            // Tổng xuất
            var totalExported = _dbContext.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Export")
                .Sum(t => (decimal?)t.Quantity) ?? 0;

            // ✅ Công thức tồn kho
            var stock = product.ProductQuantity + totalImported - totalExported;

            // Trả kết quả JSON cho view
            return Json(new
            {
                success = true,
                productName = product.ProductName,
                unit = product.ProductUnit,
                stock = stock
            });
        }

        // ========================= AJAX LẤY SẢN PHẨM THEO LOẠI =========================
        [HttpGet]
        public IActionResult GetProductsByType(string type)
        {
            // Trả về mảng rỗng nếu không có tham số
            if (string.IsNullOrWhiteSpace(type))
                return Json(new List<object>());

            // Parse chuỗi sang enum, BẬT ignoreCase để tránh sai khác hoa/thường
            if (!Enum.TryParse<ProductType>(type, true, out var productType))
                return Json(new List<object>());

            // Lọc theo enum
            var products = _dbContext.Products
                .Where(p => p.ProductType == productType)
                .OrderBy(p => p.ProductName)
                .Select(p => new
                {
                    id = p.Id,
                    name = $"{p.ProductName} ",
                    unit = p.ProductUnit
                })
                .ToList();

            return Json(products);
        }
    }
}
