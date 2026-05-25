using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Mvc;

namespace HMDN_QuanLyVatTu.Controllers
{
    public class CustomAuthorizeAttribute : AuthorizeAttribute
    {
        private readonly string _moduleCode;

        public CustomAuthorizeAttribute(string moduleCode)
        {
            _moduleCode = moduleCode;
        }

        protected override bool AuthorizeCore(HttpContextBase httpContext)
        {
            // 1. SỬA TẠI ĐÂY: Dùng ĐỘNG (dynamic) để C# ép kiểu chuẩn từ AccountController sang
            var userModules = httpContext.Session["UserModules"] as dynamic;
            if (userModules == null) return false;

            try
            {
                // 2. Duyệt qua danh sách module
                foreach (var mod in userModules)
                {
                    // So sánh không phân biệt chữ hoa chữ thường cho chắc chắn
                    if (string.Equals((string)mod.code, _moduleCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return true; // Hợp lệ -> Cho phép vào trang
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false; // Không khớp module nào -> Chặn cửa
        }

        protected override void HandleUnauthorizedRequest(AuthorizationContext filterContext)
        {
            filterContext.Result = new RedirectResult("/Account/Login?error=unauthorized");
        }
    }
}