using System.ComponentModel.DataAnnotations;

namespace HMDN_QuanLyVatTu.Models
{
    public class EditUserViewModel
    {
        [Required(ErrorMessage = "Không tìm thấy mã người dùng")]
        public int Id { get; set; }

        [Required(ErrorMessage = "Họ và tên không được để trống")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email không được để trống")]
        [EmailAddress(ErrorMessage = "Email không đúng định dạng")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Số điện thoại không được để trống")]
        [Phone(ErrorMessage = "Số điện thoại không đúng định dạng")]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn phòng ban")]
        public int DepartmentId { get; set; }

        [Required(ErrorMessage = "Vui lòng chọn vai trò")]
        public string RoleName { get; set; }

        // Mật khẩu không bắt buộc, nếu nhập mới thay đổi
        public string Password { get; set; }

        [Required(ErrorMessage = "Trạng thái hoạt động không được để trống")]
        public bool IsActive { get; set; }
    }
}
