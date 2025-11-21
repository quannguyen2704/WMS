using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WMS.Models
{
    public enum PurchaseStatus
    {
        CREATED = 0,
        ORDERED = 1,
        RECEIVED = 2,
        CANCELED = 3
    }

    public class PurchaseOrderModel
    {
        public int Id { get; set; }

        // ❌ Gỡ Required
        [StringLength(64)]
        public string? PurchaseNumber { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nguyên liệu")]
        [ForeignKey("Product")]
        public int ProductId { get; set; }

        [ValidateNever]
        public ProductModel Product { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nhà cung cấp")]
        [ForeignKey("Supplier")]
        public int SupplierId { get; set; }

        [ValidateNever]
        public SupplierModel Supplier { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng")]
        public decimal Quantity { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đơn vị")]
        [StringLength(100)]
        public string Unit { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập đơn giá")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        public decimal TotalValue => Quantity * UnitPrice;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime OrderDate { get; set; } = DateTime.Now;

        public DateTime? ReceivedDate { get; set; }

        public PurchaseStatus Status { get; set; } = PurchaseStatus.CREATED;
    }
}
