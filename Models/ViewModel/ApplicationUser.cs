using Microsoft.AspNetCore.Identity;

namespace WMS.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }
    }
}
