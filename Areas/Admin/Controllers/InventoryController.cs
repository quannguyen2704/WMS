using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WMS.Models;
using WMS.Repository;
using ClosedXML.Excel;
using System.Data;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin,Nhân viên kho,Manager")]
    [Area("Admin")]
    public class InventoryController : Controller
    {
        private readonly DataContext _dbContext;

        public InventoryController(DataContext dbContext)
        {
            _dbContext = dbContext;
        }

        // ==========================
        // 📦 Hiển thị danh sách tồn kho
        // ==========================
        public async Task<IActionResult> Index()
        {
            var products = await _dbContext.Products.ToListAsync();
            var inventoryList = new List<InventoryViewModel>();

            foreach (var product in products)
            {
                var totalImported = await _dbContext.WarehouseTransaction
                    .Where(t => t.ProductId == product.Id && t.TransactionType == "Import")
                    .SumAsync(t => (decimal?)t.Quantity) ?? 0;

                var totalExported = await _dbContext.WarehouseTransaction
                    .Where(t => t.ProductId == product.Id && t.TransactionType == "Export")
                    .SumAsync(t => (decimal?)t.Quantity) ?? 0;

                var stock = product.ProductQuantity + totalImported - totalExported;

                var totalImportValue = totalImported * product.ProductPrice;
                var totalExportValue = totalExported * product.ProductPrice;
                var totalStockValue = stock * product.ProductPrice;

                inventoryList.Add(new InventoryViewModel
                {
                    ProductId = product.Id,
                    ProductName = product.ProductName,
                    ProductTypeName = product.ProductType == 0 ? "Thành phẩm" : "Nguyên liệu",
                    Unit = product.ProductUnit,
                    ProductPrice = product.ProductPrice,
                    InitialQuantity = product.ProductQuantity,
                    TotalImported = totalImported,
                    TotalExported = totalExported,
                    Stock = stock,
                    TotalImportValue = totalImportValue,
                    TotalExportValue = totalExportValue,
                    TotalStockValue = totalStockValue
                });
            }

            return View(inventoryList);
        }

        // ==========================
        // 📤 Xuất file Excel tồn kho
        // ==========================
        [HttpGet]
        public async Task<IActionResult> ExportToExcel()
        {
            var products = await _dbContext.Products.ToListAsync();
            var data = new List<InventoryViewModel>();

            foreach (var p in products)
            {
                var totalImported = await _dbContext.WarehouseTransaction
                    .Where(t => t.ProductId == p.Id && t.TransactionType == "Import")
                    .SumAsync(t => (decimal?)t.Quantity) ?? 0;

                var totalExported = await _dbContext.WarehouseTransaction
                    .Where(t => t.ProductId == p.Id && t.TransactionType == "Export")
                    .SumAsync(t => (decimal?)t.Quantity) ?? 0;

                var stock = p.ProductQuantity + totalImported - totalExported;

                data.Add(new InventoryViewModel
                {
                    ProductName = p.ProductName,
                    ProductTypeName = p.ProductType == 0 ? "Thành phẩm" : "Nguyên liệu",
                    Unit = p.ProductUnit,
                    ProductPrice = p.ProductPrice,
                    TotalImported = totalImported,
                    TotalExported = totalExported,
                    Stock = stock,
                    TotalImportValue = totalImported * p.ProductPrice,
                    TotalExportValue = totalExported * p.ProductPrice,
                    TotalStockValue = stock * p.ProductPrice
                });
            }

            using (var workbook = new XLWorkbook())
            {
                var ws = workbook.Worksheets.Add("Báo cáo tồn kho");

                // Tiêu đề bảng
                ws.Cell(1, 1).Value = "STT";
                ws.Cell(1, 2).Value = "Sản phẩm";
                ws.Cell(1, 3).Value = "Loại";
                ws.Cell(1, 4).Value = "Đơn vị";
                ws.Cell(1, 5).Value = "Đơn giá (VNĐ)";
                ws.Cell(1, 6).Value = "Tổng nhập";
                ws.Cell(1, 7).Value = "Tổng xuất";
                ws.Cell(1, 8).Value = "Tồn kho";
                ws.Cell(1, 9).Value = "Giá trị nhập (VNĐ)";
                ws.Cell(1, 10).Value = "Giá trị xuất (VNĐ)";
                ws.Cell(1, 11).Value = "Giá trị tồn kho (VNĐ)";

                int row = 2;
                int stt = 1;

                foreach (var item in data)
                {
                    ws.Cell(row, 1).Value = stt++;
                    ws.Cell(row, 2).Value = item.ProductName;
                    ws.Cell(row, 3).Value = item.ProductTypeName;
                    ws.Cell(row, 4).Value = item.Unit;
                    ws.Cell(row, 5).Value = item.ProductPrice;
                    ws.Cell(row, 6).Value = item.TotalImported;
                    ws.Cell(row, 7).Value = item.TotalExported;
                    ws.Cell(row, 8).Value = item.Stock;
                    ws.Cell(row, 9).Value = item.TotalImportValue;
                    ws.Cell(row, 10).Value = item.TotalExportValue;
                    ws.Cell(row, 11).Value = item.TotalStockValue;
                    row++;
                }

                // Định dạng
                ws.Columns().AdjustToContents();
                ws.Range("A1:K1").Style.Fill.BackgroundColor = XLColor.LightBlue;
                ws.Range("A1:K1").Style.Font.Bold = true;
                ws.Range("A1:K1").Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                // Xuất file
                using (var stream = new MemoryStream())
                {
                    workbook.SaveAs(stream);
                    var content = stream.ToArray();
                    return File(content,
                        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        "InventoryReport.xlsx");
                }
            }
        }
    }
}
