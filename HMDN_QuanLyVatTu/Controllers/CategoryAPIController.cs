using System.Linq;
using System.Web.Http;
using System.Data.SqlClient;
using System.Data.Entity;
using HMS.Models.ViewModels;
using HMS.Models;
using HMS.Data;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/category")]
    public class CategoryAPIController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();
    }
}
