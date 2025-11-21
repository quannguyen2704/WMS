using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using WMS.Models;
using WMS.Models.ViewModel;
using System.Threading.Tasks;

namespace WMS.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpGet]
        public IActionResult AccessDenied() => View();

        public IActionResult Index() => View();

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.Rememberme,
                lockoutOnFailure: false);

            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "Đăng nhập thành công";
                return RedirectToAction("Index", "Home");
            }

            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không đúng.");
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            TempData["SuccessMessage"] = "Đăng xuất thành công";
            return RedirectToAction("Login", "Account");
        }

        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                PhoneNumber = model.Phone
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                const string defaultRole = "Khách hàng";

                var roleAssignmentResult = await _userManager.AddToRoleAsync(user, defaultRole);

                if (roleAssignmentResult.Succeeded)
                {
                    using (var scope = HttpContext.RequestServices.CreateScope())
                    {
                        var db = scope.ServiceProvider.GetRequiredService<WMS.Repository.DataContext>();

                        if (!db.Customer.Any(c => c.Email == user.Email || c.UserEmail == user.Email))
                        {
                            var customer = new CustomerModel
                            {
                                Name = model.FullName,
                                Address = "",
                                Phone = model.Phone,
                                Email = user.Email,
                                UserEmail = user.Email
                            };

                            db.Customer.Add(customer);
                            db.SaveChanges();
                        }
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    TempData["SuccessMessage"] = "✅ Đăng ký thành công! Bạn đã được gán vai trò Khách hàng.";
                    return RedirectToAction("Index", "Home");
                }
                else
                {
                    await _userManager.DeleteAsync(user);
                    TempData["ErrorMessage"] = "❌ Đăng ký thất bại do lỗi gán vai trò. Vui lòng thử lại.";
                    return RedirectToAction("Register");
                }
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return View(model);
        }
    }
}
