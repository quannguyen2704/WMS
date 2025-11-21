using DocumentFormat.OpenXml.Office2010.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using WMS.Repository;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class RoleController : Controller
    {

        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly DataContext _dbContext;

        public RoleController(DataContext dbContext, RoleManager<IdentityRole> roleManager)
        {
            _dbContext = dbContext;
            _roleManager = roleManager;
        }

        // Cập nhật: Thêm tham số tìm kiếm (search)
        public IActionResult Index(string search)
        {
            var rolesQuery = _roleManager.Roles.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                // Lọc vai trò theo tên (Role Name)
                rolesQuery = rolesQuery.Where(r => r.Name.Contains(search));
                ViewBag.SearchKeyword = search; // Lưu từ khóa tìm kiếm để hiển thị lại trên View
            }

            var roles = rolesQuery.ToList();
            return View(roles);
        }

        // ... (Giữ nguyên các Action Create, Edit, Delete)

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string Name)
        {
            if (!string.IsNullOrWhiteSpace(Name))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(Name));
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Tạo vai trò thành công.";
                    return RedirectToAction("Index");
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
            {
                await _roleManager.DeleteAsync(role);
                TempData["SuccessMessage"] = "Xóa vai trò thành công.";
            }
            return RedirectToAction("Index");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy vai trò.";
                return RedirectToAction("Index");
            }
            return View(role);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string name)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role != null)
            {
                role.Name = name;
                var result = await _roleManager.UpdateAsync(role);
                if (result.Succeeded)
                {
                    TempData["SuccessMessage"] = "Cập nhật vai trò thành công.";
                    return RedirectToAction("Index");
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }
            }
            // Nếu không thành công, role vẫn được truyền lại cho View
            return View(role);
        }
    }
}