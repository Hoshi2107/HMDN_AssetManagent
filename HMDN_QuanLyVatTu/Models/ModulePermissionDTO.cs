using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace HMDN_QuanLyVatTu.Models
{
    public class ModulePermissionDTO
    {
        public string code { get; set; }
        public string name { get; set; }
        public string url { get; set; }
        public string icon { get; set; }
        public List<string> permissions { get; set; }
    }
}