using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using WMS.Models;
using WMS.Repository;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Khách hàng,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class OrderController : Controller
    {
        private readonly DataContext _db;
        private readonly UserManager<ApplicationUser> _userManager; // ✅ ĐỔI IdentityUser → ApplicationUser

        public OrderController(DataContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 📋 Danh sách đơn hàng
        // 📋 Danh sách đơn hàng
        public async Task<IActionResult> Index(string? keyword, DateTime? fromDate, DateTime? toDate)
        {
            var userEmail = User.Identity?.Name;

            var query = _db.Orders
                .Include(o => o.Product)
                .Include(o => o.Customer)
                .OrderByDescending(o => o.OrderDate)
                .AsQueryable();

            // ❌ BỎ ĐI: if (!User.IsInRole("Admin")) ...

            // ✅ CHỈ KHÁCH HÀNG bị lọc đơn của chính họ
            if (User.IsInRole("Khách hàng"))
            {
                query = query.Where(o =>
                    o.CustomerEmail == userEmail ||
                    o.Customer.Email == userEmail
                );
            }
            // 👉 Admin, Manager, Nhân viên kho: không lọc, thấy tất cả đơn

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(o => o.OrderNumber.Contains(keyword) ||
                                         o.Product.ProductName.Contains(keyword) ||
                                         o.CustomerName.Contains(keyword));

            if (fromDate.HasValue)
                query = query.Where(o => o.OrderDate >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(o => o.OrderDate <= toDate.Value);

            ViewBag.Keyword = keyword;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            var orders = await query.ToListAsync();
            return View(orders);
        }


        // ➕ GET: Tạo mới đơn hàng
        [HttpGet]
        public IActionResult Create()
        {
            var userEmail = User.Identity?.Name;

            ViewBag.Products = _db.Products
                .Where(p => p.ProductType == ProductType.FinishedProduct)
                .ToList();

            ViewBag.Customers = _db.Customer.ToList();

            var orderNumber = GenerateOrderNumber();
            var model = new OrderModel { OrderNumber = orderNumber };

            // Nếu user là khách hàng → tự động đổ thông tin
            var currentCustomer = _db.Customer.FirstOrDefault(c => c.Email == userEmail || c.UserEmail == userEmail);
            if (User.IsInRole("Khách hàng") && currentCustomer != null)
            {
                model.CustomerId = currentCustomer.Id;
                model.CustomerName = currentCustomer.Name;
                model.CustomerAddress = currentCustomer.Address;
                model.CustomerPhone = currentCustomer.Phone;
                model.CustomerEmail = currentCustomer.Email;
            }

            return View(model);
        }

        // ➕ POST: Tạo đơn hàng
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderModel model)
        {
            ModelState.Remove("Product");
            ModelState.Remove("Customer");

            // Load lại dropdown để nếu có lỗi thì View vẫn đủ dữ liệu
            ViewBag.Products = _db.Products
                .Where(p => p.ProductType == ProductType.FinishedProduct)
                .ToList();
            ViewBag.Customers = _db.Customer.ToList();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            // 🔎 Kiểm tra tồn kho sản phẩm trước khi tạo đơn
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == model.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Không tìm thấy sản phẩm.");
                TempData["ErrorMessage"] = "❌ Không tìm thấy sản phẩm.";
                return View(model);
            }

            // Tính tồn kho thực tế: tồn cơ sở + tổng nhập - tổng xuất
            var importQty = await _db.WarehouseTransaction
                .Where(t => t.ProductId == product.Id && t.TransactionType == "Import")
                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            var exportQty = await _db.WarehouseTransaction
                .Where(t => t.ProductId == product.Id && t.TransactionType == "Export")
                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            var currentStock = product.ProductQuantity + importQty - exportQty;

            // 🚫 Nếu hết hàng hoặc không đủ tồn kho → không cho đặt
            if (currentStock <= 0)
            {
                ModelState.AddModelError("ProductId", $"Sản phẩm \"{product.ProductName}\" hiện đang hết hàng.");
                TempData["ErrorMessage"] = $"❌ Sản phẩm \"{product.ProductName}\" đã hết hàng, không thể tạo đơn.";
                return View(model);
            }

            if (model.Quantity > currentStock)
            {
                ModelState.AddModelError("Quantity",
                    $"Số lượng đặt ({model.Quantity} {product.ProductUnit}) vượt quá tồn kho hiện tại ({currentStock} {product.ProductUnit}).");
                TempData["ErrorMessage"] =
                    $"❌ Không đủ tồn kho. Hiện chỉ còn {currentStock} {product.ProductUnit} trong kho.";
                return View(model);
            }

            // 📦 Sinh mã đơn hàng nếu chưa có
            if (string.IsNullOrEmpty(model.OrderNumber))
                model.OrderNumber = GenerateOrderNumber();

            model.OrderDate = DateTime.Now;
            model.WarehouseStatus = WarehouseOrderStatus.PENDING;
            model.Unit = string.IsNullOrWhiteSpace(model.Unit) ? product.ProductUnit : model.Unit;
            if (model.UnitPrice <= 0)
                model.UnitPrice = product.ProductPrice;

            // 🟦 Lấy email người dùng hiện tại
            var userEmail = User.Identity?.Name;

            // 🟦 Tìm khách hàng đã có trong DB
            var existingCustomer = await _db.Customer
                .FirstOrDefaultAsync(c => c.Email == userEmail || c.UserEmail == userEmail);

            if (User.IsInRole("Khách hàng"))
            {
                if (existingCustomer != null)
                {
                    existingCustomer.Name = string.IsNullOrWhiteSpace(existingCustomer.Name) ? model.CustomerName : existingCustomer.Name;
                    existingCustomer.Address = string.IsNullOrWhiteSpace(existingCustomer.Address) ? model.CustomerAddress : existingCustomer.Address;
                    existingCustomer.Phone = string.IsNullOrWhiteSpace(existingCustomer.Phone) ? model.CustomerPhone : existingCustomer.Phone;

                    _db.Customer.Update(existingCustomer);
                    await _db.SaveChangesAsync();

                    model.CustomerId = existingCustomer.Id;
                    model.CustomerName = existingCustomer.Name;
                    model.CustomerAddress = existingCustomer.Address;
                    model.CustomerPhone = existingCustomer.Phone;
                    model.CustomerEmail = existingCustomer.Email;
                }
                else
                {
                    var newCustomer = new CustomerModel
                    {
                        Name = model.CustomerName ?? userEmail,
                        Address = model.CustomerAddress ?? "",
                        Phone = model.CustomerPhone ?? "",
                        Email = userEmail,
                        UserEmail = userEmail
                    };
                    _db.Customer.Add(newCustomer);
                    await _db.SaveChangesAsync();

                    model.CustomerId = newCustomer.Id;
                    model.CustomerName = newCustomer.Name;
                    model.CustomerAddress = newCustomer.Address;
                    model.CustomerPhone = newCustomer.Phone;
                    model.CustomerEmail = newCustomer.Email;
                }
            }

            _db.Orders.Add(model);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = $"✅ Tạo đơn hàng {model.OrderNumber} thành công!";
            return RedirectToAction(nameof(Index));
        }


        // 📦 Sinh mã đơn hàng tự động
        private string GenerateOrderNumber()
        {
            string prefix = "DH";
            string datePart = DateTime.Now.ToString("yyyyMMdd");

            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            int countToday = _db.Orders
                .Where(o => o.OrderDate >= today && o.OrderDate < tomorrow)
                .Count() + 1;

            string serial = countToday.ToString("D4");
            return $"{prefix}-{datePart}-{serial}";
        }

        // ✏️ Sửa đơn hàng (GET)
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

            // ⚙️ Bỏ toàn bộ kiểm tra quyền
            ViewBag.Products = _db.Products.ToList();
            ViewBag.Customers = _db.Customer.ToList();
            return View(order);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(OrderModel model)
        {
            // Lấy đơn hàng cũ để so sánh trạng thái
            var orderOld = await _db.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == model.Id);

            if (orderOld == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // ⚡ Kiểm tra xem trạng thái có đổi sang "Giao hàng thành công" không
            bool justDelivered =
                orderOld.Status != model.Status &&
                model.Status == OrderStatus.DELIVERED_SUCCESS;

            // Lấy entity để cập nhật
            var order = await _db.Orders
                .Include(o => o.Customer)
                .FirstOrDefaultAsync(o => o.Id == model.Id);

            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật thông tin đơn hàng
            order.ProductId = model.ProductId;
            order.Quantity = model.Quantity;
            order.Unit = model.Unit;
            order.UnitPrice = model.UnitPrice;
            order.Description = model.Description;
            order.DeliveryDate = model.DeliveryDate;
            order.Status = model.Status;
            order.PaymentMethod = model.PaymentMethod;

            // Cập nhật khách hàng snapshot
            var orderCustomer = await _db.Customer.FindAsync(order.CustomerId);
            if (orderCustomer != null)
            {
                orderCustomer.Name = string.IsNullOrWhiteSpace(model.CustomerName) ? orderCustomer.Name : model.CustomerName;
                orderCustomer.Address = string.IsNullOrWhiteSpace(model.CustomerAddress) ? orderCustomer.Address : model.CustomerAddress;
                orderCustomer.Phone = string.IsNullOrWhiteSpace(model.CustomerPhone) ? orderCustomer.Phone : model.CustomerPhone;

                order.CustomerName = orderCustomer.Name;
                order.CustomerAddress = orderCustomer.Address;
                order.CustomerPhone = orderCustomer.Phone;
                order.CustomerEmail = orderCustomer.Email;

                _db.Customer.Update(orderCustomer);
            }

            // ⭐⭐⭐ NẾU ĐƠN VỪA CHUYỂN SANG GIAO HÀNG THÀNH CÔNG → TỰ ĐỘNG XUẤT KHO ⭐⭐⭐
            if (justDelivered && order.WarehouseStatus != WarehouseOrderStatus.DELIVERED)
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == order.ProductId);
                if (product == null)
                {
                    TempData["ErrorMessage"] = "❌ Không tìm thấy sản phẩm.";
                    return RedirectToAction(nameof(Index));
                }

                // Lấy tồn kho thực tế (theo logic GetInventoryStock)
                var stockResult = new WarehouseController(_db).GetInventoryStock(product.Id) as JsonResult;
                decimal currentStock = 0;

                if (stockResult?.Value is { } jsonValue)
                {
                    var prop = jsonValue.GetType().GetProperty("stock");
                    if (prop != null)
                        currentStock = Convert.ToDecimal(prop.GetValue(jsonValue));
                }

                if (order.Quantity > currentStock)
                {
                    TempData["ErrorMessage"] =
                        $"❌ Không đủ tồn kho để xuất kho theo đơn {order.OrderNumber}. Tồn: {currentStock} {product.ProductUnit}.";
                    return RedirectToAction(nameof(Index));
                }
                order.DeliveryDate = DateTime.Now;

                // 🎉 Tạo phiếu xuất kho
                var exportTrans = new WarehouseTransactionModel
                {
                    ProductId = order.ProductId,
                    Quantity = order.Quantity,
                    TransactionDate = DateTime.Now,
                    TransactionType = "Export",
                    Unit = product.ProductUnit,
                    UnitPrice = product.ProductPrice,
                    Notes = $"Xuất kho theo đơn hàng {order.OrderNumber}"
                };

                _db.WarehouseTransaction.Add(exportTrans);

                // Trừ tồn kho cơ sở
                product.ProductQuantity -= order.Quantity;
                _db.Products.Update(product);

                // Cập nhật trạng thái kho
                order.WarehouseStatus = WarehouseOrderStatus.DELIVERED;
            }

            await _db.SaveChangesAsync();
            TempData["SuccessMessage"] = "✅ Cập nhật đơn hàng thành công!";
            return RedirectToAction(nameof(Index));
        }


        // 🗑️ Xóa đơn hàng
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var order = await _db.Orders.Include(o => o.Customer).FirstOrDefaultAsync(o => o.Id == id);
            if (order == null)
            {
                TempData["ErrorMessage"] = "❌ Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // ⚙️ Không kiểm tra quyền nữa
            _db.Orders.Remove(order);
            await _db.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ Đã xóa đơn hàng.";
            return RedirectToAction(nameof(Index));
        }

        // 📦 API: Lấy tồn kho + giá
        [HttpGet]
        public async Task<IActionResult> GetStockByProductId(int id)
        {
            var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id);
            if (product == null)
                return Json(new { stock = 0, unit = "", price = 0, image = "" });

            var importQty = await _db.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Import")
                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            var exportQty = await _db.WarehouseTransaction
                .Where(t => t.ProductId == id && t.TransactionType == "Export")
                .SumAsync(t => (decimal?)t.Quantity) ?? 0m;

            var stock = product.ProductQuantity + importQty - exportQty;

            return Json(new
            {
                stock,
                unit = product.ProductUnit,
                price = product.ProductPrice,
                image = product.ProductImage
            });
        }
    }
}
