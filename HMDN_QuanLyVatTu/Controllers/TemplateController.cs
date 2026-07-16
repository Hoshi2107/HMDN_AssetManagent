using HMS.Data;
using OfficeOpenXml;
using OfficeOpenXml.DataValidation;
using System.Data.SqlClient;
using System.IO;
using System.Linq;
using System.Web.Mvc;

namespace HMDN.Controllers
{
    public class TemplateController : Controller
    {
        HospitalAssetDbContext db = new HospitalAssetDbContext();

        [HttpGet]
        [Route("template/inventory")]
        public ActionResult Inventory()
        {
            // Lấy danh sách dropdown từ DB
            var items = db.Database.SqlQuery<string>(
                "SELECT Name FROM Items WHERE IsActive = 1 ORDER BY Name"
            ).ToList();

            var departments = db.Database.SqlQuery<string>(
                "SELECT Name FROM Departments WHERE IsActive = 1 ORDER BY Name"
            ).ToList();

            var locations = db.Database.SqlQuery<string>(
                "SELECT Name FROM Locations WHERE IsActive = 1 ORDER BY Name"
            ).ToList();

            var groups = db.Database.SqlQuery<string>(
                "SELECT Name FROM Groups WHERE IsActive = 1 ORDER BY SortOrder, Name"
            ).ToList();

            using (var pkg = new ExcelPackage())
            {
                // ── Sheet 1: Import data ──────────────────────
                var ws = pkg.Workbook.Worksheets.Add("Import Thiết Bị");

                // Row 1: Tiêu đề
                ws.Cells[1, 1].Value = "TEMPLATE IMPORT THIẾT BỊ — Không xóa/sửa 3 dòng đầu. (*) Bắt buộc.";
                ws.Cells[1, 1, 1, 18].Merge = true;
                ws.Cells[1, 1].Style.Font.Bold = true;
                ws.Cells[1, 1].Style.Font.Size = 12;
                ws.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 78, 121));
                ws.Cells[1, 1].Style.Font.Color.SetColor(System.Drawing.Color.White);

                // Row 2: Hướng dẫn
                ws.Cells[2, 1].Value = "(*) Bắt buộc: Mã tài sản, Tên thiết bị  |  Ngày: YYYY-MM-DD  |  Đơn giá: số nguyên (VND)  |  Cột màu vàng = chọn từ dropdown";
                ws.Cells[2, 1, 2, 18].Merge = true;
                ws.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(255, 242, 204));
                ws.Cells[2, 1].Style.Font.Italic = true;

                // Row 3: Headers
                string[] headers = {
                    "Mã tài sản *", "Tên thiết bị *", "Serial Number", "Số lượng",
                    "Khoa", "Vị trí", "Ngày nhập", "Hết bảo hành",
                    "Đơn giá", "Năm sản xuất", "Năm sử dụng",
                    "Khấu hao (%)", "Số năm khấu hao",
                    "Loại tài sản", "Nhà sản xuất", "Nhà cung cấp",
                    "Nước sản xuất", "Ghi chú"
                };

                // Cột dropdown (1-based)
                int[] dropdownCols = { 2, 5, 6, 14 }; // Tên thiết bị, Khoa, Vị trí, Loại tài sản

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cells[3, i + 1];
                    cell.Value = headers[i];
                    cell.Style.Font.Bold = true;
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;

                    bool isDropdown = dropdownCols.Contains(i + 1);
                    cell.Style.Fill.BackgroundColor.SetColor(
                        isDropdown
                            ? System.Drawing.Color.FromArgb(255, 230, 153)  // vàng = dropdown
                            : System.Drawing.Color.FromArgb(68, 114, 196)   // xanh = nhập tay
                    );
                    cell.Style.Font.Color.SetColor(
                        isDropdown
                            ? System.Drawing.Color.FromArgb(60, 60, 60)
                            : System.Drawing.Color.White
                    );
                    cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                }

                // Row 4-6: Dữ liệu mẫu
                //object[][] samples = {
                //    new object[] {
                //        "TS-2024-001",
                //        items.Count > 0 ? items[0] : "Máy đo huyết áp",
                //        "SN-OMR-001", 1,
                //        departments.Count > 0 ? departments[0] : "Khoa Nội",
                //        locations.Count > 0 ? locations[0] : "Tầng 1 - P101",
                //        "2024-01-15", "2027-01-15",
                //        4500000, 2022, 2024, 10, 10,
                //        groups.Count > 0 ? groups[0] : "Thiết bị chẩn đoán",
                //        "Omron", "Công ty Thiết Bị Y Tế Miền Nam", "Nhật Bản", "Dữ liệu mẫu 1"
                //    },
                //    new object[] {
                //        "TS-2024-002",
                //        items.Count > 1 ? items[1] : "Máy thở",
                //        "SN-PHL-002", 1,
                //        departments.Count > 1 ? departments[1] : "Khoa Ngoại",
                //        locations.Count > 1 ? locations[1] : "Tầng 2 - P201",
                //        "2024-03-20", "2029-03-20",
                //        85000000, 2023, 2024, 10, 10,
                //        groups.Count > 1 ? groups[1] : "Thiết bị điều trị",
                //        "Philips", "Công ty Dược Phẩm TW", "Hà Lan", "Dữ liệu mẫu 2"
                //    },
                //    new object[] {
                //        "TS-2024-003",
                //        items.Count > 2 ? items[2] : "Máy siêu âm",
                //        "SN-SIE-003", 1,
                //        departments.Count > 2 ? departments[2] : "Khoa ICU",
                //        locations.Count > 2 ? locations[2] : "Tầng 3 - P301",
                //        "2024-05-10", "2029-05-10",
                //        320000000, 2023, 2024, 10, 10,
                //        groups.Count > 0 ? groups[0] : "Thiết bị chẩn đoán",
                //        "Siemens", "Công ty Medtronic VN", "Đức", ""
                //    }
                //};

                //for (int r = 0; r < samples.Length; r++)
                //{
                //    for (int c = 0; c < samples[r].Length; c++)
                //    {
                //        ws.Cells[r + 4, c + 1].Value = samples[r][c];
                //    }
                //    // Zebra stripe
                //    if (r % 2 == 1)
                //    {
                //        ws.Cells[r + 4, 1, r + 4, 18].Style.Fill.PatternType =
                //            OfficeOpenXml.Style.ExcelFillStyle.Solid;
                //        ws.Cells[r + 4, 1, r + 4, 18].Style.Fill.BackgroundColor
                //            .SetColor(System.Drawing.Color.FromArgb(242, 242, 242));
                //    }
                //}

                // ── Column widths ──
                int[] widths = { 14, 26, 16, 10, 22, 22, 12, 13, 14, 12, 12, 13, 16, 22, 20, 24, 14, 28 };
                for (int i = 0; i < widths.Length; i++)
                    ws.Column(i + 1).Width = widths[i];

                // Freeze panes: giữ header khi scroll
                ws.View.FreezePanes(4, 1);

                // ── Sheet 2: DanhMuc (source cho dropdown) ───
                var wsDm = pkg.Workbook.Worksheets.Add("DanhMuc");

                // Headers
                wsDm.Cells[1, 1].Value = "Tên thiết bị";
                wsDm.Cells[1, 3].Value = "Khoa";
                wsDm.Cells[1, 5].Value = "Vị trí";
                wsDm.Cells[1, 7].Value = "Loại tài sản";
                foreach (var h in new[] { wsDm.Cells[1, 1], wsDm.Cells[1, 3], wsDm.Cells[1, 5], wsDm.Cells[1, 7] })
                {
                    h.Style.Font.Bold = true;
                    h.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    h.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(68, 114, 196));
                    h.Style.Font.Color.SetColor(System.Drawing.Color.White);
                }

                int maxLen = new[] { items.Count, departments.Count, locations.Count, groups.Count }.Max();
                for (int i = 0; i < maxLen; i++)
                {
                    if (i < items.Count) wsDm.Cells[i + 2, 1].Value = items[i];
                    if (i < departments.Count) wsDm.Cells[i + 2, 3].Value = departments[i];
                    if (i < locations.Count) wsDm.Cells[i + 2, 5].Value = locations[i];
                    if (i < groups.Count) wsDm.Cells[i + 2, 7].Value = groups[i];
                }

                wsDm.Column(1).Width = 30;
                wsDm.Column(3).Width = 24;
                wsDm.Column(5).Width = 24;
                wsDm.Column(7).Width = 24;

                // Ẩn sheet DanhMuc - người dùng không cần thấy
                wsDm.Hidden = OfficeOpenXml.eWorkSheetHidden.Hidden;

                // ── Named Ranges để dùng trong Data Validation ──
                var itemRange = wsDm.Cells[2, 1, items.Count + 1, 1];
                var deptRange = wsDm.Cells[2, 3, departments.Count + 1, 3];
                var locRange = wsDm.Cells[2, 5, locations.Count + 1, 5];
                var groupRange = wsDm.Cells[2, 7, groups.Count + 1, 7];

                pkg.Workbook.Names.Add("LIST_ITEMS", itemRange);
                pkg.Workbook.Names.Add("LIST_DEPTS", deptRange);
                pkg.Workbook.Names.Add("LIST_LOCS", locRange);
                pkg.Workbook.Names.Add("LIST_GROUPS", groupRange);

                // ── Data Validation ──────────────────────────
                // Áp dụng cho row 4 → 203 (200 dòng data)

                // Tên thiết bị - col B
                var valItem = ws.DataValidations.AddListValidation("B4:B203");
                valItem.Formula.ExcelFormula = "LIST_ITEMS";
                valItem.ShowErrorMessage = true;
                valItem.ErrorTitle = "Tên thiết bị không hợp lệ";
                valItem.Error = "Vui lòng chọn tên thiết bị từ danh sách dropdown";
                valItem.ShowInputMessage = true;
                valItem.PromptTitle = "Tên thiết bị";
                valItem.Prompt = "Click mũi tên để chọn thiết bị từ hệ thống";

                // Khoa - col E
                var valDept = ws.DataValidations.AddListValidation("E4:E203");
                valDept.Formula.ExcelFormula = "LIST_DEPTS";
                valDept.ShowErrorMessage = true;
                valDept.ErrorTitle = "Khoa không tồn tại";
                valDept.Error = "Vui lòng chọn khoa từ danh sách dropdown";
                valDept.ShowInputMessage = true;
                valDept.PromptTitle = "Khoa";
                valDept.Prompt = "Click mũi tên để chọn khoa từ hệ thống";

                // Vị trí - col F
                var valLoc = ws.DataValidations.AddListValidation("F4:F203");
                valLoc.Formula.ExcelFormula = "LIST_LOCS";
                valLoc.ShowErrorMessage = true;
                valLoc.ErrorTitle = "Vị trí không tồn tại";
                valLoc.Error = "Vui lòng chọn vị trí từ danh sách dropdown";
                valLoc.ShowInputMessage = true;
                valLoc.PromptTitle = "Vị trí";
                valLoc.Prompt = "Click mũi tên để chọn vị trí từ hệ thống";

                // Loại tài sản - col N
                var valGroup = ws.DataValidations.AddListValidation("N4:N203");
                valGroup.Formula.ExcelFormula = "LIST_GROUPS";
                valGroup.ShowErrorMessage = true;
                valGroup.ErrorTitle = "Loại tài sản không hợp lệ";
                valGroup.Error = "Vui lòng chọn loại tài sản từ danh sách dropdown";
                valGroup.ShowInputMessage = true;
                valGroup.PromptTitle = "Loại tài sản";
                valGroup.Prompt = "Click mũi tên để chọn loại tài sản từ hệ thống";

                // Số lượng - col D (số nguyên ≥ 1)
                var valQty = ws.DataValidations.AddIntegerValidation("D4:D203");
                valQty.Operator = ExcelDataValidationOperator.greaterThanOrEqual;
                valQty.Formula.Value = 1;
                valQty.ShowErrorMessage = true;
                valQty.ErrorTitle = "Số lượng không hợp lệ";
                valQty.Error = "Số lượng phải là số nguyên ≥ 1";

                // Đơn giá - col I (số ≥ 0)
                var valPrice = ws.DataValidations.AddDecimalValidation("I4:I203");
                valPrice.Operator = ExcelDataValidationOperator.greaterThanOrEqual;
                valPrice.Formula.Value = 0;
                valPrice.ShowErrorMessage = true;
                valPrice.ErrorTitle = "Đơn giá không hợp lệ";
                valPrice.Error = "Đơn giá phải là số ≥ 0";

                // ── Trả về file ──────────────────────────────
                var stream = new MemoryStream(pkg.GetAsByteArray());
                return File(
                    stream,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    "Template_Import_ThietBi.xlsx"
                );
            }
        }
    }
}