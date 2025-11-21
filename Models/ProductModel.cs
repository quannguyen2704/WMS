using System.ComponentModel.DataAnnotations;

namespace WMS.Models
{
    public enum ProductType
    {
        FinishedProduct, // Thành phẩm
        Material         // Nguyên liệu
    }

    public class ProductModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mã sản phẩm")]
        [StringLength(100)]
        public string ProductCode { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên sản phẩm")]
        [StringLength(100)]
        public string ProductName { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập mô tả sản phẩm")]
        [StringLength(200)]
        public string ProductDescription { get; set; }

        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "Số lượng sản phẩm phải >= 0")]
        public decimal ProductQuantity { get; set; }

        [Required]
        [StringLength(100)]
        public string ProductUnit { get; set; }

        [Required]
        [StringLength(200)]
        public string Location { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn loại sản phẩm")]
        public ProductType ProductType { get; set; }
        [Display(Name = "Hình ảnh sản phẩm")]
        public string? ProductImage { get; set; } // Cho phép NULL nếu không bắt buộc

        // 🆕 Thêm đơn giá (theo đơn vị đo lường)
        [Required(ErrorMessage = "Vui lòng nhập đơn giá sản phẩm")]
        [Range(typeof(decimal), "0", "9999999999", ErrorMessage = "Đơn giá phải >= 0")]
        [Display(Name = "Đơn giá (VNĐ / đơn vị)")]
        public decimal ProductPrice { get; set; }

        [DataType(DataType.DateTime)]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [DataType(DataType.DateTime)]
        public DateTime UpdateDate { get; set; } = DateTime.Now;
    }
}
