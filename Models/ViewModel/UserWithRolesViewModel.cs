using System.Collections.Generic;

namespace WMS.Models.ViewModel
{
    public class UserWithRolesViewModel
    {
        public ApplicationUser User { get; set; }   // ✅ Phải là ApplicationUser
        public List<string> Roles { get; set; }
    }
}
