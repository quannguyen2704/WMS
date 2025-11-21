using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WMS.Models
{
    public enum ProductionStatus
    {
        CREATED,       // Khởi tạo
        IN_PROGRESS,   // Đang sản xuất
        DONE           // Hoàn thành
    }

    public class ProductionOrderModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã lệnh sản xuất")]
        [StringLength(64)]
        public string OrderNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng chọn sản phẩm thành phẩm")]
        [ForeignKey("Product")]
        public int ProductId { get; set; }

        [ValidateNever]
        public ProductModel? Product { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng sản xuất")]
        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "Số lượng phải >= 0")]
        public decimal Quantity { get; set; }

        // 💰 Đơn giá và thành tiền
        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "Đơn giá phải >= 0")]
        [Display(Name = "Đơn giá (VNĐ)")]
        public decimal UnitPrice { get; set; } = 0;

        [NotMapped]
        [Display(Name = "Thành tiền (VNĐ)")]
        public decimal TotalValue => Quantity * UnitPrice;

        // 🕒 Ngày bắt đầu
        [DataType(DataType.Date)]
        [Display(Name = "Ngày bắt đầu")]
        public DateTime StartDate { get; set; } = DateTime.Now;

        // 📅 Ngày kết thúc dự kiến (người dùng nhập khi tạo lệnh)
        [DataType(DataType.Date)]
        [Display(Name = "Ngày kết thúc dự kiến")]
        public DateTime? PlannedEndDate { get; set; }

        // ✅ Ngày hoàn thành thực tế (tự động lưu khi Status = DONE)
        [DataType(DataType.DateTime)]
        [Display(Name = "Ngày hoàn thành thực tế")]
        public DateTime? EndDate { get; set; }

        // 📝 Ghi chú
        [StringLength(500)]
        public string? Notes { get; set; }

        // 📊 Trạng thái
        [Required]
        public ProductionStatus Status { get; set; } = ProductionStatus.CREATED;

        // 🔗 Danh sách nguyên liệu
        [ValidateNever]
        public ICollection<ProductionOrderMaterialModel> Materials { get; set; } = new List<ProductionOrderMaterialModel>();
    }

    public class ProductionOrderMaterialModel
    {
        public int Id { get; set; }

        [ForeignKey("ProductionOrder")]
        public int ProductionOrderId { get; set; }

        [ValidateNever]
        public ProductionOrderModel? ProductionOrder { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn nguyên liệu")]
        [ForeignKey("Product")]
        public int MaterialId { get; set; }

        [ValidateNever]
        public ProductModel? Product { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng nguyên liệu")]
        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "Số lượng phải >= 0")]
        public decimal PlannedQuantity { get; set; }

        [StringLength(50)]
        [Required(ErrorMessage = "Vui lòng nhập đơn vị")]
        public string Unit { get; set; } = string.Empty;

        [Required(ErrorMessage = "Vui lòng nhập đơn giá nguyên liệu")]
        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "Đơn giá phải >= 0")]
        public decimal UnitPrice { get; set; }
    }
}
