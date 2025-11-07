using Microsoft.AspNetCore.Mvc;

namespace SmartRoute.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Map()
        {
            return View();
        }
    }
}