using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WMS.Models
{
    // 🌟 Trạng thái dành cho khách hàng
    public enum OrderStatus
    {
        [Display(Name = "Bắt đầu đặt hàng")]
        CREATED = 0,

        [Display(Name = "Giao thành công")]
        DELIVERED_SUCCESS = 1,

        [Display(Name = "Giao không thành công")]
        DELIVERED_FAILED = 2
    }

    // 🌟 Trạng thái dành riêng cho kho
    public enum WarehouseOrderStatus
    {
        [Display(Name = "Đơn mới")]
        PENDING = 0,

        [Display(Name = "Đang xử lý")]
        PROCESSING = 1,

        [Display(Name = "Chờ xác nhận")]
        WAIT_CONFIRM = 2,

        [Display(Name = "Đang đóng gói")]
        PACKING = 3,

        [Display(Name = "Đang giao hàng")]
        DELIVERING = 4,

        [Display(Name = "Xuất kho hoàn tất")]
        DELIVERED = 5,

        [Display(Name = "Đã giao")]
        COMPLETED = 6,

        [Display(Name = "Giao hàng không thànhh công")]
        FAILED = 7
    }

    // 🌟 PHƯƠNG THỨC THANH TOÁN MỚI
    public enum PaymentMethod
    {
        [Display(Name = "Thanh toán khi nhận hàng (COD)")]
        COD = 0,

        [Display(Name = "Thanh toán qua Momo")]
        MOMO = 1
    }


    public class OrderModel
    {
        public int Id { get; set; }

        [Required, StringLength(64)]
        [Display(Name = "Mã đơn hàng")]
        public string OrderNumber { get; set; } = default!;

        [Required, ForeignKey("Product")]
        [Display(Name = "Sản phẩm")]
        public int ProductId { get; set; }
        [ValidateNever] public ProductModel? Product { get; set; }

        [Required, ForeignKey("Customer")]
        [Display(Name = "Khách hàng")]
        public int CustomerId { get; set; }
        [ValidateNever] public CustomerModel? Customer { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số lượng")]
        [Display(Name = "Số lượng")]
        public decimal Quantity { get; set; }

        [Required, StringLength(100)]
        [Display(Name = "Đơn vị")]
        public string Unit { get; set; } = default!;

        [StringLength(500)]
        [Display(Name = "Mô tả")]
        public string? Description { get; set; }

        [Display(Name = "Ngày đặt hàng")]
        public DateTime OrderDate { get; set; } = DateTime.Now;

        [Display(Name = "Ngày giao hàng")]
        public DateTime? DeliveryDate { get; set; }

        [Required]
        [Display(Name = "Trạng thái khách hàng")]
        public OrderStatus Status { get; set; } = OrderStatus.CREATED;

        [Required]
        [Display(Name = "Trạng thái kho")]
        public WarehouseOrderStatus WarehouseStatus { get; set; } = WarehouseOrderStatus.PENDING;

        [Required]
        [Display(Name = "Phương thức thanh toán")]
        public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.COD;

        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Đơn giá")]
        public decimal UnitPrice { get; set; }

        [NotMapped]
        [Display(Name = "Thành tiền")]
        public decimal TotalValue => Quantity * UnitPrice;

        // 📝 Snapshot thông tin KH ngay lúc tạo đơn (đáp ứng yêu cầu hiển thị/khóa lịch sử)
        [StringLength(200)] public string? CustomerName { get; set; }
        [StringLength(500)] public string? CustomerAddress { get; set; }
        [StringLength(20)] public string? CustomerPhone { get; set; }
        [StringLength(100)][EmailAddress] public string? CustomerEmail { get; set; }
    }
}
