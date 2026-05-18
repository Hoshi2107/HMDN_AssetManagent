using System.Linq;
using System.Web.Http;
using System.Data.SqlClient;
using System.Data.Entity;
using HMS.Models.ViewModels;
using HMS.Models;
using HMS.Data;

namespace HMDN.Controllers.API
{
    [RoutePrefix("api/location")]
    public class LocationAPIController : ApiController
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();
    }
}
