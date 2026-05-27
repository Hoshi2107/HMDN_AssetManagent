using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Locations")]
    public class LocationController : Controller
    {
        // GET: Location
        public ActionResult Location()
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