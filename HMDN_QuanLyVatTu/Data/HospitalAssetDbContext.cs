using HMS.Models;
using HMS.Models.Auth;
using HMS.Models.Catalog;
using HMS.Models.Inventory;
using HMDN_QuanLyVatTu.Models;
using System.Collections.Generic;
using System.Data.Entity;

namespace HMS.Data
{
    public class HospitalAssetDbContext : DbContext
    {
        public HospitalAssetDbContext()
            : base("name=HospitalAssetDB")
        {
        }

        public DbSet<User> Users { get; set; }

        public DbSet<Role> Roles { get; set; }

        public DbSet<UserRole> UserRoles { get; set; }

        public DbSet<Department> Departments { get; set; }

        public DbSet<Group> Groups { get; set; }

        public DbSet<Item> Items { get; set; }

        public DbSet<Inventory> Inventories { get; set; }

        public DbSet<Location> Locations { get; set; }
        public DbSet<Tickets> Tickets { get; set; }
        public DbSet<TicketDiscussion> TicketDiscussions { get; set; }
        public DbSet<DepreciationLog> DepreciationLogs { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .ToTable("Users");

            modelBuilder.Entity<Role>()
                .ToTable("Roles");

            modelBuilder.Entity<UserRole>()
                .ToTable("UserRoles");

            modelBuilder.Entity<Department>()
                .ToTable("Departments");

            modelBuilder.Entity<Group>()
                .ToTable("Groups");

            modelBuilder.Entity<Item>()
                .ToTable("Items");

            modelBuilder.Entity<Tickets>()
                .ToTable("Tickets");

            modelBuilder.Entity<TicketDiscussion>()
                .ToTable("TicketDiscussions");

            // QUAN TRỌNG NHẤT
            modelBuilder.Entity<Inventory>()
                .ToTable("Inventory");

            modelBuilder.Entity<Location>()
                .ToTable("Locations");

            modelBuilder.Entity<DepreciationLog>()
                .ToTable("DepreciationLogs");
        }

    }
}