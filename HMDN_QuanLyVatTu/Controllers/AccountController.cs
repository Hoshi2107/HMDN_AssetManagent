using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class AccountController : Controller
    {
        // GET: Account/Login
        [HttpGet]
        public ActionResult Login()
        {
            return View();
        }
    }
}
