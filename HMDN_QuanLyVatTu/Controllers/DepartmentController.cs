using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Locations")]
    public class DepartmentController : Controller
    {
        // GET: Location
        public ActionResult Department()
        {
            return View();
        }

        //// GET: Location/Detail
        //public ActionResult Detail()
        //{
        //    return View();
        //}
    }
}