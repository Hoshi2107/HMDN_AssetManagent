using HMDN_QuanLyVatTu.Models;
using HMS.Data;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Web.Http;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/approvals")]
    public class ApprovalsAPIController : ApiController
    {
        // GET api/approvals/GetTickets
        [HttpGet]
        [Route("GetTickets")]
        public IHttpActionResult GetTickets()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var data = db.Database
                        .SqlQuery<ApprovalsListVM>("EXEC sp_Approvals_GetTickets")
                        .ToList();
                    return Ok(data);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // GET api/approvals/GetTicketDetails
        [HttpGet]
        [Route("GetTicketDetails")]
        public IHttpActionResult GetTicketDetails(int ticketId)
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var data = db.Database
                        .SqlQuery<TicketDetailVM>(
                            "EXEC sp_Approvals_GetTicketDetails @TicketId",
                            new SqlParameter("@TicketId", ticketId)
                        )
                        .ToList();
                    return Ok(data);
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // POST api/approvals/UpdateStatus
        [HttpPost]
        [Route("UpdateStatus")]
        public IHttpActionResult UpdateStatus(UpdateStatusRequest request)
        {
            if (request == null)
            {
                return BadRequest("Dữ liệu không hợp lệ.");
            }

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // Check if ticket exists
                    var exists = db.Tickets.Any(t => t.Id == request.TicketId);
                    if (!exists)
                    {
                        return Ok(new { success = false, message = "Không tìm thấy yêu cầu." });
                    }

                    db.Database.ExecuteSqlCommand(
                        "EXEC sp_Approvals_UpdateStatus @TicketId, @Status, @Note",
                        new SqlParameter("@TicketId", request.TicketId),
                        new SqlParameter("@Status", request.Status),
                        new SqlParameter("@Note", (object)request.Note ?? DBNull.Value)
                    );

                    if (request.Items != null)
                    {
                        foreach (var itemInput in request.Items)
                        {
                            var inv = db.Inventories.Find(itemInput.Id);
                            if (inv != null)
                            {
                                inv.ApprovalStatus = (request.Status == "REJECTED" ? "rejected" : (itemInput.IsApproved ? "approved" : "rejected"));
                                inv.ApprovedQuantity = itemInput.ApprovedQuantity;
                                inv.ApprovalNote = itemInput.ApprovalNote;
                                inv.ApprovedBy = 1; // Default Admin User
                                inv.ApprovedAt = DateTime.Now;
                            }
                        }
                        db.SaveChanges();
                    }

                    return Ok(new { success = true });
                }
            }
            catch (Exception ex)
            {
                return Ok(new { success = false, message = ex.Message });
            }
        }
    }

    public class ApprovalsListVM
    {
        public int Id { get; set; }
        public string TicketCode { get; set; }
        public string TicketType { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public int CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public string CreatedByUsername { get; set; }
        public string DepartmentName { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? CheckedBy { get; set; }
        public DateTime? CheckedAt { get; set; }
        public int? ApprovedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? TransactionDate { get; set; }
    }

    public class TicketDetailVM
    {
        public int Id { get; set; }
        public string ItemName { get; set; }
        public string SerialNumber { get; set; }
        public int Quantity { get; set; }
        public string LifeStatus { get; set; }
        public string ApprovalStatus { get; set; }
        public string ApprovalNote { get; set; }
        public int? ApprovedQuantity { get; set; }
    }

    public class UpdateStatusRequest
    {
        public int TicketId { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
        public List<TicketItemApprovalInput> Items { get; set; }
    }

    public class TicketItemApprovalInput
    {
        public int Id { get; set; }
        public int ApprovedQuantity { get; set; }
        public bool IsApproved { get; set; }
        public string ApprovalNote { get; set; }
    }
}