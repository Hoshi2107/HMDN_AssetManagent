using System.Net;
using System.Web.Http;
using HMS.Services;

namespace HMDN_QuanLyVatTu.Controllers
{
    [RoutePrefix("api/auth")]
    public class AuthAPIController : ApiController
    {
        private readonly IAuthService _authService;

        // Constructor dùng cho Dependency Injection
        public AuthAPIController(IAuthService authService)
        {
            _authService = authService;
        }

        // Constructor mặc định (fallback) nếu không cấu hình DI
        public AuthAPIController() : this(new AuthService())
        {
        }

        // POST api/auth/login
        [HttpPost]
        [Route("login")]
        public IHttpActionResult Login([FromBody] LoginRequest request)
        {
            if (request == null)
            {
                return BadRequest("Dữ liệu đăng nhập không hợp lệ.");
            }

            var result = _authService.Login(request.Username, request.Password);

            switch (result.Status)
            {
                case LoginStatus.Success:
                    // Thành công: HTTP 200 (OK) kèm theo thông tin tài khoản và danh sách vai trò
                    return Ok(new
                    {
                        success = true,
                        message = result.Message,
                        user = result.User
                    });

                case LoginStatus.Inactive:
                    // Tài khoản bị khóa (IsActive = 0): HTTP 400 (Bad Request) kèm thông báo lỗi cụ thể
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        success = false,
                        message = result.Message
                    });

                case LoginStatus.NotFoundOrWrongPassword:
                    // Tài khoản không tồn tại hoặc sai mật khẩu: HTTP 401 (Unauthorized)
                    return Content(HttpStatusCode.Unauthorized, new
                    {
                        success = false,
                        message = result.Message
                    });

                case LoginStatus.InvalidInput:  
                    return Content(HttpStatusCode.BadRequest, new
                    {
                        success = false,
                        message = result.Message
                    });

                default:
                    // Các lỗi hệ thống khác: HTTP 500 (Internal Server Error)
                    return Content(HttpStatusCode.InternalServerError, new
                    {
                        success = false,
                        message = result.Message
                    });
            }
        }
    }

    public class LoginRequest
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
