using System;
using System.Linq;
using System.Data.Entity;
using HMDN_QuanLyVatTu.Models;
using HMS.Data;

namespace Temp
{
    class Program
    {
        static void Main()
        {
            try
            {
                using (var db = new HospitalAssetDbContext())
                {
                    var def = new ChecklistDefinition
                    {
                        Scope = "item",
                        GroupId = 3,
                        ItemId = 285,
                        InventoryId = null,
                        DefinitionCode = null,
                        CycleType = "daily",
                        CheckName = "Kiểm tra hằng ngày - TEST EF",
                        Description = "Kiểm tra dây điện",
                        IsRequired = true,
                        SortOrder = 2,
                        IsActive = true,
                        CreatedAt = DateTime.Now
                    };
                    db.ChecklistDefinitions.Add(def);
                    db.SaveChanges();
                    Console.WriteLine("Insert successful!");
                    
                    // Clean up
                    db.ChecklistDefinitions.Remove(def);
                    db.SaveChanges();
                    Console.WriteLine("Cleanup successful!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERROR: " + ex.ToString());
                if (ex is System.Data.Entity.Validation.DbEntityValidationException)
                {
                    var valEx = (System.Data.Entity.Validation.DbEntityValidationException)ex;
                    foreach (var err in valEx.EntityValidationErrors)
                    {
                        foreach (var validationError in err.ValidationErrors)
                        {
                            Console.WriteLine("- Property: " + validationError.PropertyName + ", Error: " + validationError.ErrorMessage);
                        }
                    }
                }
            }
        }
    }
}
