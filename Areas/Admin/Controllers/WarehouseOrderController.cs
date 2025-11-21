using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using WMS.Models;
using X.PagedList;
using X.PagedList.Extensions;
using WMS.Repository;
using ClosedXML.Excel; // Cần thêm thư viện này
using System.IO;       // Cần thêm thư viện này
using System.Linq;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class WarehouseOrderController : Controller
    {
        private readonly DataContext _db;

        public WarehouseOrderController(DataContext db)
        {
            _db = db;
        }

        // 📋 Danh sách đơn hàng đặt của khách hàng
        public async Task<IActionResult> Index(
            string? keyword,
            DateTime? fromDate,
            DateTime? toDate,
            WarehouseOrderStatus? statusFilter,
            int page = 1,
            int pageSize = 10)
        {
            var query = _db.Orders
                .Include(o => o.Product)
                .Include(o => o.Customer)
                .AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(o =>
                    o.OrderNumber.Contains(keyword) ||
                    o.Product.ProductName.Contains(keyword) ||
                    o.Customer.Name.Contains(keyword));
            }

            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
            {
                // Thêm 1 ngày để bao gồm trọn vẹn ngày cuối cùng
                var end = toDate.Value.AddDays(1);
                query = query.Where(o => o.OrderDate < end);
            }

            if (statusFilter.HasValue)
                query = query.Where(o => o.WarehouseStatus == statusFilter.Value);

            var orders = query.OrderByDescending(o => o.OrderDate)
                 .ToPagedList(page, pageSize);

            ViewBag.Keyword = keyword;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.StatusFilter = statusFilter;

            // Truyền danh sách trạng thái xuống View
            ViewBag.StatusList = Enum.GetValues(typeof(WarehouseOrderStatus))
                .Cast<WarehouseOrderStatus>()
                .Select(s => new
                {
                    Value = (int)s,
                    Text = s.GetType()
                            .GetMember(s.ToString())
                            .First()
                            .GetCustomAttribute<DisplayAttribute>()?.Name ?? s.ToString()
                }).ToList();

            return View(orders);
        }

        // ======================= XUẤT EXCEL (ĐÃ THÊM) =======================
        [HttpGet]
        public async Task<IActionResult> ExportToExcel(string? keyword, DateTime? fromDate, DateTime? toDate, WarehouseOrderStatus? statusFilter)
        {
            var query = _db.Orders
                .Include(o => o.Product)
                .Include(o => o.Customer)
                .AsQueryable();

            // Áp dụng bộ lọc
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(o =>
                    o.OrderNumber.Contains(keyword) ||
                    o.Product.ProductName.Contains(keyword) ||
                    o.Customer.Name.Contains(keyword));
            }

            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
            {
                var end = toDate.Value.AddDays(1);
                query = query.Where(o => o.OrderDate < end);
            }

            if (statusFilter.HasValue)
                query = query.Where(o => o.WarehouseStatus == statusFilter.Value);

            var orders = await query.OrderByDescending(o => o.OrderDate).ToListAsync();

            var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("WarehouseOrders");

            // ====== Header ======
            ws.Cell(1, 1).Value = "Mã đơn hàng";
            ws.Cell(1, 2).Value = "Sản phẩm";
            ws.Cell(1, 3).Value = "Số lượng";
            ws.Cell(1, 4).Value = "Đơn vị";
            ws.Cell(1, 5).Value = "Đơn giá (VNĐ)";
            ws.Cell(1, 6).Value = "Thành tiền (VNĐ)";
            ws.Cell(1, 7).Value = "Khách hàng";
            ws.Cell(1, 8).Value = "Phương thức thanh toán"; // ✅ Thêm cột mới
            ws.Cell(1, 9).Value = "Ngày đặt";
            ws.Cell(1, 10).Value = "Ngày giao dự kiến";
            ws.Cell(1, 11).Value = "Trạng thái kho";

            var header = ws.Range(1, 1, 1, 11);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#007bff");
            header.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var o in orders)
            {
                ws.Cell(row, 1).Value = o.OrderNumber;
                ws.Cell(row, 2).Value = o.Product?.ProductName;
                ws.Cell(row, 3).Value = o.Quantity;
                ws.Cell(row, 4).Value = o.Unit;
                ws.Cell(row, 5).Value = o.UnitPrice;
                ws.Cell(row, 6).Value = o.Quantity * o.UnitPrice;
                ws.Cell(row, 7).Value = o.Customer?.Name;

                // ✅ Hiển thị phương thức thanh toán
                string paymentMethod = o.PaymentMethod switch
                {
                    PaymentMethod.COD => "Thanh toán khi nhận hàng (COD)",
                    PaymentMethod.MOMO => "Chuyển khoản Momo",
                    _ => "Không xác định"
                };
                ws.Cell(row, 8).Value = paymentMethod;

                ws.Cell(row, 9).Value = o.OrderDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 10).Value = o.DeliveryDate?.ToString("dd/MM/yyyy") ?? "-";
                ws.Cell(row, 11).Value = o.WarehouseStatus.ToString();
                row++;
            }

            ws.Columns().AdjustToContents();
            ws.Columns(5, 6).Style.NumberFormat.Format = "#,##0"; // Định dạng tiền tệ

            var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            string fileName = $"WarehouseOrders_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        // ✏️ Edit trạng thái kho
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var order = await _db.Orders
                .Include(o => o.Product)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // 🧩 Truyền danh sách enum ra View (tiếng Việt nhờ DisplayAttribute)
            ViewBag.StatusList = Enum.GetValues(typeof(WarehouseOrderStatus))
                .Cast<WarehouseOrderStatus>()
                .Select(s => new
                {
                    Value = (int)s,
                    Text = s.GetType()
                            .GetMember(s.ToString())
                            .First()
                            .GetCustomAttribute<DisplayAttribute>()?.Name ?? s.ToString()
                })
                .ToList();

            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OrderModel model)
        {
            var order = await _db.Orders
                .Include(o => o.Product)
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == model.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // ✅ Cập nhật trạng thái kho
            order.WarehouseStatus = model.WarehouseStatus;
            // ⏱ Nếu kho chuyển sang trạng thái hoàn tất giao / đã giao → set DeliveryDate nếu chưa có
            if ((model.WarehouseStatus == WarehouseOrderStatus.DELIVERED
                 || model.WarehouseStatus == WarehouseOrderStatus.COMPLETED)
                && !order.DeliveryDate.HasValue)
            {
                order.DeliveryDate = DateTime.Now;
            }

            // ⚠️ Không trừ kho ở đây — việc xuất kho chỉ thực hiện khi KH xác nhận "Giao thành công"
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Cập nhật trạng thái đơn hàng trong kho thành công!";
            return RedirectToAction(nameof(Index));
        }

        // 🗑️ Xóa đơn hàng
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.Orders.FindAsync(id);
            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ Đã xóa đơn hàng.";
            return RedirectToAction(nameof(Index));
        }
    }
}
