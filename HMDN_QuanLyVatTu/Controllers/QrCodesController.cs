using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("QrCodes")]
    public class QrCodesController : Controller
    {
        // GET: QrCodes
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "QrCodes";
            ViewBag.PageTitle = "Quản lý QR Code";
            return View();
        }
    }
}
