using System.Linq;
using System.Web.Mvc;
using HMS.Data;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class ApprovalsController : Controller
    {
        private HospitalAssetDbContext db = new HospitalAssetDbContext();

        // GET: Approvals
        public ActionResult Index()
        {
            ViewBag.ActiveNav = "Approvals";
            return View();
        }

        [HttpGet]
        public JsonResult GetTickets()
        {
            var data = db.Tickets
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new
                {
                    x.Id,
                    x.TicketCode,
                    x.TicketType,
                    x.Status,
                    x.Note,
                    x.CreatedBy,
                    CreatedAt = x.CreatedAt,
                    x.CheckedBy,
                    CheckedAt = x.CheckedAt,
                    x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    TransactionDate = x.TransactionDate
                })
                .ToList()
                .Select(x => new
                {
                    x.Id,
                    x.TicketCode,
                    x.TicketType,
                    x.Status,
                    x.Note,
                    x.CreatedBy,
                    CreatedAt = x.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                    x.CheckedBy,
                    CheckedAt = x.CheckedAt.HasValue ? x.CheckedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt.HasValue ? x.ApprovedAt.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null,
                    TransactionDate = x.TransactionDate.HasValue ? x.TransactionDate.Value.ToString("yyyy-MM-ddTHH:mm:ss") : null
                });
            return Json(data, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetTicketDetails(int ticketId)
        {
            var details = db.Inventories
                .Where(x => x.IdTicket == ticketId)
                .Select(x => new
                {
                    x.Id,
                    ItemName = x.Item != null ? x.Item.Name : "N/A",
                    x.SerialNumber,
                    x.Quantity,
                    x.LifeStatus
                })
                .ToList();
            return Json(details, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        public JsonResult UpdateStatus(int ticketId, string status, string note)
        {
            var ticket = db.Tickets.Find(ticketId);
            if (ticket == null)
            {
                return Json(new { success = false, message = "Không tìm thấy yêu cầu." });
            }

            ticket.Status = status;

            if (status == "APPROVED")
            {
                ticket.ApprovedBy = 1;
                ticket.ApprovedAt = System.DateTime.Now;
            }
            else if (status == "REJECTED")
            {
                ticket.CheckedBy = 1;
                ticket.CheckedAt = System.DateTime.Now;
            }

            db.SaveChanges();
            return Json(new { success = true });
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}