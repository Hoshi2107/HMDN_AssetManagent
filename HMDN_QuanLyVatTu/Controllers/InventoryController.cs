using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Inventory")]
    public class InventoryController : Controller
    {
        // GET: Inventory
        public ActionResult Index()
        {
            return View();
        }

        // GET: Inventory/Detail
        public ActionResult Detail()
        {
            return View();
        }
    }
}