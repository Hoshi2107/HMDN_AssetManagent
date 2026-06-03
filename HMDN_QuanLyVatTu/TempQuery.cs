using System;
using System.Linq;
using System.Data.Entity;
using HMDN_QuanLyVatTu.Models;
using System.IO;

namespace Temp
{
    class Program
    {
        static void Main()
        {
            using (var db = new HospitalAssetDbContext())
            {
                var approved = db.Inventories.Where(x => x.ApprovalStatus == ""approved"").ToList();
                var active = approved.Count(x => x.LifeStatus == ""active"");
                var suspended = approved.Count(x => x.LifeStatus == ""suspended"");
                var nullStatus = approved.Count(x => string.IsNullOrEmpty(x.LifeStatus));
                var other = approved.Where(x => x.LifeStatus != ""active"" && x.LifeStatus != ""suspended"" && !string.IsNullOrEmpty(x.LifeStatus)).Select(x => x.LifeStatus).ToList();
                
                var maintenanceLogs = db.Database.SqlQuery<int>(""SELECT COUNT(CASE WHEN Vendor IS NULL OR LTRIM(RTRIM(Vendor)) = '' THEN 1 END) + COUNT(CASE WHEN Vendor IS NOT NULL AND LTRIM(RTRIM(Vendor)) <> '' THEN 1 END) FROM dbo.MaintenanceLogs WHERE Status IN ('open', 'in_progress')"").FirstOrDefault();

                Console.WriteLine($""Total Approved: {approved.Count}"");
                Console.WriteLine($""Active: {active}"");
                Console.WriteLine($""Suspended: {suspended}"");
                Console.WriteLine($""Null/Empty: {nullStatus}"");
                Console.WriteLine($""Other: {string.Join("", "", other)}"");
                Console.WriteLine($""Maintenance Logs count: {maintenanceLogs}"");
            }
        }
    }
}
