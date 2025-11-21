using System.ComponentModel.DataAnnotations;

namespace WMS.Models
{
    public class CustomerModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập tên khách hàng")]
        [StringLength(200)]
        public string Name { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập địa chỉ khách hàng")]
        [StringLength(500)]
        public string Address { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại khách hàng")]
        [StringLength(20)]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng nhập email khách hàng")]
        [StringLength(100)]
        [EmailAddress]
        public string Email { get; set; }

        // 🔗 Liên kết với tài khoản đăng nhập (thường trùng Email đăng nhập)
        // Nếu bạn dùng email làm User.Identity.Name, có thể bỏ qua trường này.
        [StringLength(100)]
        [EmailAddress]
        public string? UserEmail { get; set; }
    }
}
