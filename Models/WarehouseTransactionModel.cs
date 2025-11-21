using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WMS.Models
{
    public class WarehouseTransactionModel
    {
        public int Id { get; set; }

        [Required]
        [ForeignKey("Product")]
        public int ProductId { get; set; }
        public ProductModel Product { get; set; }

        public int? SupplierId { get; set; }
        public SupplierModel Supplier { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng sản phẩm")]
        [Range(typeof(decimal), "0.01", "9999999999", ErrorMessage = "Số lượng phải lớn hơn 0")]
        public decimal Quantity { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.Now;

        [StringLength(50)]
        [Required(ErrorMessage = "Vui lòng chọn loại giao dịch")]
        public string TransactionType { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập ghi chú.")]
        [StringLength(200)]
        public string Notes { get; set; }

        [StringLength(100)]
        [Required(ErrorMessage = "Vui lòng nhập đơn vị")]
        public string Unit { get; set; }

        // 🆕 Gợi ý đơn giá (có thể sửa)
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, 999999999, ErrorMessage = "Đơn giá không hợp lệ")]
        public decimal UnitPrice { get; set; }

        // 🧾 Tổng giá trị (tự tính: Quantity * UnitPrice)
        [NotMapped]
        public decimal TotalValue => Quantity * UnitPrice;
    }
}
