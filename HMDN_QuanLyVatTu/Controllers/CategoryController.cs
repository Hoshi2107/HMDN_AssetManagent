using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    [CustomAuthorize("Catalog")]
    public class CategoryController : Controller
    {
        //// GET: Category
        public ActionResult Category()
        {
            return View();
        }

        //// GET: Category/Detail
        //public ActionResult Detail()
        //{
        //    return View();
        //}
    }
}