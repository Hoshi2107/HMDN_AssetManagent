using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Maintenance")]
    public class MaintenanceController : Controller
    {
        // GET: Maintenance
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Maintenance";
            ViewBag.PageTitle = "Nhật ký sửa chữa thiết bị";
            return View();
        }
    }
}
