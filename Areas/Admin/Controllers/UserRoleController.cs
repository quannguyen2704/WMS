using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WMS.Models;
using WMS.Models.ViewModel;
using WMS.Repository;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class UserRoleController : Controller
    {
        // ✅ ĐÃ ĐỔI IdentityUser → ApplicationUser
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dbContext;

        public UserRoleController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            DataContext dbContext)
        {
            _dbContext = dbContext;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ================== INDEX ==================
        public async Task<IActionResult> Index(string search)
        {
            var usersQuery = _userManager.Users.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                ViewBag.SearchKeyword = search;

                // 🔍 Tìm theo UserName / Email / FullName / PhoneNumber
                usersQuery = usersQuery.Where(u =>
                    u.UserName.Contains(search) ||
                    u.Email.Contains(search) ||
                    u.FullName.Contains(search) ||
                    u.PhoneNumber.Contains(search)
                );
            }

            var users = usersQuery.ToList();
            var model = new List<UserWithRolesViewModel>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);

                model.Add(new UserWithRolesViewModel
                {
                    // ⚠ Đảm bảo trong UserWithRolesViewModel:
                    // public ApplicationUser User { get; set; }
                    User = user,
                    Roles = roles.ToList()
                });
            }

            return View(model);
        }

        // ================== MANAGE (GET) ==================
        [HttpGet]
        public async Task<IActionResult> Manage(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return NotFound();

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var roles = _roleManager.Roles.ToList();
            var userRoles = await _userManager.GetRolesAsync(user);

            var model = new UserRolesViewModel
            {
                UserId = user.Id,
                UserEmail = user.Email,
                Roles = roles.Select(role => new RoleSelection
                {
                    RoleName = role.Name,
                    IsSelected = userRoles.Contains(role.Name)
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Manage(UserRolesViewModel model)
        {
            // Nếu binding lỗi (thiếu Roles list / UserId) → trả về view + báo lỗi
            if (!ModelState.IsValid)
            {
                TempData["ErrorMessage"] = "Dữ liệu không hợp lệ, vui lòng thử lại.";
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy người dùng.";
                return RedirectToAction("Index");
            }

            // Lấy danh sách role hiện tại của user
            var currentUserRoles = await _userManager.GetRolesAsync(user);

            // Lấy các role được tick trong form
            var selectedRoles = model.Roles?
                .Where(r => r.IsSelected)
                .Select(r => r.RoleName)
                .ToList() ?? new List<string>();

            // Nếu không chọn gì và user cũng không có role nào → coi như không có thay đổi
            if (!selectedRoles.Any() && !currentUserRoles.Any())
            {
                TempData["ErrorMessage"] = "Không có thay đổi nào về vai trò để lưu.";
                return RedirectToAction("Index");
            }

            // Chỉ add những role thực sự tồn tại trong hệ thống
            var rolesToAdd = selectedRoles.Except(currentUserRoles).ToList();
            var rolesToRemove = currentUserRoles.Except(selectedRoles).ToList();

            // ➕ ADD ROLES
            if (rolesToAdd.Any())
            {
                var addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
                if (!addResult.Succeeded)
                {
                    foreach (var error in addResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);

                    TempData["ErrorMessage"] = "Không thể thêm một số vai trò.";
                    // Hiển thị lại view để bạn thấy lỗi
                    return View(model);
                }
            }

            // ➖ REMOVE ROLES
            if (rolesToRemove.Any())
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
                if (!removeResult.Succeeded)
                {
                    foreach (var error in removeResult.Errors)
                        ModelState.AddModelError(string.Empty, error.Description);

                    TempData["ErrorMessage"] = "Không thể gỡ một số vai trò.";
                    return View(model);
                }
            }

            TempData["SuccessMessage"] = "✅ Cập nhật quyền thành công.";
            return RedirectToAction("Index");
        }

    }
}
