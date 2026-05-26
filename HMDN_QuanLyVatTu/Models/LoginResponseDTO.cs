using HMDN_QuanLyVatTu.Controllers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class LoginResponseDTO
    {
        public bool success { get; set; }
        public string message { get; set; }
        public UserInfoDTO user { get; set; }
        public List<ModuleDTO> modules { get; set; }

        public class UserInfoDTO
        {
            public int Id { get; set; }
            public string Username { get; set; }
            public string FullName { get; set; }
            public string Email { get; set; }
            public List<string> roles { get; set; }
        }

        // Class chứa danh sách phân hệ Menu và Mảng quyền hành động (VIEW, CREATE,...)
        public class ModuleDTO
        {
            public string code { get; set; }
            public string name { get; set; }
            public string url { get; set; }
            public string icon { get; set; }
            public List<string> permissions { get; set; }
        }
    }
}