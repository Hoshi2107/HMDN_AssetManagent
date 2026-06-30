using System;
using System.Collections.Generic;
using System.Data.Entity;
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

        // GET: User/Index
        [HttpGet]
        public ActionResult Index(int? departmentId = null, string status = null, string search = null, int page = 1)
        {
            if (!IsUserAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            const int pageSize = 10;
            var query = db.Users
                .Include(u => u.Department)
                .Include(u => u.UserRoles.Select(ur => ur.Role))
                .AsQueryable();

            // Lọc theo phòng ban
            if (departmentId.HasValue && departmentId.Value > 0)
            {
                query = query.Where(u => u.DepartmentId == departmentId.Value);
            }

            // Lọc theo trạng thái
            if (!string.IsNullOrEmpty(status))
            {
                if (status.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(u => u.IsActive);
                }
                else if (status.Equals("inactive", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(u => !u.IsActive);
                }
            }

            // Lọc theo tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim();
                query = query.Where(u => u.FullName.Contains(search) || 
                                         u.Username.Contains(search) || 
                                         u.Email.Contains(search) || 
                                         u.Phone.Contains(search));
            }

            // Tính tổng số lượng
            int totalUsers = query.Count();
            int totalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
            page = Math.Max(1, Math.Min(page, totalPages == 0 ? 1 : totalPages));

            // Lấy danh sách phân trang
            var users = query
                .OrderBy(u => u.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // ViewBag dữ liệu
            ViewBag.ActiveNav = "UserList";
            ViewBag.Departments = db.Departments.Where(d => d.IsActive).ToList();
            ViewBag.SelectedDepartmentId = departmentId;
            ViewBag.SelectedStatus = status;
            ViewBag.SearchQuery = search;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalUsers = totalUsers;
            ViewBag.PageSize = pageSize;

            return View(users);
        }

        // GET: User/CreateUser
        [HttpGet]
        public ActionResult CreateUser()
        {
            if (!IsUserAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            ViewBag.ActiveNav = "UserList";
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
                    CreatedAt = DateTime.Now,
                    AvatarUrl = model.AvatarUrl
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

        // GET: User/EditUser/5
        [HttpGet]
        public ActionResult EditUser(int id)
        {
            if (!IsUserAdmin())
            {
                return RedirectToAction("Login", "Account");
            }

            var user = db.Users
                .Include(u => u.UserRoles.Select(ur => ur.Role))
                .FirstOrDefault(u => u.Id == id);

            if (user == null)
            {
                return HttpNotFound();
            }

            var userRole = user.UserRoles.FirstOrDefault();
            var roleCode = userRole != null && userRole.Role != null ? userRole.Role.Code : "";

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                DepartmentId = user.DepartmentId ?? 0,
                RoleName = roleCode,
                IsActive = user.IsActive,
                AvatarUrl = user.AvatarUrl
            };

            ViewBag.ActiveNav = "UserList";
            ViewBag.Departments = db.Departments.Where(d => d.IsActive).ToList();
            ViewBag.Roles = db.Roles.ToList();
            ViewBag.Username = user.Username;

            return View(model);
        }

        // POST: User/EditUser
        [HttpPost]
        public ActionResult EditUser(EditUserViewModel model)
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
                var user = db.Users
                    .Include(u => u.UserRoles)
                    .FirstOrDefault(u => u.Id == model.Id);

                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng này trên hệ thống." });
                }

                // Kiểm tra trùng lặp email với người dùng khác
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    bool emailExists = db.Users.Any(u => u.Id != model.Id && u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
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

                // Cập nhật thông tin
                user.FullName = model.FullName.Trim();
                user.Email = model.Email.Trim();
                user.Phone = model.Phone.Trim();
                user.DepartmentId = model.DepartmentId;
                user.IsActive = model.IsActive;
                user.UpdatedAt = DateTime.Now;
                user.AvatarUrl = model.AvatarUrl;

                // Nếu nhập mật khẩu mới thì cập nhật
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    if (model.Password.Trim().Length < 6)
                    {
                        return Json(new { success = false, message = "Mật khẩu mới phải từ 6 ký tự trở lên." });
                    }
                    user.PasswordHash = model.Password.Trim();
                }

                // Cập nhật vai trò (xóa vai trò cũ, thêm vai trò mới)
                var oldRoles = user.UserRoles.ToList();
                foreach (var oldUR in oldRoles)
                {
                    db.UserRoles.Remove(oldUR);
                }
                db.SaveChanges();

                var newUserRole = new UserRole
                {
                    UserId = user.Id,
                    RoleId = role.Id
                };
                db.UserRoles.Add(newUserRole);
                db.SaveChanges();

                return Json(new { success = true, message = "Cập nhật tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi cập nhật tài khoản: " + ex.Message });
            }
        }

        // POST: User/DeleteUser
        [HttpPost]
        public ActionResult DeleteUser(int id)
        {
            if (!IsUserAdmin())
            {
                return Json(new { success = false, message = "Bạn không có quyền thực hiện thao tác này." });
            }

            try
            {
                var user = db.Users.Find(id);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy người dùng này." });
                }

                // Không cho phép tự xóa tài khoản của chính mình
                var currentUserId = Session["UserId"] as int?;
                if (currentUserId.HasValue && currentUserId.Value == id)
                {
                    return Json(new { success = false, message = "Bạn không thể tự xóa tài khoản đang đăng nhập." });
                }

                db.Users.Remove(user);
                db.SaveChanges();
                return Json(new { success = true, message = "Xóa người dùng thành công!" });
            }
            catch (Exception)
            {
                // Nếu bị lỗi khóa ngoại (do đã tạo các phiếu hoặc tài sản), đổi sang trạng thái ngừng hoạt động
                try
                {
                    var user = db.Users.Find(id);
                    if (user != null)
                    {
                        user.IsActive = false;
                        db.SaveChanges();
                        return Json(new { success = true, message = "Tài khoản có dữ liệu liên kết, đã tự động chuyển trạng thái ngừng hoạt động thay vì xóa." });
                    }
                }
                catch (Exception ex2)
                {
                    return Json(new { success = false, message = "Lỗi khi cập nhật trạng thái: " + ex2.Message });
                }
                return Json(new { success = false, message = "Không thể xóa người dùng do có liên kết dữ liệu." });
            }
        }

        // GET: User/Profile
        [HttpGet]
        public new ActionResult Profile()
        {
            var userIdSession = Session["UserId"] as int?;
            if (userIdSession == null || userIdSession.Value == 0)
            {
                return RedirectToAction("Login", "Account");
            }
            int userId = userIdSession.Value;
            var user = db.Users
                .Include(u => u.UserRoles.Select(ur => ur.Role))
                .FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                return HttpNotFound();
            }

            var userRole = user.UserRoles.FirstOrDefault();
            var roleCode = userRole != null && userRole.Role != null ? userRole.Role.Code : "";

            var model = new EditUserViewModel
            {
                Id = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Phone = user.Phone,
                DepartmentId = user.DepartmentId ?? 0,
                RoleName = roleCode,
                IsActive = user.IsActive
            };

            ViewBag.ActiveNav = "Profile";
            ViewBag.Departments = db.Departments.Where(d => d.IsActive).ToList();
            ViewBag.Roles = db.Roles.ToList();
            ViewBag.Username = user.Username;

            return View(model);
        }

        // POST: User/Profile
        [HttpPost]
        [ValidateAntiForgeryToken]
        public new ActionResult Profile(EditUserViewModel model, System.Web.HttpPostedFileBase avatarFile)
        {
            var userIdSession = Session["UserId"] as int?;
            if (userIdSession == null || userIdSession.Value == 0)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này." });
            }

            if (model == null)
            {
                return Json(new { success = false, message = "Dữ liệu gửi lên không hợp lệ." });
            }

            int currentUserId = userIdSession.Value;
            if (model.Id != currentUserId)
            {
                return Json(new { success = false, message = "Bạn chỉ có thể cập nhật hồ sơ của chính mình." });
            }

            try
            {
                var user = db.Users.FirstOrDefault(u => u.Id == currentUserId);
                if (user == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy tài khoản trên hệ thống." });
                }

                // Kiểm tra trùng lặp email với người dùng khác
                if (!string.IsNullOrWhiteSpace(model.Email))
                {
                    bool emailExists = db.Users.Any(u => u.Id != currentUserId && u.Email.Equals(model.Email, StringComparison.OrdinalIgnoreCase));
                    if (emailExists)
                    {
                        return Json(new { success = false, message = "Địa chỉ email đã được sử dụng bởi một tài khoản khác." });
                    }
                }

                // Kiểm tra trùng lặp số điện thoại với người dùng khác
                if (!string.IsNullOrWhiteSpace(model.Phone))
                {
                    string phoneTrim = model.Phone.Trim();
                    bool phoneExists = db.Users.Any(u => u.Id != currentUserId && u.Phone == phoneTrim);
                    if (phoneExists)
                    {
                        return Json(new { success = false, message = "Số điện thoại đã được sử dụng bởi một tài khoản khác." });
                    }
                }

                // Cập nhật thông tin được phép
                user.FullName = model.FullName.Trim();
                user.Email = model.Email.Trim();
                user.Phone = model.Phone.Trim();
                user.UpdatedAt = DateTime.Now;

                // Cập nhật session FullName
                Session["FullName"] = user.FullName;

                // Nếu nhập mật khẩu mới thì cập nhật
                if (!string.IsNullOrWhiteSpace(model.Password))
                {
                    if (model.Password.Trim().Length < 6)
                    {
                        return Json(new { success = false, message = "Mật khẩu mới phải từ 6 ký tự trở lên." });
                    }
                    user.PasswordHash = model.Password.Trim();
                }

                // Xử lý upload ảnh đại diện
                if (avatarFile != null && avatarFile.ContentLength > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                    var ext = System.IO.Path.GetExtension(avatarFile.FileName).ToLower();
                    if (!allowedExtensions.Contains(ext))
                    {
                        return Json(new { success = false, message = "Định dạng ảnh đại diện không hợp lệ. Vui lòng chọn ảnh .jpg, .jpeg, .png hoặc .gif" });
                    }

                    string uploadDir = Server.MapPath("~/Uploads");
                    if (!System.IO.Directory.Exists(uploadDir))
                    {
                        System.IO.Directory.CreateDirectory(uploadDir);
                    }
                    string fileName = "avatar_" + currentUserId + ".png";
                    string path = System.IO.Path.Combine(uploadDir, fileName);

                    if (System.IO.File.Exists(path))
                    {
                        System.IO.File.Delete(path);
                    }
                    avatarFile.SaveAs(path);
                }

                db.SaveChanges();
                return Json(new { success = true, message = "Cập nhật hồ sơ tài khoản thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi cập nhật hồ sơ: " + ex.Message });
            }
        }

        // POST: User/RemoveAvatar
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveAvatar()
        {
            var userIdSession = Session["UserId"] as int?;
            if (userIdSession == null || userIdSession.Value == 0)
            {
                return Json(new { success = false, message = "Bạn cần đăng nhập để thực hiện thao tác này." });
            }

            int currentUserId = userIdSession.Value;
            try
            {
                string uploadDir = Server.MapPath("~/Uploads");
                string fileName = "avatar_" + currentUserId + ".png";
                string path = System.IO.Path.Combine(uploadDir, fileName);

                if (System.IO.File.Exists(path))
                {
                    System.IO.File.Delete(path);
                    return Json(new { success = true, message = "Đã gỡ ảnh đại diện thành công!" });
                }
                return Json(new { success = false, message = "Không tìm thấy ảnh đại diện để gỡ." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi khi gỡ ảnh đại diện: " + ex.Message });
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
