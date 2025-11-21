using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using WMS.Models;

namespace WMS.Repository
{
    public class DataContext : IdentityDbContext<ApplicationUser>
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }

        public DbSet<ProductModel> Products { get; set; }
        public DbSet<WarehouseTransactionModel> WarehouseTransaction { get; set; }
        public DbSet<SupplierModel> Supplier { get; set; }

        // Sản xuất
        public DbSet<ProductionOrderModel> ProductionOrders { get; set; }
        public DbSet<ProductionOrderMaterialModel> ProductionOrderMaterials { get; set; }

        // Khách hàng + Đơn hàng
        public DbSet<CustomerModel> Customer { get; set; }
        public DbSet<OrderModel> Orders { get; set; }

        public DbSet<PurchaseOrderModel> PurchaseOrders { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Định nghĩa precision cho tất cả decimal liên quan số lượng
            builder.Entity<ProductModel>()
                   .Property(p => p.ProductQuantity)
                   .HasPrecision(18, 2);

            builder.Entity<WarehouseTransactionModel>()
                   .Property(t => t.Quantity)
                   .HasPrecision(18, 2);

            builder.Entity<ProductionOrderModel>()
                   .Property(p => p.Quantity)
                   .HasPrecision(18, 2);

            builder.Entity<ProductionOrderMaterialModel>()
                   .Property(m => m.PlannedQuantity)
                   .HasPrecision(18, 2);

            builder.Entity<OrderModel>()
                   .Property(o => o.Quantity)
                   .HasPrecision(18, 2);

            // ✅ Ngắt vòng xóa lan truyền giữa Products và ProductionOrderMaterials
            builder.Entity<ProductionOrderMaterialModel>()
                .HasOne(m => m.Product)
                .WithMany()
                .HasForeignKey(m => m.MaterialId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PurchaseOrderModel>()
                  .Property(p => p.Quantity)
                  .HasPrecision(18, 2);
        }
    }
}
