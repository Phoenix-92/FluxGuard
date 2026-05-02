using Microsoft.AspNetCore.Mvc;

namespace FluxGuard.Api.Controllers
{
    public class TestController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
