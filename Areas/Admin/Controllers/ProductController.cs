using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting; // ❗ Thêm thư viện để truy cập wwwroot
using Microsoft.AspNetCore.Http; // ❗ Thêm thư viện để dùng IFormFile
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WMS.Models;
using WMS.Repository;
using X.PagedList;
using X.PagedList.Extensions;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class ProductController : Controller
    {
        private readonly DataContext _dbContext;
        private readonly IWebHostEnvironment _webHostEnvironment; // ❗ Khai báo

        // ⚙️ CHỈNH SỬA CONSTRUCTOR
        public ProductController(DataContext dbContext, IWebHostEnvironment webHostEnvironment)
        {
            _dbContext = dbContext;
            _webHostEnvironment = webHostEnvironment; // ❗ Khởi tạo
        }

        // 📦 Danh sách sản phẩm + Tìm kiếm + Lọc + Phân trang
        public IActionResult Index(string? search, string? typeFilter, int? page)
        {
            int pageSize = 10;
            int pageNumber = page ?? 1;

            var query = _dbContext.Products.AsQueryable();

            // 🔍 Tìm kiếm theo tên, mã, vị trí
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(p =>
                    p.ProductName.Contains(search) ||
                    p.ProductCode.Contains(search) ||
                    p.Location.Contains(search));
            }

            // ⚙️ Lọc theo loại sản phẩm
            if (!string.IsNullOrEmpty(typeFilter))
            {
                if (typeFilter == "FinishedProduct")
                    query = query.Where(p => p.ProductType == ProductType.FinishedProduct);
                else if (typeFilter == "Material")
                    query = query.Where(p => p.ProductType == ProductType.Material);
            }

            // 🔢 Tổng số sản phẩm sau khi lọc
            int totalCount = query.Count();

            // 📜 Sắp xếp & phân trang
            var pagedProducts = query
                .OrderByDescending(p => p.UpdateDate)
                .ToPagedList(pageNumber, pageSize);

            // 🎯 Gửi dữ liệu ra View
            ViewBag.TotalCount = totalCount;
            ViewBag.TypeFilter = typeFilter;
            ViewBag.Search = search;

            return View(pagedProducts);
        }

        // ➕ Tạo mới (GET)
        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        // ➕ Tạo mới (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        // ❗ Thêm tham số IFormFile productImage
        public async Task<IActionResult> Create(ProductModel model, IFormFile? productImage)
        {
            if (!ModelState.IsValid)
                return View(model);

            // 🖼️ XỬ LÝ UPLOAD HÌNH ẢNH
            if (productImage != null)
            {
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }

                string uniqueFileName = Guid.NewGuid().ToString() + "_" + productImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await productImage.CopyToAsync(fileStream);
                }
                // Lưu đường dẫn tương đối (từ thư mục wwwroot) vào Model
                model.ProductImage = "/images/products/" + uniqueFileName;
            }

            model.CreateDate = DateTime.Now;
            model.UpdateDate = DateTime.Now;

            await _dbContext.Products.AddAsync(model);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Thêm sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        // ✏️ Chỉnh sửa (GET)
        [HttpGet]
        public IActionResult Edit(int id)
        {
            var product = _dbContext.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
                return NotFound();

            return View(product);
        }

        // ✏️ Chỉnh sửa (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        // ❗ Thêm tham số IFormFile productImage
        public async Task<IActionResult> Edit(ProductModel model, int id, IFormFile? productImage)
        {
            if (id != model.Id)
                return BadRequest();

            // Giữ lại ProductImage hiện tại trong trường hợp ModelState không hợp lệ
            var productToUpdate = _dbContext.Products.AsNoTracking().FirstOrDefault(p => p.Id == id);
            if (productToUpdate != null && productImage == null)
            {
                model.ProductImage = productToUpdate.ProductImage;
            }


            if (!ModelState.IsValid)
                return View(model);

            var product = _dbContext.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
                return NotFound();

            // 🖼️ XỬ LÝ UPLOAD HÌNH ẢNH MỚI
            if (productImage != null)
            {
                // Xóa ảnh cũ nếu tồn tại
                if (!string.IsNullOrEmpty(product.ProductImage))
                {
                    string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ProductImage.TrimStart('/'));
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                // Lưu ảnh mới
                string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images", "products");
                if (!Directory.Exists(uploadsFolder))
                {
                    Directory.CreateDirectory(uploadsFolder);
                }
                string uniqueFileName = Guid.NewGuid().ToString() + "_" + productImage.FileName;
                string filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await productImage.CopyToAsync(fileStream);
                }
                product.ProductImage = "/images/products/" + uniqueFileName; // Cập nhật đường dẫn mới
            }
            // Nếu người dùng không upload ảnh mới, giữ nguyên ảnh cũ
            else
            {
                // Nếu người dùng không upload ảnh mới, giữ nguyên giá trị ảnh đã có trong DB
                // (Không cần làm gì cả vì `product` đã được fetch từ DB)
            }


            // ✅ Cập nhật thông tin sản phẩm
            product.ProductCode = model.ProductCode;
            product.ProductName = model.ProductName;
            product.ProductUnit = model.ProductUnit;
            product.ProductQuantity = model.ProductQuantity;
            product.Location = model.Location;
            product.ProductDescription = model.ProductDescription;
            product.ProductType = model.ProductType;
            product.ProductPrice = model.ProductPrice; // 💰 cập nhật đơn giá
            product.UpdateDate = DateTime.Now;

            // _dbContext.Entry(product).State = EntityState.Modified; // Không cần nếu dùng FirstOrDefault
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "✅ Cập nhật sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }

        // 🗑️ Xóa sản phẩm
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var product = _dbContext.Products.FirstOrDefault(p => p.Id == id);
            if (product == null)
                return NotFound();

            // 🗑️ Xóa tệp ảnh khỏi thư mục
            if (!string.IsNullOrEmpty(product.ProductImage))
            {
                string filePath = Path.Combine(_webHostEnvironment.WebRootPath, product.ProductImage.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _dbContext.Products.Remove(product);
            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = "🗑️ Đã xóa sản phẩm thành công!";
            return RedirectToAction(nameof(Index));
        }
    }
}