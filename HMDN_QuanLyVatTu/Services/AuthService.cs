using System;
using System.Collections.Generic;
using System.Linq;
using System.Data.Entity;
using HMS.Data;
using HMS.Models.Auth;

namespace HMS.Services
{
    public class AuthService : IAuthService
    {
        public LoginResult Login(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                return new LoginResult
                {
                    Status = LoginStatus.InvalidInput,
                    Message = "Tên đăng nhập hoặc mật khẩu không được để trống."
                };
            }

            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    // 1. Kiểm tra tài khoản trong bảng dbo.Users. Trùng khớp Username.
                    var user = db.Users
                        .Include(u => u.UserRoles.Select(ur => ur.Role))
                        .FirstOrDefault(u => u.Username.Equals(username, StringComparison.OrdinalIgnoreCase));

                    if (user == null)
                    {
                        return new LoginResult
                        {
                            Status = LoginStatus.NotFoundOrWrongPassword,
                            Message = "Tài khoản không tồn tại hoặc mật khẩu không chính xác."
                        };
                    }

                    // Kiểm tra xem tài khoản có hoạt động không (IsActive = 1)
                    if (!user.IsActive)
                    {
                        return new LoginResult
                        {
                            Status = LoginStatus.Inactive,
                            Message = "Tài khoản của bạn đã bị vô hiệu hóa."
                        };
                    }

                    // 2. Kiểm tra mật khẩu bằng cách so sánh chuỗi trực tiếp (Plain-text)
                    if (user.PasswordHash != password)
                    {
                        return new LoginResult
                        {
                            Status = LoginStatus.NotFoundOrWrongPassword,
                            Message = "Tài khoản không tồn tại hoặc mật khẩu không chính xác."
                        };
                    }

                    // 3. Lấy ra danh sách quyền (Roles) của người dùng đó thông qua bảng liên kết trung gian
                    var roles = user.UserRoles
                        .Where(ur => ur.Role != null)
                        .Select(ur => ur.Role.Code)
                        .ToList();

                    // Cập nhật LastLoginAt
                    user.LastLoginAt = DateTime.Now;
                    db.SaveChanges();

                    return new LoginResult
                    {
                        Status = LoginStatus.Success,
                        Message = "Đăng nhập thành công.",
                        User = new UserDto
                        {
                            Id = user.Id,
                            Username = user.Username,
                            FullName = user.FullName,
                            Email = user.Email,
                            Phone = user.Phone,
                            AvatarUrl = user.AvatarUrl,
                            DepartmentId = user.DepartmentId,
                            Roles = roles
                        }
                    };
                }
            }
            catch (Exception ex)
            {
                return new LoginResult
                {
                    Status = LoginStatus.ServerError,
                    Message = "Lỗi hệ thống khi xác thực: " + ex.Message
                };
            }
        }
    }

    public enum LoginStatus
    {
        Success,
        InvalidInput,
        NotFoundOrWrongPassword,
        Inactive,
        ServerError
    }

    public class LoginResult
    {
        public LoginStatus Status { get; set; }
        public string Message { get; set; }
        public UserDto User { get; set; }
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string AvatarUrl { get; set; }
        public int? DepartmentId { get; set; }
        public List<string> Roles { get; set; }
    }
}
