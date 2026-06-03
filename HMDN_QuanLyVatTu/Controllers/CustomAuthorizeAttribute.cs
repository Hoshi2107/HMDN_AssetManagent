using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Web;
using System.Web.Http.Controllers;
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

    public class CustomApiAuthorizeAttribute : System.Web.Http.AuthorizeAttribute
    {
        private readonly string _moduleCode;

        public CustomApiAuthorizeAttribute(string moduleCode)
        {
            _moduleCode = moduleCode;
        }

        protected override bool IsAuthorized(HttpActionContext actionContext)
        {
            var httpContext = HttpContext.Current;
            if (httpContext == null || httpContext.Session == null) return false;

            var userModules = httpContext.Session["UserModules"] as dynamic;
            if (userModules == null) return false;

            try
            {
                foreach (var mod in userModules)
                {
                    if (string.Equals((string)mod.code, _moduleCode, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        protected override void HandleUnauthorizedRequest(HttpActionContext actionContext)
        {
            actionContext.Response = actionContext.Request.CreateResponse(
                System.Net.HttpStatusCode.Unauthorized,
                new { success = false, message = "Bạn không có quyền truy cập tài nguyên này hoặc phiên đăng nhập đã hết hạn." }
            );
        }
    }
}