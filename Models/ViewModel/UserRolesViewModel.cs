using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace WMS.Models.ViewModel
{
    public class UserRolesViewModel
    {
        public string UserId { get; set; }

        // ✅ Chỉ dùng để hiển thị, KHÔNG cần validate / bind bắt buộc
        [ValidateNever]                 // <-- thêm dòng này
        public string? UserEmail { get; set; }   // <-- cho phép null

        public List<RoleSelection> Roles { get; set; }
    }
}
