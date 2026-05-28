using System.Collections.Generic;
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

        // POST: Account/SetLoginSession
        [HttpPost]
        public ActionResult SetLoginSession(List<HMDN_QuanLyVatTu.Models.ModulePermissionDTO> modules, string fullName, int userId = 0)
        {
            Session["UserModules"] = modules;
            Session["FullName"] = fullName;
            Session["UserId"] = userId;
            return Json(new { success = true });
        }

        // GET: Account/Logout
        [HttpGet]
        public ActionResult Logout()
        {
            Session.Clear();
            Session.Abandon();
            return RedirectToAction("Login");
        }
    }
}
