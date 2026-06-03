using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Settings")]
    public class SettingsController : Controller
    {
        // GET: Settings
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Settings";
            ViewBag.PageTitle = "Cài đặt hệ thống";
            return View();
        }
    }
}
