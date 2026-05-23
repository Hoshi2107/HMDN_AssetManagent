using HMS.Data;
using HMS.Services;
using System;
using System.Linq;
using System.Net;
using System.Web.Http;
using System.Web.UI.WebControls;

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
                var user = db.Users
                    .FirstOrDefault(x =>
                        x.Username == model.Username
                        && x.IsActive);

                if (user == null)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Sai tài khoản hoặc mật khẩu"
                    });
                }

                // TEST tạm
                if (user.PasswordHash != model.Password)
                {
                    return Ok(new
                    {
                        success = false,
                        message = "Sai mật khẩu"
                    });
                }

                return Ok(new
                {
                    success = true,
                    user = new
                    {
                        user.Id,
                        user.Username,
                        user.FullName
                    }
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

}

