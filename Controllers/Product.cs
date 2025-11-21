using Microsoft.AspNetCore.Mvc;

namespace WMS.Controllers
{
    public class Product : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
