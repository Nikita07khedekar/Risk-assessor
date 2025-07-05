using Microsoft.AspNetCore.Mvc;

namespace Risk_assessor.Controllers
{
    public class RiskanalyticsController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
