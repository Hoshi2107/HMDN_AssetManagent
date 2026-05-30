using System;
using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Alerts")]
    public class AlertsController : Controller
    {
        // GET: Alerts
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Alerts";
            ViewBag.PageTitle = "Trung tâm Cảnh báo & Thông báo";
            return View();
        }
    }
}
