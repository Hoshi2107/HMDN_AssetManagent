using System;
public class CompleteRenewalDto
{
    public int Id { get; set; }
    // Ngày tiếp theo do người dùng xác nhận/chỉnh trong modal.
    // Nếu null thì fallback về tính tự động (giữ tương thích ngược).
    public DateTime? NextMaintenanceDate { get; set; }
}