using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WMS.Models;
using WMS.Repository;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Khách hàng,Admin,Manager")]
    [Area("Admin")]
    public class CustomerController : Controller
    {
        private readonly DataContext _db;
        private readonly UserManager<ApplicationUser> _userManager; // ✅ ĐỔI IdentityUser → ApplicationUser

        public CustomerController(DataContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // 📋 Danh sách khách hàng (chỉ hiện những người còn role "Khách hàng")
        public IActionResult Index()
        {
            var customers = _db.Customer.ToList();
            var validCustomers = new List<CustomerModel>();

            foreach (var c in customers)
            {
                var email = c.UserEmail ?? c.Email;
                var user = _userManager.Users.FirstOrDefault(u => u.Email == email);
                if (user != null)
                {
                    var roles = _userManager.GetRolesAsync(user).Result;
                    if (roles.Contains("Khách hàng"))
                    {
                        validCustomers.Add(c);
                    }
                    else
                    {
                        // Nếu role không còn là Khách hàng → xóa khỏi danh sách
                        _db.Customer.Remove(c);
                        _db.SaveChanges();
                    }
                }
            }

            return View(validCustomers);
        }

        // ➕ Tạo mới khách hàng (Admin/Manager)
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult Create() => View();

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult Create(CustomerModel customer)
        {
            if (!ModelState.IsValid) return View(customer);

            customer.UserEmail = customer.Email;
            _db.Customer.Add(customer);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "✅ Thêm khách hàng thành công!";
            return RedirectToAction("Index");
        }

        // ✏️ Chỉnh sửa
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult Edit(int id)
        {
            var customer = _db.Customer.Find(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(CustomerModel customer)
        {
            if (!ModelState.IsValid) return View(customer);

            _db.Customer.Update(customer);
            _db.SaveChanges();

            TempData["SuccessMessage"] = "✅ Cập nhật khách hàng thành công!";
            return RedirectToAction("Index");
        }

        // 🗑️ Xóa
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public IActionResult Delete(int id)
        {
            var customer = _db.Customer.Find(id);
            if (customer != null)
            {
                _db.Customer.Remove(customer);
                _db.SaveChanges();
                TempData["SuccessMessage"] = "🗑️ Đã xóa khách hàng!";
            }
            return RedirectToAction("Index");
        }
    }
}
