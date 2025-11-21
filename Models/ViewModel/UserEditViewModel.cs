using System.ComponentModel.DataAnnotations;

namespace WMS.Models.ViewModel
{
    public class UserEditViewModel
    {
        public string Id { get; set; }

        [Required] // Email vẫn required vì luôn có sẵn, không cho sửa
        [EmailAddress]
        [Display(Name = "Email đăng nhập")]
        public string Email { get; set; }

        // ❌ Bỏ Required – cho phép để trống
        [StringLength(100)]
        [Display(Name = "Họ và tên")]
        public string? FullName { get; set; }

        // ❌ Bỏ Required – cho phép để trống
        [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
        [Display(Name = "Số điện thoại")]
        public string? Phone { get; set; }

        // ❌ Không Required – chỉ đổi pass khi nhập
        [DataType(DataType.Password)]
        [Display(Name = "Mật khẩu mới (bỏ trống nếu không đổi)")]
        public string? NewPassword { get; set; }
    }
}
