using System;
using System.Collections.Generic;
using System.Linq;

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

        // Helper property to map detailed permissions stored in AvatarUrl
        public List<string> DetailedPermissions
        {
            get
            {
                if (string.IsNullOrEmpty(AvatarUrl)) return new List<string>();
                return AvatarUrl.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            }
            set
            {
                AvatarUrl = value != null ? string.Join(",", value) : null;
            }
        }

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