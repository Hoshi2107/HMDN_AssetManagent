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

        private static readonly List<string> FULL_PERMS = new List<string> { "VIEW", "CREATE", "EDIT", "DELETE" };
        private static readonly List<string> VIEW_ONLY = new List<string> { "VIEW" };

        private List<LoginResponseDTO.ModuleDTO> GetModulesForRoles(List<string> roles)
        {
            // Định nghĩa danh sách các module có thể có trên hệ thống
            var allModules = new Dictionary<string, LoginResponseDTO.ModuleDTO>
            {
                { "Analytics", new LoginResponseDTO.ModuleDTO { code = "Analytics", name = "Dashboard", url = "/Analytics/Index", icon = "fa-chart-pie", permissions = FULL_PERMS } },
                { "Inventory", new LoginResponseDTO.ModuleDTO { code = "Inventory", name = "Tổng tài sản", url = "/Inventory/Index", icon = "fa-box-archive", permissions = FULL_PERMS } },
                { "Catalog", new LoginResponseDTO.ModuleDTO { code = "Catalog", name = "Danh mục", url = "/Category/Category", icon = "fa-layer-group", permissions = FULL_PERMS } },
                { "Locations", new LoginResponseDTO.ModuleDTO { code = "Locations", name = "Vị trí", url = "/Location/Location", icon = "fa-map-pin", permissions = FULL_PERMS } },
                { "QrCodes", new LoginResponseDTO.ModuleDTO { code = "QrCodes", name = "QR Code", url = "/QrCodes/Index", icon = "fa-qrcode", permissions = FULL_PERMS } },
                { "Lifecycle", new LoginResponseDTO.ModuleDTO { code = "Lifecycle", name = "Trạng thái", url = "/Lifecycle/Index", icon = "fa-rotate", permissions = FULL_PERMS } },
                { "VongDoiKhauHao", new LoginResponseDTO.ModuleDTO { code = "VongDoiKhauHao", name = "Vòng đời & Khấu hao", url = "/VongDoiKhauHao/VongDoiKhauHao", icon = "fa-recycle", permissions = FULL_PERMS } },
                { "Checklists", new LoginResponseDTO.ModuleDTO { code = "Checklists", name = "Checklist", url = "/Checklists/Index", icon = "fa-list-check", permissions = FULL_PERMS } },
                { "Maintenance", new LoginResponseDTO.ModuleDTO { code = "Maintenance", name = "Sửa chữa", url = "/Maintenance/Index", icon = "fa-screwdriver-wrench", permissions = FULL_PERMS } },
                { "TiepNhanBaoHong", new LoginResponseDTO.ModuleDTO { code = "TiepNhanBaoHong", name = "Tiếp nhận báo hỏng", url = "/Maintenance/TiepNhanBaoHong", icon = "fa-triangle-exclamation", permissions = FULL_PERMS } },
                { "CreateTicket", new LoginResponseDTO.ModuleDTO { code = "CreateTicket", name = "Tạo phiếu", url = "/CreateTicket/Index", icon = "fa-file-circle-plus", permissions = FULL_PERMS } },
                { "Alerts", new LoginResponseDTO.ModuleDTO { code = "Alerts", name = "Cảnh báo", url = "/Alerts/Index", icon = "fa-bell", permissions = FULL_PERMS } },
                { "Approvals", new LoginResponseDTO.ModuleDTO { code = "Approvals", name = "Phê duyệt", url = "/Approvals/Index", icon = "fa-clipboard-check", permissions = FULL_PERMS } },
                { "Settings", new LoginResponseDTO.ModuleDTO { code = "Settings", name = "Cài đặt", url = "/Settings/Index", icon = "fa-gear", permissions = FULL_PERMS } },
                { "Support", new LoginResponseDTO.ModuleDTO { code = "Support", name = "Hỗ trợ", url = "/Support/Index", icon = "fa-circle-question", permissions = FULL_PERMS } },
                { "MaintainList", new LoginResponseDTO.ModuleDTO { code = "MaintainList", name = "Danh sách bảo trì", url = "/MaintainList/Index", icon = "fa-wrench", permissions = FULL_PERMS } }
            };

            // Admin → toàn quyền tất cả module
            if (roles.Contains("admin", StringComparer.OrdinalIgnoreCase))
            {
                return allModules.Values.ToList();
            }

            // Bảng phân quyền chi tiết: role → { moduleCode → permissions }
            // FULL = VIEW + CREATE + EDIT + DELETE
            // VIEW_ONLY = chỉ xem danh sách, không tạo/sửa/xóa
            var rolePermissions = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.OrdinalIgnoreCase)
            {
                {
                    "manager", new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Analytics", FULL_PERMS }, { "Inventory", FULL_PERMS }, { "Catalog", FULL_PERMS },
                        { "Locations", FULL_PERMS }, { "QrCodes", FULL_PERMS }, { "Lifecycle", FULL_PERMS },
                        { "VongDoiKhauHao", FULL_PERMS }, { "Checklists", FULL_PERMS }, { "Maintenance", FULL_PERMS },
                        { "TiepNhanBaoHong", FULL_PERMS }, { "CreateTicket", FULL_PERMS }, { "Alerts", FULL_PERMS },
                        { "Approvals", FULL_PERMS }, { "Settings", FULL_PERMS }, { "Support", FULL_PERMS },
                        { "MaintainList", FULL_PERMS }
                    }
                },
                {
                    "technician", new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Analytics", VIEW_ONLY },
                        { "Inventory", FULL_PERMS }, { "Catalog", FULL_PERMS }, { "Locations", FULL_PERMS },
                        { "QrCodes", FULL_PERMS }, { "Lifecycle", FULL_PERMS },
                        { "VongDoiKhauHao", VIEW_ONLY },
                        { "Checklists", FULL_PERMS }, { "Maintenance", FULL_PERMS },
                        { "TiepNhanBaoHong", FULL_PERMS }, { "CreateTicket", FULL_PERMS },
                        { "Alerts", FULL_PERMS }, { "Approvals", FULL_PERMS }, { "Support", FULL_PERMS },
                        { "MaintainList", FULL_PERMS }
                    }
                },
                {
                    "approver", new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Analytics", FULL_PERMS }, { "Inventory", VIEW_ONLY },
                        { "Catalog", VIEW_ONLY }, { "Locations", VIEW_ONLY },
                        { "QrCodes", VIEW_ONLY }, { "Lifecycle", VIEW_ONLY },
                        { "VongDoiKhauHao", FULL_PERMS },
                        { "Checklists", VIEW_ONLY },
                        { "Maintenance", FULL_PERMS },
                        { "TiepNhanBaoHong", VIEW_ONLY },
                        { "CreateTicket", VIEW_ONLY },
                        { "Alerts", FULL_PERMS }, { "Approvals", FULL_PERMS }, { "Support", FULL_PERMS },
                        { "MaintainList", VIEW_ONLY }
                    }
                },
                {
                    "viewer", new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
                    {
                        { "Analytics", VIEW_ONLY }, { "Inventory", VIEW_ONLY }, { "Catalog", VIEW_ONLY },
                        { "Locations", VIEW_ONLY }, { "QrCodes", VIEW_ONLY }, { "Lifecycle", VIEW_ONLY },
                        { "VongDoiKhauHao", VIEW_ONLY }, { "Checklists", VIEW_ONLY },
                        { "Maintenance", VIEW_ONLY },
                        { "TiepNhanBaoHong", VIEW_ONLY },
                        { "CreateTicket", VIEW_ONLY },
                        { "Alerts", VIEW_ONLY }, { "Approvals", VIEW_ONLY }, { "Support", VIEW_ONLY },
                        { "MaintainList", VIEW_ONLY }
                    }
                }
            };

            // "ktv" là alias của "technician"
            rolePermissions["ktv"] = rolePermissions["technician"];

            // Merge permissions từ tất cả roles của user (union - lấy quyền cao nhất)
            var mergedPerms = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var role in roles)
            {
                if (!rolePermissions.TryGetValue(role, out var modulePerms))
                    continue;

                foreach (var kvp in modulePerms)
                {
                    if (!mergedPerms.ContainsKey(kvp.Key))
                    {
                        mergedPerms[kvp.Key] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    }
                    foreach (var perm in kvp.Value)
                    {
                        mergedPerms[kvp.Key].Add(perm);
                    }
                }
            }

            // Build danh sách module với permissions đã merge, giữ đúng thứ tự hiển thị
            var result = new List<LoginResponseDTO.ModuleDTO>();
            foreach (var moduleCode in allModules.Keys)
            {
                if (!mergedPerms.TryGetValue(moduleCode, out var perms))
                    continue;

                var mod = allModules[moduleCode];
                result.Add(new LoginResponseDTO.ModuleDTO
                {
                    code = mod.code,
                    name = mod.name,
                    url = mod.url,
                    icon = mod.icon,
                    permissions = perms.ToList()
                });
            }

            return result;
        }
    }
}

