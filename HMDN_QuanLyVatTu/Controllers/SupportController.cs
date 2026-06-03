using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Support")]
    public class SupportController : Controller
    {
        // GET: Support
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Support";
            ViewBag.PageTitle = "Hỗ trợ & Hướng dẫn sử dụng";
            return View();
        }
    }
}
