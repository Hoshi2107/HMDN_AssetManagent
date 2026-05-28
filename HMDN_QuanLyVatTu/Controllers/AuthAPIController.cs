using HMS.Data;
using HMS.Services;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web.Http;
using HMDN_QuanLyVatTu.Models;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/authapi")]
    public class AuthAPIController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login(LoginVM model)
        {
            try
            {
                if (model == null || string.IsNullOrWhiteSpace(model.Username) || string.IsNullOrWhiteSpace(model.Password))
                {
                    return Ok(new LoginResponseDTO
                    {
                        success = false,
                        message = "Tên đăng nhập hoặc mật khẩu không được để trống."
                    });
                }

                // Nạp luôn UserRoles và Role liên quan
                var user = db.Users
                    .Include(u => u.UserRoles.Select(ur => ur.Role))
                    .FirstOrDefault(x =>
                        (x.Username == model.Username || x.Email == model.Username)
                        && x.IsActive);

                if (user == null)
                {
                    return Ok(new LoginResponseDTO
                    {
                        success = false,
                        message = "Tài khoản không tồn tại hoặc đã bị vô hiệu hóa."
                    });
                }

                if (user.PasswordHash != model.Password)
                {
                    return Ok(new LoginResponseDTO
                    {
                        success = false,
                        message = "Mật khẩu không chính xác."
                    });
                }

                // Lấy danh sách mã vai trò của user
                var roles = user.UserRoles
                    .Where(ur => ur.Role != null)
                    .Select(ur => ur.Role.Code)
                    .ToList();

                var modules = GetModulesForRoles(roles);

                // Cập nhật thời gian đăng nhập cuối
                user.LastLoginAt = DateTime.Now;
                db.SaveChanges();

                return Ok(new LoginResponseDTO
                {
                    success = true,
                    message = "Đăng nhập thành công",
                    user = new LoginResponseDTO.UserInfoDTO
                    {
                        Id = user.Id,
                        Username = user.Username,
                        FullName = user.FullName,
                        Email = user.Email,
                        roles = roles
                    },
                    modules = modules
                });
            }
            catch (Exception ex)
            {
                return Ok(new LoginResponseDTO
                {
                    success = false,
                    message = "Lỗi hệ thống khi xác thực: " + ex.Message
                });
            }
        }

        private List<LoginResponseDTO.ModuleDTO> GetModulesForRoles(List<string> roles)
        {
            var modules = new List<LoginResponseDTO.ModuleDTO>();

            // Định nghĩa danh sách các module có thể có trên hệ thống
            var allModules = new Dictionary<string, LoginResponseDTO.ModuleDTO>
            {
                { "Analytics", new LoginResponseDTO.ModuleDTO { code = "Analytics", name = "Dashboard", url = "/Analytics/Index", icon = "fa-chart-pie", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Inventory", new LoginResponseDTO.ModuleDTO { code = "Inventory", name = "Tổng tài sản", url = "/Inventory/Index", icon = "fa-box-archive", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Catalog", new LoginResponseDTO.ModuleDTO { code = "Catalog", name = "Danh mục", url = "/Category/Category", icon = "fa-layer-group", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Locations", new LoginResponseDTO.ModuleDTO { code = "Locations", name = "Vị trí", url = "/Location/Location", icon = "fa-map-pin", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "QrCodes", new LoginResponseDTO.ModuleDTO { code = "QrCodes", name = "QR Code", url = "/QrCodes/Index", icon = "fa-qrcode", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Lifecycle", new LoginResponseDTO.ModuleDTO { code = "Lifecycle", name = "Trạng thái", url = "/Lifecycle/Index", icon = "fa-rotate", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "VongDoiKhauHao", new LoginResponseDTO.ModuleDTO { code = "VongDoiKhauHao", name = "Vòng đời & Khấu hao", url = "/VongDoiKhauHao/VongDoiKhauHao", icon = "fa-recycle", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Checklists", new LoginResponseDTO.ModuleDTO { code = "Checklists", name = "Checklist", url = "/Checklists/Index", icon = "fa-list-check", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Maintenance", new LoginResponseDTO.ModuleDTO { code = "Maintenance", name = "Sửa chữa", url = "/Maintenance/Index", icon = "fa-screwdriver-wrench", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "TiepNhanBaoHong", new LoginResponseDTO.ModuleDTO { code = "TiepNhanBaoHong", name = "Tiếp nhận báo hỏng", url = "/Maintenance/TiepNhanBaoHong", icon = "fa-triangle-exclamation", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "CreateTicket", new LoginResponseDTO.ModuleDTO { code = "CreateTicket", name = "Tạo phiếu", url = "/CreateTicket/Index", icon = "fa-file-circle-plus", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Alerts", new LoginResponseDTO.ModuleDTO { code = "Alerts", name = "Cảnh báo", url = "/Alerts/Index", icon = "fa-bell", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Approvals", new LoginResponseDTO.ModuleDTO { code = "Approvals", name = "Phê duyệt", url = "/Approvals/Index", icon = "fa-clipboard-check", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Settings", new LoginResponseDTO.ModuleDTO { code = "Settings", name = "Cài đặt", url = "/Settings/Index", icon = "fa-gear", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } },
                { "Support", new LoginResponseDTO.ModuleDTO { code = "Support", name = "Hỗ trợ", url = "/Support/Index", icon = "fa-circle-question", permissions = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" } } }
            };

            // Phân quyền theo vai trò
            if (roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
            {
                return allModules.Values.ToList();
            }

            var allowedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var role in roles)
            {
                if (string.Equals(role, "manager", StringComparison.OrdinalIgnoreCase))
                {
                    allowedCodes.Add("Analytics");
                    allowedCodes.Add("Inventory");
                    allowedCodes.Add("Catalog");
                    allowedCodes.Add("Locations");
                    allowedCodes.Add("QrCodes");
                    allowedCodes.Add("Lifecycle");
                    allowedCodes.Add("VongDoiKhauHao");
                    allowedCodes.Add("Maintenance");
                    allowedCodes.Add("TiepNhanBaoHong");
                    allowedCodes.Add("Approvals");
                    allowedCodes.Add("Settings");
                    allowedCodes.Add("Support");
                    allowedCodes.Add("CreateTicket");
                }
                else if (string.Equals(role, "technician", StringComparison.OrdinalIgnoreCase) || string.Equals(role, "ktv", StringComparison.OrdinalIgnoreCase))
                {
                    allowedCodes.Add("Inventory");
                    allowedCodes.Add("QrCodes");
                    allowedCodes.Add("Checklists");
                    allowedCodes.Add("Maintenance");
                    allowedCodes.Add("TiepNhanBaoHong");
                    allowedCodes.Add("CreateTicket");
                    allowedCodes.Add("Alerts");
                    allowedCodes.Add("Support");
                }
                else if (string.Equals(role, "approver", StringComparison.OrdinalIgnoreCase))
                {
                    allowedCodes.Add("Analytics");
                    allowedCodes.Add("Inventory");
                    allowedCodes.Add("Approvals");
                    allowedCodes.Add("Alerts");
                    allowedCodes.Add("Support");
                }
                else if (string.Equals(role, "viewer", StringComparison.OrdinalIgnoreCase))
                {
                    allowedCodes.Add("Analytics");
                    allowedCodes.Add("Inventory");
                    allowedCodes.Add("Support");
                }
            }

            foreach (var code in allowedCodes)
            {
                if (allModules.TryGetValue(code, out var mod))
                {
                    var m = new LoginResponseDTO.ModuleDTO
                    {
                        code = mod.code,
                        name = mod.name,
                        url = mod.url,
                        icon = mod.icon,
                        permissions = roles.Contains("viewer", StringComparer.OrdinalIgnoreCase)
                            ? new List<string> { "VIEW" }
                            : new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" }
                    };
                    modules.Add(m);
                }
            }

            // Trả về danh sách được sắp xếp theo thứ tự hiển thị chuẩn của allModules
            return allModules.Keys
                .Where(k => allowedCodes.Contains(k))
                .Select(k => modules.First(m => string.Equals(m.code, k, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }
    }
}

