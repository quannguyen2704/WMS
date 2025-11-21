using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WMS.Models;
using WMS.Repository;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class SupplierController : Controller
    {
        private readonly DataContext _dbContext;
        public SupplierController(DataContext dbContext)
        {
            _dbContext = dbContext;
        }

        // Cập nhật: Thêm tham số tìm kiếm
        public IActionResult Index(string search)
        {
            var suppliersQuery = _dbContext.Supplier.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                // Tìm kiếm không phân biệt chữ hoa/chữ thường theo Tên, Địa chỉ, Email hoặc Điện thoại
                suppliersQuery = suppliersQuery.Where(s =>
                    s.Name.Contains(search) ||
                    s.Address.Contains(search) ||
                    s.Email.Contains(search) ||
                    s.Phone.Contains(search)
                );
                ViewBag.SearchKeyword = search; // Lưu từ khóa tìm kiếm để hiển thị lại trên View
            }

            var suppliers = suppliersQuery.ToList();
            return View(suppliers);
        }

        // ... (Giữ nguyên các Action Create, Edit, Delete)

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SupplierModel supplier)
        {
            if (ModelState.IsValid)
            {
                // Dòng này không cần thiết: var supplierModel = new SupplierModel();
                _dbContext.Supplier.Add(supplier);
                _dbContext.SaveChanges();
                TempData["SuccessMessage"] = "Thêm nhà cung cấp thành công";
                return RedirectToAction("Index", "Supplier");
            }
            return View(supplier);
        }
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var supplier = _dbContext.Supplier.Find(id);
            return View(supplier);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(SupplierModel supplier)
        {
            if (ModelState.IsValid)
            {
                _dbContext.Supplier.Update(supplier);
                _dbContext.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật cung cấp thành công";
                return RedirectToAction("Index");
            }
            return View(supplier);
        }


        [HttpPost]
        public IActionResult Delete(int id)
        {
            var supplier = _dbContext.Supplier.Find(id);
            if (supplier != null)
            {
                _dbContext.Supplier.Remove(supplier);
                _dbContext.SaveChanges();
                TempData["SuccessMessage"] = "Xóa nhà cung cấp thành công";
            }
            return RedirectToAction("Index");
        }
    }
}