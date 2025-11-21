using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using WMS.Models;
using WMS.Repository;

namespace WMS.Areas.Admin.Controllers
{
    [Authorize(Roles = "Manager,Admin")]
    [Area("Admin")]
    public class DashboardController : Controller
    {
        private readonly DataContext _db;

        public DashboardController(DataContext db)
        {
            _db = db;
        }

        public async Task<IActionResult> Index(string? keyword, DateTime? fromDate, DateTime? toDate)
        {
            // ===== THỐNG KÊ TỔNG QUAN =====
            var totalProducts = await _db.Products.CountAsync();
            var totalProduction = await _db.ProductionOrders.CountAsync();
            var totalOrders = await _db.Orders.CountAsync();
            var totalPurchaseOrders = await _db.PurchaseOrders.CountAsync();
            var totalSuppliers = await _db.Supplier.CountAsync();
            var lowStock = await _db.Products.CountAsync(p => p.ProductQuantity < 10);

            var totalRevenue = await _db.Orders
                .Where(o => o.Status == OrderStatus.DELIVERED_SUCCESS)
                .SumAsync(o => (decimal?)(o.Quantity * o.UnitPrice)) ?? 0m;

            // ===== LỌC DỮ LIỆU NHẬP/XUẤT =====
            IQueryable<WarehouseTransactionModel> query = _db.WarehouseTransaction
                .Include(t => t.Product);

            // 1. Lọc theo Từ khóa (Sản phẩm)
            if (!string.IsNullOrEmpty(keyword))
            {
                var product = await _db.Products
                    .FirstOrDefaultAsync(p => p.ProductName.Contains(keyword));

                if (product != null)
                {
                    // Lọc giao dịch nhập/xuất theo sản phẩm
                    query = query.Where(t => t.ProductId == product.Id);
                    ViewBag.SearchName = product.ProductName;
                    ViewBag.Keyword = keyword;

                    // Tổng doanh thu sản phẩm (cho card info nếu cần)
                    var productRevenue = await _db.Orders
                        .Where(o => o.ProductId == product.Id && o.Status == OrderStatus.DELIVERED_SUCCESS)
                        .SumAsync(o => (decimal?)(o.Quantity * o.UnitPrice)) ?? 0m;
                    ViewBag.ProductRevenue = productRevenue;

                    // Nếu là NGUYÊN VẬT LIỆU → tính TIÊU HAO THEO NGÀY (VNĐ)
                    if (product.ProductType == ProductType.Material)
                    {
                        var usageByDay = await _db.WarehouseTransaction
                            .Where(t => t.ProductId == product.Id && t.TransactionType == "Export")
                            .GroupBy(t => t.TransactionDate.Date)
                            .Select(g => new
                            {
                                Date = g.Key,
                                Cost = g.Sum(x => x.Quantity * x.Product.ProductPrice)
                            })
                            .OrderBy(x => x.Date)
                            .ToListAsync();

                        ViewBag.IsMaterialSearch = true;
                        ViewBag.MaterialUsageDays = usageByDay
                            .Select(x => x.Date.ToString("dd/MM"))
                            .ToList();
                        ViewBag.MaterialUsageData = usageByDay
                            .Select(x => x.Cost)
                            .ToArray();
                    }
                    // Nếu là THÀNH PHẨM → tính DOANH THU THEO NGÀY
                    else
                    {
                        var dailyRevenue = await _db.Orders
                            .Where(o => o.ProductId == product.Id && o.Status == OrderStatus.DELIVERED_SUCCESS)
                            .GroupBy(o => o.OrderDate.Date)
                            .Select(g => new
                            {
                                Date = g.Key,
                                Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                            })
                            .OrderBy(x => x.Date)
                            .ToListAsync();

                        ViewBag.HasProductRevenue = true;
                        ViewBag.ProductRevenueDays = dailyRevenue
                            .Select(x => x.Date.ToString("dd/MM"))
                            .ToList();
                        ViewBag.ProductRevenueData = dailyRevenue
                            .Select(x => x.Revenue)
                            .ToArray();
                    }
                }
                else
                {
                    // Không tìm thấy sản phẩm → không thống kê
                    query = query.Where(t => false);
                    ViewBag.SearchMessage = "❌ Không tìm thấy sản phẩm nào khớp với từ khóa.";
                    ViewBag.Keyword = keyword;
                }
            }

            // 2. Lọc theo Ngày tháng cho giao dịch nhập/xuất
            if (fromDate.HasValue)
            {
                query = query.Where(t => t.TransactionDate >= fromDate.Value);
                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            }

            if (toDate.HasValue)
            {
                var end = toDate.Value.AddDays(1); // bao gồm cả ngày cuối
                query = query.Where(t => t.TransactionDate < end);
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
            }

            // ===== GOM NHÓM THEO NGÀY (NHẬP/XUẤT) =====
            var transactions = await query.ToListAsync();

            var groupedByDate = transactions
                .GroupBy(t => t.TransactionDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    ImportTotal = g.Where(x => x.TransactionType == "Import").Sum(x => x.Quantity),
                    ExportTotal = g.Where(x => x.TransactionType == "Export").Sum(x => x.Quantity),

                    ImportDetails = g.Where(x => x.TransactionType == "Import")
                        .GroupBy(x => x.Product.ProductName)
                        .Select(x => new
                        {
                            Product = x.Key,
                            Unit = x.First().Product.ProductUnit,
                            Quantity = x.Sum(v => v.Quantity),
                            TotalValue = x.Sum(v => v.Quantity * v.Product.ProductPrice)
                        }),

                    ExportDetails = g.Where(x => x.TransactionType == "Export")
                        .GroupBy(x => x.Product.ProductName)
                        .Select(x => new
                        {
                            Product = x.Key,
                            Unit = x.First().Product.ProductUnit,
                            Quantity = x.Sum(v => v.Quantity),
                            TotalValue = x.Sum(v => v.Quantity * v.Product.ProductPrice)
                        })
                })
                .OrderBy(x => x.Date)
                .ToList();

            // ===== DỮ LIỆU BIỂU ĐỒ NHẬP/XUẤT =====
            ViewBag.Days = groupedByDate.Select(d => d.Date.ToString("dd/MM")).ToList();
            ViewBag.ImportData = groupedByDate.Select(d => d.ImportTotal).ToArray();
            ViewBag.ExportData = groupedByDate.Select(d => d.ExportTotal).ToArray();
            ViewBag.ImportDetails = groupedByDate.Select(d => d.ImportDetails).ToList();
            ViewBag.ExportDetails = groupedByDate.Select(d => d.ExportDetails).ToList();

            // ===== BIỂU ĐỒ DOANH THU THEO THÁNG (MẶC ĐỊNH) =====
            var monthlyRevenue = await _db.Orders
                .Where(o => o.Status == OrderStatus.DELIVERED_SUCCESS)
                .GroupBy(o => o.OrderDate.Month)
                .Select(g => new
                {
                    Month = g.Key,
                    Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
                })
                .OrderBy(g => g.Month)
                .ToListAsync();

            var months = Enumerable.Range(1, 12)
                .Select(m => CultureInfo.CurrentCulture.DateTimeFormat.GetAbbreviatedMonthName(m))
                .ToList();

            var revenueData = new decimal[12];
            foreach (var m in monthlyRevenue)
            {
                revenueData[m.Month - 1] = m.Revenue;
            }

            // ===== TÍNH TỔNG TIỀN NGUYÊN VẬT LIỆU TỒN KHO =====
            var materialsInStock = await _db.Products
                .Where(p => p.ProductType == ProductType.Material)
                .Select(p => new
                {
                    ProductId = p.Id,
                    p.ProductName,
                    p.ProductUnit,
                    p.ProductQuantity, // tồn ban đầu
                    UnitPrice = p.ProductPrice,

                    TotalImported = _db.WarehouseTransaction
                        .Where(t => t.ProductId == p.Id && t.TransactionType == "Import")
                        .Sum(t => (decimal?)t.Quantity) ?? 0m,

                    TotalExported = _db.WarehouseTransaction
                        .Where(t => t.ProductId == p.Id && t.TransactionType == "Export")
                        .Sum(t => (decimal?)t.Quantity) ?? 0m,
                })
                .ToListAsync();

            var materialInventory = materialsInStock
                .Select(m => new
                {
                    m.ProductName,
                    m.ProductUnit,
                    Stock = m.ProductQuantity + m.TotalImported - m.TotalExported,
                    m.UnitPrice,
                    TotalValue = (m.ProductQuantity + m.TotalImported - m.TotalExported) * m.UnitPrice
                })
                .Where(m => m.Stock > 0)
                .ToList();

            ViewBag.MaterialInventory = materialInventory;
            ViewBag.MaterialTotalValue = materialInventory.Sum(x => x.TotalValue);

            // ===== DỮ LIỆU GẦN ĐÂY =====
            var recentOrders = await _db.Orders
                .Include(o => o.Product)
                .OrderByDescending(o => o.OrderDate)
                .Take(5)
                .ToListAsync();

            var recentProductions = await _db.ProductionOrders
                .Include(p => p.Product)
                .Include(p => p.Materials).ThenInclude(m => m.Product)
                .OrderByDescending(p => p.StartDate)
                .Take(5)
                .ToListAsync();

            var recentPurchases = await _db.PurchaseOrders
                .Include(p => p.Product)
                .OrderByDescending(p => p.OrderDate)
                .Take(5)
                .ToListAsync();

            // ===== GỬI RA VIEW =====
            ViewBag.Stats = new
            {
                TotalProducts = totalProducts,
                TotalProduction = totalProduction,
                TotalOrders = totalOrders,
                TotalPurchaseOrders = totalPurchaseOrders,
                TotalSuppliers = totalSuppliers,
                LowStock = lowStock,
                TotalRevenue = totalRevenue
            };

            ViewBag.Months = months;
            ViewBag.RevenueData = revenueData;

            ViewBag.RecentOrders = recentOrders;
            ViewBag.RecentProductions = recentProductions;
            ViewBag.RecentPurchases = recentPurchases;

            return View();
        }
    }
}
