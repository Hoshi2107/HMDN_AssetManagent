using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using HMDN_QuanLyVatTu.Models;
using HMS.Data;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class ApprovalsController : Controller
    {
        // GET: Approvals
        public ActionResult Index()
        {
            using (var db = new HospitalAssetDbContext())
            {
                var approvals = db.Approvals.OrderBy(a => a.Id).ToList();
                return View(approvals);
            }
        }
    }
}