using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Checklists")]
    public class ChecklistsController : Controller
    {
        // GET: Checklists
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Checklists";
            ViewBag.PageTitle = "Lập lịch & Thực hiện Checklist";
            return View();
        }
    }
}
