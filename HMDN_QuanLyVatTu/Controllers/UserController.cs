using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using HMS.Data;
using HMS.Models.Auth;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class UserController : Controller
    {
        private HospitalAssetDbContext db = new HospitalAssetDbContext();

        private bool IsUserAdmin()
        {
            var userIdSession = Session["UserId"] as int?;
            if (userIdSession == null || userIdSession.Value == 0)
            {
                return false;
            }
            int userId = userIdSession.Value;
            return db.UserRoles.Any(ur => ur.UserId == userId && ur.Role.Code.Equals("admin", StringComparison.OrdinalIgnoreCase));
        }

        // GET: User/CreateUser
        [HttpGet]
        public ActionResult CreateUser()
        {
            if (!IsUserAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.ActiveNav = "CreateUser";
            ViewBag.Departments = db.Departments.Where(d => d.IsActive).ToList();
            ViewBag.Roles = db.Roles.ToList();
            return View();
        }

        // POST: User/CreateUser
        [HttpPost]
        public ActionResult CreateUser(CreateUserViewModel model)
        {
            if (!IsUserAdmin())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
            }

            if (!ModelState.IsValid)
            {
                var errors = string.Join("<br/>", ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage));
                return Json(new { success = false, message = errors });
            }

            try
            {
                // Kiểm tra trùng lặp Username hoặc Email trong db.Users
                bool usernameExists = db.Users.Any(u => u.Username.Equals(model.Username, StringComparison.OrdinalIgnoreCase));
                if (usernameExists)
                {
                    return Json(new { success = false, message = "Tên đăng nhập đã tồn tại trong hệ thống." });
                }

                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    bool emailExists = db.Users.Any(u => u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
                    if (emailExists)
                    {
                        return Json(new { success = false, message = "Địa chỉ email đã được sử dụng bởi một tài khoản khác." });
                    }
                }

                // Tìm Role tương ứng với RoleName (Code) được gửi lên
                var role = db.Roles.FirstOrDefault(r => r.Code.Equals(model.RoleName, StringComparison.OrdinalIgnoreCase));
                if (role == null)
                {
                    return Json(new { success = false, message = "Vai trò được chọn không hợp lệ." });
                }

                var newUser = new User
                {
                    Username = model.Username.Trim(),
                    FullName = model.FullName.Trim(),
                    Email = model.Email.Trim(),
                    Phone = model.Phone.Trim(),
                    DepartmentId = model.DepartmentId,
                    PasswordHash = model.Password.Trim(), // Sử dụng mật khẩu do người dùng nhập vào
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                db.Users.Add(newUser);
                db.SaveChanges();

                // Lưu liên kết UserRoles
                var userRole = new UserRole
                {
                    UserId = newUser.Id,
                    RoleId = role.Id
                };
                db.UserRoles.Add(userRole);
                db.SaveChanges();

                return Json(new { success = true, message = "Tạo tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi tạo tài khoản: " + ex.Message });
            }
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
