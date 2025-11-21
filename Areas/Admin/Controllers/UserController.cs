using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using WMS.Models;
using WMS.Models.ViewModel;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class UserController : Controller
    {
        // 🔁 ĐỔI IdentityUser → ApplicationUser
        private readonly UserManager<ApplicationUser> _userManager;

        public UserController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // ================== INDEX ==================
        public IActionResult Index(string search)
        {
            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();

                usersQuery = usersQuery.Where(u =>
                    u.UserName.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.FullName.Contains(search) ||
                    u.PhoneNumber.Contains(search)
                );

                ViewBag.SearchKeyword = search;
            }

            var users = usersQuery.ToList();
            return View(users);   // ⚠ View nên dùng @model IEnumerable<ApplicationUser>
        }

        // ================== CREATE ==================
        [HttpGet]
        public IActionResult Create() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(UserViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.Phone
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "✅ Tạo người dùng thành công";
                return RedirectToAction("Index");
            }

            // Ghi lỗi Identity ra view
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return View(model);
        }

        // ================== EDIT ==================
        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var currentUserId = _userManager.GetUserId(User); // ID của user đang đăng nhập

            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "Không thể sửa chính tài khoản của bạn.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new UserEditViewModel
            {
                Id = user.Id,
                Email = user.Email,
                FullName = user.FullName,
                Phone = user.PhoneNumber
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(UserEditViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            // ✅ Chỉ cập nhật Họ tên nếu user có nhập
            if (!string.IsNullOrWhiteSpace(model.FullName))
            {
                user.FullName = model.FullName;
            }

            // ✅ Chỉ cập nhật SĐT nếu user có nhập
            if (!string.IsNullOrWhiteSpace(model.Phone))
            {
                user.PhoneNumber = model.Phone;
            }

            // ✅ Chỉ reset password nếu có nhập mật khẩu mới
            if (!string.IsNullOrWhiteSpace(model.NewPassword))
            {
                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var resetResult = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (!resetResult.Succeeded)
                {
                    foreach (var error in resetResult.Errors)
                        ModelState.AddModelError("", error.Description);

                    return View(model);
                }
            }

            // Lưu thay đổi (FullName / Phone nếu có)
            await _userManager.UpdateAsync(user);

            TempData["SuccessMessage"] = "Cập nhật người dùng thành công";
            return RedirectToAction("Index");
        }



        // ================== DELETE ==================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var currentUserId = _userManager.GetUserId(User); // ID của user đang đăng nhập

            if (id == currentUserId)
            {
                TempData["ErrorMessage"] = "Không thể xóa chính tài khoản của bạn.";
                return RedirectToAction("Index");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "🗑️ Đã xóa người dùng thành công";
            }
            else
            {
                TempData["ErrorMessage"] = "❌ Lỗi khi xóa người dùng.";
            }

            return RedirectToAction("Index");
        }
    }
}
