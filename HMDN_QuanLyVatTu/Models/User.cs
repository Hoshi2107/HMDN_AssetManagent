using System;
using System.Collections.Generic;

namespace HMS.Models.Auth
{
    public class User
    {
        public int Id { get; set; }

        public string Username { get; set; }

        public string PasswordHash { get; set; }

        public string FullName { get; set; }

        public string Email { get; set; }

        public string Phone { get; set; }

        public int? DepartmentId { get; set; }

        public string AvatarUrl { get; set; }

        public bool IsActive { get; set; }

        public DateTime? LastLoginAt { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation
        public virtual Department Department { get; set; }

        public virtual ICollection<UserRole> UserRoles { get; set; }

        public User()
        {
            UserRoles = new HashSet<UserRole>();
        }
    }
}