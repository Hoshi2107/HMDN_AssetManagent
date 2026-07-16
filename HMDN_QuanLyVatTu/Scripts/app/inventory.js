const STATUS = {
    active: {
        label: 'Đang sử dụng',
        class: 's-active'
    },
    maintenance_bv: {
        label: 'BV bảo trì',
        class: 's-maintenance_bv'
    },
    maintenance_hang: {
        label: 'Hãng bảo hành',
        class: 's-maintenance_hang'
    },
    suspended: {
        label: 'Tạm ngưng',
        class: 's-suspended'
    },
    Send_for_warranty: {
        label: 'Gửi về bảo hành',
        class: 's-Send_for_warranty'
    },
    replaced: {
        label: 'Thay thế',
        class: 's-replaced'
    }
}

var app = new Vue({
    el: '#app',

    delimiters: ['${', '}'],

    data: {
        STATUS: STATUS,

        ticketSearch: '',

        // IMPORT
        showImportModal: false,
        importStep: 1,
        importFile: null,
        importDragOver: false,
        importParsing: false,
        importLoading: false,
        importRows: [],
        importResult: { success: 0, failed: 0 },

        // === REPLACE MODAL STATE ===
        showReplaceModal: false,
        replaceableDevices: [],
        replaceNote: '',
        selectedReplaceId: null,
        replaceLoading: false,

        showScheduleModal: false,
        maintenanceSchedules: [],
        scheduleForm: {
            InventoryId: null,
            ScheduleName: '',
            MaintenanceType: 'preventive',
            LastMaintenanceDate: null,
            NextMaintenanceDate: null,
            ReminderDays: 15,
            IsRecurring: true,
            RecurringMonths: 3,
            CreatedBy: 1
        },


        showTicketDropdown: false,

        qrDevice: null,
        showQrActionModal: false,
        selectedQrInventoryId: null,

        qrScanner: null,
        showQrInDetail: false,
        showQrResultModal: false,
        newInventoryQr: {
            id: null,
            url: '',
            token: ''
        },

        //ticketList: [],

        //errorForm: {
        //    inventoryId: null,
        //    ticketId: null,
        //    title: '',
        //    errorDescription: '',
        //    priority: 'Medium'
        //},
        errorForm: {
            inventoryId: null,
            //ticketId: null,
            IdTicket: null,
            title: '',
            errorDescription: '',
            priority: 'normal',
            stillWorking: false 
        },

        showHistoryDetail: false,
        selectedHistory: null,

        activeTab: "detail",

        maintenanceHistory: [],

        groupsData: [],

        checkCycles: [],

        //ImportDate: null,
        //ExpiryDate: null,
        //WarrantyExpiry: null,

        createForm: {

            AssetCode: '',

            ItemId: '',

            SerialNumber: '',

            DepartmentId: '',

            LocationId: '',

            ImportDate: null,

            UnitPrice: 0,

            Quantity: 1,

            CreatedBy: 1,

            IdTicket: null,

            ExpiryDate: null,
            WarrantyExpiry: null,
            CheckCycleId: null,

            DepreciationRate: null,
            DepreciationYears: null,
            ResidualValue: null,

            ApprovedQuantity: null,

            YearManufactured: null,
            YearInUse: null,
            UsageYears: null,

            AssetCategory: '',
            GroupAssetCode: '',
            AccountingCode: '',
            InsuranceCode: '',

            CountryManufactured: '',
            Manufacturer: '',
            SupplierName: '',

            QrCode: '',
            Note: ''
        },

        showEditModal: false,

        editForm: {
            Id: null,

            AssetCode: '',
            ItemId: null,
            SerialNumber: '',
            Quantity: 1,

            DepartmentId: null,
            LocationId: null,

            ImportDate: null,
            ExpiryDate: null,
            WarrantyExpiry: null,

            CheckCycleId: null,

            UnitPrice: 0,

            DepreciationRate: null,
            DepreciationYears: null,
            ResidualValue: null,

            YearManufactured: null,
            YearInUse: null,
            UsageYears: null,

            AssetCategory: '',

            GroupAssetCode: '',
            AccountingCode: '',
            InsuranceCode: '',

            CountryManufactured: '',
            Manufacturer: '',
            SupplierName: '',

            QrCode: '',
            Note: '',

            IdTicket: null
        },

        items: [],

        departments: [],

        locationsData: [],

        tickets: [],

        showAddOptionModal: false,

        showQrModal: false,

        showManualModal: false,

        showSuspendModal: false,

        suspendReason: '',

        selectedSuspendId: null,

        searchQuery: '',

        // filterLoca: '',

        filterDept: '',

        filterStatus: '',

        filterGroup: '',

        showModal: false,

        selectedDevice: null,

        currentPage: 1,

        pageSize: 15,

        alertCount: 3,

        devices: [],

        sort: {
            key: '',
            dir: 1
        }
    },
    
    computed: {

        filteredTickets() {
            if (!this.ticketSearch.trim()) return this.tickets
            const q = this.ticketSearch.toLowerCase().trim()
            return this.tickets.filter(t =>
                (t.TicketCode || '').toLowerCase().includes(q) ||
                (t.Status || '').toLowerCase().includes(q) ||
                (t.Description || '').toLowerCase().includes(q)
            )
        },

        importValidCount() {
            return this.importRows.filter(r => r._errors.length === 0).length
        },
        importErrorCount() {
            return this.importRows.filter(r => r._errors.length > 0).length
        },

        selectedTicketName() {

            const ticket =
                this.tickets.find(
                    x => x.Id == this.errorForm.IdTicket
                )

            return ticket
                ? ticket.TicketCode
                : 'Chọn phiếu sửa chữa'
        },

        filteredDevices() {

            let list = [...this.devices]

            // SEARCH
            if (this.searchQuery) {

                const q = this.searchQuery
                    .toLowerCase()
                    .trim()

                list = list.filter(x =>

                    (
                        (x.Id || '') +
                        ' ' +

                        (x.AssetCode || '') +
                        ' ' +

                        (x.ItemName || '') +
                        ' ' +

                        (x.Model || '') +
                        ' ' +

                        (x.TicketCode || '') +
                        ' '+

                        (x.SerialNumber || '') +
                        ' ' +

                        (x.DepartmentName || '') +
                        ' ' +

                        (x.LocationName || '') +
                        ' ' +

                        (x.LifeStatus || '')

                    )
                        .toLowerCase()
                        .includes(q)
                )
            }
            //if (this.searchQuery) {

            //    const q = this.searchQuery.toLowerCase()

            //    list = list.filter(x =>
            //        (
            //            (x.id || '')+
            //            (x.name || '') +
            //            (x.serial || '') +
            //            (x.dept || '')
            //        )
            //            .toLowerCase()
            //            .includes(q)
            //    )
            //}

            // FILTER
            if (this.filterDept) {
                list = list.filter(x => x.DepartmentName == this.filterDept)
            }

            // if (this.filterLoca) {
            //     list = list.filter(x =>
            //         x.LocationName == this.filterLoca
            //     )
            // }

            if (this.filterStatus) {
                list = list.filter(x =>
                    x.LifeStatus == this.filterStatus
                )
            }

            if (this.filterGroup) {

                list = list.filter(x =>
                    x.GroupName == this.filterGroup
                )
            }

            // SORT
            if (this.sort.key) {

                list.sort((a, b) => {

                    const A = a[this.sort.key]
                    const B = b[this.sort.key]

                    if (A < B)
                        return -1 * this.sort.dir

                    if (A > B)
                        return 1 * this.sort.dir

                    return 0
                })
            }

            return list
        },

        // Get unique groups for filter dropdown
        groups() {

            return [...new Set(

                this.devices
                    .map(x => x.GroupName)
                    .filter(x => x)

            )]
        },

        //Paginate
        paginatedDevices() {

            const start =
                (this.currentPage - 1) *
                this.pageSize

            return this.filteredDevices.slice(
                start,
                start + this.pageSize
            )
        },

        totalPages() {

            return Math.ceil(
                this.filteredDevices.length / this.pageSize
            )
        },

        pages() {

            const total = this.totalPages
            const current = this.currentPage
            const delta = 1 // số trang hiển thị mỗi bên currentPage

            if (total <= 0) return []

            const range = []

            for (let i = 1; i <= total; i++) {
                if (
                    i === 1 ||
                    i === total ||
                    (i >= current - delta && i <= current + delta)
                ) {
                    range.push(i)
                }
            }

            // Chèn dấu "..." vào chỗ bị nhảy số
            const result = []
            let last = 0

            range.forEach(i => {
                if (last) {
                    if (i - last === 2) {
                        // chỉ nhảy đúng 1 số thì hiện luôn số đó thay vì "..."
                        result.push(last + 1)
                    } else if (i - last > 2) {
                        result.push('...')
                    }
                }
                result.push(i)
                last = i
            })

            return result
        },
        // Get unique locations for filter dropdown
        // locations() {

        //     return [...new Set(
        //         this.devices
        //             .map(x => x.LocationName)
        //             .filter(x => x)
        //     )]
        // },
        deptOptions() {
            return [...new Set(
                this.devices.map(x => x.DepartmentName).filter(x => x)
            )]
        },

        status() {

            return [...new Set(
                this.devices
                    .map(x => x.LifeStatus)
                    .filter(x => x)
                    
            )]

        },

        //formatDate(date) {

        //    if (!date)
        //        return null

        //    return date.substring(0, 10)
        //},

    },

    methods: {

        // ══════════════════════════════════
        // IMPORT EXCEL
        // ══════════════════════════════════

        openImportModal() {
            this.showAddOptionModal = false
            this.importStep = 1
            this.importFile = null
            this.importRows = []
            this.importResult = { success: 0, failed: 0 }
            this.showImportModal = true
        },

        closeImportModal() {
            this.showImportModal = false
            this.importFile = null
            this.importRows = []
            this.importStep = 1
        },

        onFileSelect(e) {
            const file = e.target.files[0]
            if (file) this.importFile = file
        },

        onFileDrop(e) {
            this.importDragOver = false
            const file = e.dataTransfer.files[0]
            if (file && (file.name.endsWith('.xlsx') || file.name.endsWith('.xls'))) {
                this.importFile = file
            } else {
                alert('Chỉ hỗ trợ file .xlsx hoặc .xls')
            }
        },

        parseExcel() {
            if (!this.importFile) return
            this.importParsing = true

            const reader = new FileReader()
            reader.onload = (e) => {
                try {
                    const data = new Uint8Array(e.target.result)
                    const wb = XLSX.read(data, { type: 'array', cellDates: true })
                    const ws = wb.Sheets[wb.SheetNames[0]]

                    // Đọc từ row 3 (row 1-2 là header mô tả, row 3 là tên cột)
                    const raw = XLSX.utils.sheet_to_json(ws, {
                        header: 1,
                        range: 2,       // bỏ 2 row đầu (title + mô tả)
                        defval: ''
                    })

                    if (raw.length < 2) {
                        alert('File không có dữ liệu hoặc sai template!')
                        this.importParsing = false
                        return
                    }

                    // Row 0 là header cột
                    const headers = raw[0]
                    const dataRows = raw.slice(1).filter(r => r.some(c => c !== ''))

                    this.importRows = dataRows.map(row => {
                        const obj = {}
                        headers.forEach((h, i) => { obj[h] = row[i] })
                        return this.validateImportRow(obj)
                    })

                    this.importStep = 2
                } catch (err) {
                    alert('Lỗi đọc file: ' + err.message)
                }
                this.importParsing = false
            }
            reader.readAsArrayBuffer(this.importFile)
        },

        validateImportRow(raw) {
            const errors = []

            const row = {
                AssetCode: (raw['Mã tài sản *'] || '').toString().trim(),
                ItemName: (raw['Tên thiết bị *'] || '').toString().trim(),
                SerialNumber: (raw['Serial Number'] || '').toString().trim(),
                Quantity: parseInt(raw['Số lượng']) || 1,
                DepartmentName: (raw['Khoa'] || '').toString().trim(),
                // LocationName: (raw['Vị trí'] || '').toString().trim(),
                ImportDate: this.parseExcelDate(raw['Ngày nhập']),
                WarrantyExpiry: this.parseExcelDate(raw['Hết bảo hành']),
                UnitPrice: parseFloat((raw['Đơn giá'] || '0').toString().replace(/,/g, '')) || 0,
                YearManufactured: parseInt(raw['Năm sản xuất']) || null,
                YearInUse: parseInt(raw['Năm sử dụng']) || null,
                DepreciationRate: parseFloat(raw['Khấu hao (%)']) || null,
                DepreciationYears: parseInt(raw['Số năm khấu hao']) || null,
                AssetCategory: (raw['Loại tài sản'] || '').toString().trim(),
                Manufacturer: (raw['Nhà sản xuất'] || '').toString().trim(),
                SupplierName: (raw['Nhà cung cấp'] || '').toString().trim(),
                CountryManufactured: (raw['Nước sản xuất'] || '').toString().trim(),
                Note: (raw['Ghi chú'] || '').toString().trim(),
                _errors: []
            }

            // Validate bắt buộc
            if (!row.AssetCode) errors.push('Thiếu mã tài sản')
            if (!row.ItemName) errors.push('Thiếu tên thiết bị')
            if (row.Quantity < 1) errors.push('Số lượng không hợp lệ')

            // Validate khoa / vị trí khớp dropdown (warning, không block)
            if (row.DepartmentName && !this.departments.find(d => d.Name === row.DepartmentName)) {
                errors.push('Khoa "' + row.DepartmentName + '" không tồn tại')
            }
            if (row.LocationName && !this.locationsData.find(l => l.Name === row.LocationName)) {
                errors.push('Vị trí "' + row.LocationName + '" không tồn tại')
            }

            row._errors = errors
            return row
        },

        parseExcelDate(val) {
            if (!val) return null
            // SheetJS trả về Date object khi cellDates: true
            if (val instanceof Date) {
                return val.toISOString().split('T')[0]
            }
            // String dạng dd/mm/yyyy hoặc yyyy-mm-dd
            const s = val.toString().trim()
            if (!s) return null
            if (/^\d{4}-\d{2}-\d{2}/.test(s)) return s.substring(0, 10)
            const parts = s.split('/')
            if (parts.length === 3) {
                const [d, m, y] = parts
                return `${y.padStart(4, '0')}-${m.padStart(2, '0')}-${d.padStart(2, '0')}`
            }
            return s
        },

        submitImport() {
            const validRows = this.importRows.filter(r => r._errors.length === 0)
            if (validRows.length === 0) return

            this.importLoading = true

            // Map sang format API
            const payload = validRows.map(row => ({
                AssetCode: row.AssetCode,
                ItemId: this.resolveItemId(row.ItemName),
                SerialNumber: row.SerialNumber || null,
                Quantity: row.Quantity,
                DepartmentId: this.resolveDeptId(row.DepartmentName),
                // LocationId: this.resolveLocId(row.LocationName),
                ImportDate: row.ImportDate,
                WarrantyExpiry: row.WarrantyExpiry,
                UnitPrice: row.UnitPrice,
                YearManufactured: row.YearManufactured,
                YearInUse: row.YearInUse,
                DepreciationRate: row.DepreciationRate,
                DepreciationYears: row.DepreciationYears,
                AssetCategory: row.AssetCategory || null,
                Manufacturer: row.Manufacturer || null,
                SupplierName: row.SupplierName || null,
                CountryManufactured: row.CountryManufactured || null,
                Note: row.Note || null,
                CreatedBy: JSON.parse(localStorage.getItem('current_user')).Id
            }))

            $.ajax({
                url: '/api/device/import',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload),
                success: (res) => {
                    this.importResult = {
                        success: res.success,
                        failed: this.importErrorCount
                    }
                    this.importStep = 3
                    this.loadDevices()
                },
                error: (xhr) => {
                    alert('Import thất bại: ' + xhr.responseText)
                },
                complete: () => {
                    this.importLoading = false
                }
            })
        },

        // Helper resolve tên → Id
        resolveItemId(name) {
            const found = this.items.find(i => i.Name === name)
            return found ? found.Id : null
        },
        resolveDeptId(name) {
            if (!name) return null
            const found = this.departments.find(d => d.Name === name)
            return found ? found.Id : null
        },
        resolveLocId(name) {
            if (!name) return null
            const found = this.locationsData.find(l => l.Name === name)
            return found ? found.Id : null
        },

        // ══════════════════════════════════
        // DOWNLOAD TEMPLATE
        // ══════════════════════════════════
        downloadTemplate() {
            window.location.href = '/template/inventory'
        },
        //downloadTemplate() {
        //    const headers = [
        //        'Mã tài sản', 'Tên thiết bị', 'Serial Number', 'Số lượng',
        //        'Khoa', 'Vị trí', 'Ngày nhập', 'Hết bảo hành',
        //        'Đơn giá', 'Năm sản xuất', 'Năm sử dụng',
        //        'Khấu hao (%)', 'Số năm khấu hao',
        //        'Loại tài sản', 'Nhà sản xuất', 'Nhà cung cấp',
        //        'Nước sản xuất', 'Ghi chú'
        //    ]

        //    // Index các cột cần dropdown (0-based, tính từ header row)
        //    // Col 1 = Tên thiết bị, Col 4 = Khoa, Col 5 = Vị trí, Col 13 = Loại tài sản
        //    const COL_ITEM = 1
        //    const COL_DEPT = 4
        //    const COL_LOC = 5
        //    const COL_GROUP = 13

        //    const dept1 = this.departments[0]?.Name || 'Khoa Nội'
        //    const dept2 = this.departments[1]?.Name || 'Khoa Ngoại'
        //    const dept3 = this.departments[2]?.Name || 'Khoa ICU'
        //    const loc1 = this.locationsData[0]?.Name || 'Tầng 1 - P101'
        //    const loc2 = this.locationsData[1]?.Name || 'Tầng 2 - P201'
        //    const loc3 = this.locationsData[2]?.Name || 'Tầng 3 - P301'
        //    const grp1 = this.groupsData[0]?.Name || 'Thiết bị chẩn đoán'
        //    const grp2 = this.groupsData[1]?.Name || 'Thiết bị điều trị'
        //    const item1 = this.items[0]?.Name || 'Máy đo huyết áp'
        //    const item2 = this.items[1]?.Name || 'Máy thở'
        //    const item3 = this.items[2]?.Name || 'Máy siêu âm'
        //    const item4 = this.items[3]?.Name || 'Máy theo dõi bệnh nhân'
        //    const item5 = this.items[4]?.Name || 'Đèn phẫu thuật'

        //    const sampleRows = [
        //        [
        //            'TS-2024-001', item1, 'SN-OMR-001', 1,
        //            dept1, loc1, '2024-01-15', '2027-01-15',
        //            4500000, 2022, 2024, 10, 10,
        //            grp1, 'Omron', 'Công ty Thiết Bị Y Tế Miền Nam', 'Nhật Bản', 'Mẫu 1'
        //        ],
        //        [
        //            'TS-2024-002', item2, 'SN-PHL-002', 1,
        //            dept2, loc2, '2024-03-20', '2029-03-20',
        //            85000000, 2023, 2024, 10, 10,
        //            grp2, 'Philips', 'Công ty Dược Phẩm TW', 'Hà Lan', 'Mẫu 2'
        //        ],
        //        [
        //            'TS-2024-003', item3, 'SN-SIE-003', 1,
        //            dept3, loc3, '2024-05-10', '2029-05-10',
        //            320000000, 2023, 2024, 10, 10,
        //            grp1, 'Siemens', 'Công ty Medtronic VN', 'Đức', ''
        //        ],
        //    ]

        //    // ── Sheet 1: Import data ──────────────────────────
        //    const wsData = [
        //        ['TEMPLATE IMPORT THIẾT BỊ — Không xóa/sửa 3 dòng đầu. Các cột có (*) bắt buộc. Cột màu vàng chọn từ dropdown.'],
        //        ['(*) Bắt buộc: Mã tài sản, Tên thiết bị  |  Ngày: YYYY-MM-DD  |  Đơn giá: số nguyên (VND)'],
        //        headers,
        //        ...sampleRows
        //    ]

        //    const ws = XLSX.utils.aoa_to_sheet(wsData)

        //    ws['!cols'] = [
        //        { wch: 14 }, { wch: 26 }, { wch: 16 }, { wch: 10 },
        //        { wch: 22 }, { wch: 22 }, { wch: 12 }, { wch: 13 },
        //        { wch: 14 }, { wch: 13 }, { wch: 12 }, { wch: 13 },
        //        { wch: 16 }, { wch: 22 }, { wch: 20 }, { wch: 24 },
        //        { wch: 14 }, { wch: 28 }
        //    ]

        //    ws['!merges'] = [
        //        { s: { r: 0, c: 0 }, e: { r: 0, c: headers.length - 1 } },
        //        { s: { r: 1, c: 0 }, e: { r: 1, c: headers.length - 1 } },
        //    ]

        //    // ── Sheet 2: Danh mục (dropdown source) ──────────
        //    // Excel data validation chỉ nhận named range hoặc list string ngắn.
        //    // Cách an toàn nhất: để list trong sheet riêng, reference bằng tên sheet.
        //    const maxRef = Math.max(
        //        this.items.length,
        //        this.departments.length,
        //        this.locationsData.length,
        //        this.groupsData.length
        //    )

        //    const refRows = [
        //        ['Tên thiết bị', '', 'Khoa', '', 'Vị trí', '', 'Loại tài sản']
        //    ]
        //    for (let i = 0; i < maxRef; i++) {
        //        refRows.push([
        //            this.items[i]?.Name || '', '',
        //            this.departments[i]?.Name || '', '',
        //            this.locationsData[i]?.Name || '', '',
        //            this.groupsData[i]?.Name || ''
        //        ])
        //    }

        //    const wsRef = XLSX.utils.aoa_to_sheet(refRows)
        //    wsRef['!cols'] = [
        //        { wch: 30 }, { wch: 3 },
        //        { wch: 24 }, { wch: 3 },
        //        { wch: 24 }, { wch: 3 },
        //        { wch: 24 }
        //    ]

        //    // ── Build workbook ────────────────────────────────
        //    const wb = XLSX.utils.book_new()
        //    XLSX.utils.book_append_sheet(wb, ws, 'Import Thiết Bị')
        //    XLSX.utils.book_append_sheet(wb, wsRef, 'DanhMuc')

        //    // ── Data Validation (dropdown) ────────────────────
        //    // SheetJS community hỗ trợ !dataValidations từ v0.18
        //    // Áp dụng cho row 4 → 203 (200 dòng data, row index 3..202, Excel row 4..203)
        //    const DATA_START_ROW = 3   // 0-based (row index 4 trong Excel)
        //    const DATA_END_ROW = 202 // 200 dòng

        //    const itemCount = this.items.length
        //    const deptCount = this.departments.length
        //    const locCount = this.locationsData.length
        //    const groupCount = this.groupsData.length

        //    // Helper: chuyển col index → letter (0=A, 1=B, ...)
        //    const colLetter = (c) => XLSX.utils.encode_col(c)

        //    // Helper: tạo sqref cho 1 cột, nhiều row
        //    const sqref = (col, r1, r2) =>
        //        `${colLetter(col)}${r1 + 1}:${colLetter(col)}${r2 + 1}`

        //    ws['!dataValidations'] = [
        //        // Tên thiết bị (col B) → DanhMuc!$A$2:$A${itemCount+1}
        //        {
        //            type: 'list',
        //            sqref: sqref(COL_ITEM, DATA_START_ROW, DATA_END_ROW),
        //            formula1: `DanhMuc!$A$2:$A$${itemCount + 1}`,
        //            showDropDown: false,   // false = HIỆN dropdown arrow
        //            showErrorMessage: true,
        //            errorTitle: 'Giá trị không hợp lệ',
        //            error: 'Vui lòng chọn tên thiết bị từ danh sách',
        //            showInputMessage: true,
        //            promptTitle: 'Tên thiết bị',
        //            prompt: 'Chọn từ danh sách thiết bị trong hệ thống'
        //        },
        //        // Khoa (col E) → DanhMuc!$C$2:$C${deptCount+1}
        //        {
        //            type: 'list',
        //            sqref: sqref(COL_DEPT, DATA_START_ROW, DATA_END_ROW),
        //            formula1: `DanhMuc!$C$2:$C$${deptCount + 1}`,
        //            showDropDown: false,
        //            showErrorMessage: true,
        //            errorTitle: 'Khoa không tồn tại',
        //            error: 'Vui lòng chọn khoa từ danh sách',
        //            showInputMessage: true,
        //            promptTitle: 'Khoa',
        //            prompt: 'Chọn từ danh sách khoa trong hệ thống'
        //        },
        //        // Vị trí (col F) → DanhMuc!$E$2:$E${locCount+1}
        //        {
        //            type: 'list',
        //            sqref: sqref(COL_LOC, DATA_START_ROW, DATA_END_ROW),
        //            formula1: `DanhMuc!$E$2:$E$${locCount + 1}`,
        //            showDropDown: false,
        //            showErrorMessage: true,
        //            errorTitle: 'Vị trí không tồn tại',
        //            error: 'Vui lòng chọn vị trí từ danh sách',
        //            showInputMessage: true,
        //            promptTitle: 'Vị trí',
        //            prompt: 'Chọn từ danh sách vị trí trong hệ thống'
        //        },
        //        // Loại tài sản (col N) → DanhMuc!$G$2:$G${groupCount+1}
        //        {
        //            type: 'list',
        //            sqref: sqref(COL_GROUP, DATA_START_ROW, DATA_END_ROW),
        //            formula1: `DanhMuc!$G$2:$G$${groupCount + 1}`,
        //            showDropDown: false,
        //            showErrorMessage: true,
        //            errorTitle: 'Loại tài sản không hợp lệ',
        //            error: 'Vui lòng chọn loại tài sản từ danh sách',
        //            showInputMessage: true,
        //            promptTitle: 'Loại tài sản',
        //            prompt: 'Chọn từ danh sách loại tài sản trong hệ thống'
        //        },
        //        // Ngày nhập (col G) — date validation
        //        {
        //            type: 'date',
        //            sqref: sqref(6, DATA_START_ROW, DATA_END_ROW),
        //            operator: 'greaterThan',
        //            formula1: '1900-01-01',
        //            showErrorMessage: true,
        //            errorTitle: 'Ngày không hợp lệ',
        //            error: 'Nhập ngày theo định dạng YYYY-MM-DD',
        //            showInputMessage: true,
        //            promptTitle: 'Ngày nhập',
        //            prompt: 'Định dạng: YYYY-MM-DD (VD: 2024-01-15)'
        //        },
        //        // Số lượng (col D) — số nguyên ≥ 1
        //        {
        //            type: 'whole',
        //            sqref: sqref(3, DATA_START_ROW, DATA_END_ROW),
        //            operator: 'greaterThanOrEqual',
        //            formula1: '1',
        //            showErrorMessage: true,
        //            errorTitle: 'Số lượng không hợp lệ',
        //            error: 'Số lượng phải là số nguyên ≥ 1',
        //        },
        //        // Đơn giá (col I) — số ≥ 0
        //        {
        //            type: 'decimal',
        //            sqref: sqref(8, DATA_START_ROW, DATA_END_ROW),
        //            operator: 'greaterThanOrEqual',
        //            formula1: '0',
        //            showErrorMessage: true,
        //            errorTitle: 'Đơn giá không hợp lệ',
        //            error: 'Đơn giá phải là số ≥ 0',
        //        }
        //    ]

        //    XLSX.writeFile(wb, 'Template_Import_ThietBi.xlsx')
        //},

        // ============================================
        // EXPORT EXCEL
        // ============================================

        exportExcel() {
            // Lấy data theo filter hiện tại, giới hạn pageSize
            const data = this.filteredDevices.slice(0, this.pageSize)

            if (data.length === 0) {
                alert('Không có dữ liệu để xuất')
                return
            }

            // ── Map sang object có tên cột tiếng Việt ──
            const rows = data.map((x, i) => ({
                'STT': i + 1,
                'Mã ID': x.Id || '',
                'Mã tài sản': x.AssetCode || '',
                'Tên thiết bị': x.ItemName || '',
                'Model': x.Model || '',
                'Serial': x.SerialNumber || '',
                'Hãng': x.Brand || '',
                'Loại tài sản': x.GroupName || '',
                'Khoa': x.DepartmentName || '',
                // 'Vị trí': x.LocationName || '',
                'Trạng thái': this.getLifeStatusMeta(x.LifeStatus).text,
                'Đơn giá': x.UnitPrice || 0,
                'Tổng giá trị': x.TotalPrice || 0,
                'Khấu hao (%)': x.DepreciationRate || '',
                'Số năm khấu hao': x.DepreciationYears || '',
                'Giá trị còn lại': x.ResidualValue || '',
                'Năm sản xuất': x.YearManufactured || '',
                'Năm sử dụng': x.YearInUse || '',
                'Số năm hoạt động': x.UsageYears || '',
                'Phân loại tài sản': x.AssetCategory || '',
                'Mã tập đoàn': x.GroupAssetCode || '',
                'Mã kế toán': x.AccountingCode || '',
                'Mã bảo hiểm': x.InsuranceCode || '',
                'Nước sản xuất': x.CountryManufactured || '',
                'Nhà sản xuất': x.Manufacturer || '',
                'Nhà cung cấp': x.SupplierName || '',
                'Ngày nhập': x.ImportDate ? x.ImportDate.substring(0, 10) : '',
                'Hết bảo hành': x.WarrantyExpiry ? x.WarrantyExpiry.substring(0, 10) : '',
                'Phiếu': x.TicketCode || '',
                'Người tạo': x.CreatedByName || '',
                'Ngày tạo': x.CreatedAt ? x.CreatedAt.substring(0, 10) : '',
            }))

            // ── Tạo worksheet ──
            const ws = XLSX.utils.json_to_sheet(rows, { origin: 'A3' })

            // ── Tiêu đề lớn (row 1) ──
            XLSX.utils.sheet_add_aoa(ws, [
                ['DANH SÁCH TÀI SẢN THIẾT BỊ Y TẾ'],
                ['Xuất ngày: ' + new Date().toLocaleDateString('vi-VN') +
                    '   |   Số bản ghi: ' + data.length +
                    (this.filterGroup ? '   |   Loại: ' + this.filterGroup : '') +
                    (this.filterLoca ? '   |   Vị trí: ' + this.filterLoca : '') +
                    (this.filterStatus ? '   |   Trạng thái: ' + this.statusLabel(this.filterStatus) : '') +
                    (this.searchQuery ? '   |   Tìm kiếm: "' + this.searchQuery + '"' : '')]
            ], { origin: 'A1' })

            // ── Độ rộng cột ──
            ws['!cols'] = [
                { wch: 5 },  // STT
                { wch: 8 },  // Mã ID
                { wch: 16 },  // Mã tài sản
                { wch: 28 },  // Tên thiết bị
                { wch: 16 },  // Model
                { wch: 18 },  // Serial
                { wch: 14 },  // Hãng
                { wch: 20 },  // Loại tài sản
                { wch: 20 },  // Khoa
                { wch: 16 },  // Trạng thái
                { wch: 14 },  // Đơn giá
                { wch: 14 },  // Tổng giá trị
                { wch: 13 },  // Khấu hao
                { wch: 15 },  // Số năm KH
                { wch: 15 },  // Giá trị còn lại
                { wch: 13 },  // Năm SX
                { wch: 13 },  // Năm SD
                { wch: 16 },  // Số năm HĐ
                { wch: 18 },  // Phân loại
                { wch: 16 },  // Mã tập đoàn
                { wch: 14 },  // Mã KT
                { wch: 14 },  // Mã BH
                { wch: 16 },  // Nước SX
                { wch: 20 },  // Nhà SX
                { wch: 22 },  // Nhà CC
                { wch: 12 },  // Ngày nhập
                { wch: 13 },  // Hết BH
                { wch: 14 },  // Phiếu
                { wch: 18 },  // Người tạo
                { wch: 12 },  // Ngày tạo
            ]

            // ── Merge tiêu đề lớn qua tất cả cột ──
            const colCount = Object.keys(rows[0]).length
            ws['!merges'] = [
                { s: { r: 0, c: 0 }, e: { r: 0, c: colCount - 1 } },
                { s: { r: 1, c: 0 }, e: { r: 1, c: colCount - 1 } },
            ]

            // ── Style (SheetJS free chỉ hỗ trợ cơ bản, dùng cellStyles nếu có Pro) ──
            // Với SheetJS community, set số format cho cột tiền
            const numFmt = '#,##0'
            const totalRows = rows.length + 3  // header 2 + col header 1 + data
            for (let r = 4; r <= totalRows; r++) {
                const cellL = XLSX.utils.encode_cell({ r: r - 1, c: 11 }) // Đơn giá col L
                const cellM = XLSX.utils.encode_cell({ r: r - 1, c: 12 }) // Tổng giá trị col M
                const cellP = XLSX.utils.encode_cell({ r: r - 1, c: 15 }) // Giá trị còn lại
                if (ws[cellL]) ws[cellL].z = numFmt
                if (ws[cellM]) ws[cellM].z = numFmt
                if (ws[cellP]) ws[cellP].z = numFmt
            }

            // ── Tạo workbook và xuất file ──
            const wb = XLSX.utils.book_new()
            XLSX.utils.book_append_sheet(wb, ws, 'Danh sách tài sản')

            const fileName = 'TaiSan_' +
                new Date().toISOString().substring(0, 10).replace(/-/g, '') +
                '_' + data.length + 'bk.xlsx'

            XLSX.writeFile(wb, fileName)
        },



        // ============================================
        // REPLACE DEVICE - Thay thế tài sản
        // ============================================
        openReplaceModal() {
            if (!this.selectedDevice) return
            const status = this.selectedDevice.LifeStatus
            if (status !== 'suspended' && status !== 'liquidated' && status !== 'replaced') {
                alert('Chỉ có thể thay thế tài sản đang Tạm ngưng hoặc đã Thanh lý!')
                return
            }
            this.replaceNote = ''
            this.selectedReplaceId = null
            this.replaceableDevices = []
            this.showReplaceModal = true
            this.loadReplaceableDevices()
        },
        loadReplaceableDevices() {
            this.replaceLoading = true
            $.get('/api/device/active-replaceable?inventoryId=' + this.selectedDevice.Id, (res) => {
                this.replaceableDevices = res
                this.replaceLoading = false
            }).fail(() => {
                alert('Không tải được danh sách tài sản thay thế')
                this.replaceLoading = false
            })
        },
        confirmReplace() {
            if (!this.selectedReplaceId) {
                alert('Vui lòng chọn tài sản thay thế!')
                return
            }
            const replaceName = this.replaceableDevices.find(d => d.Id == this.selectedReplaceId)
            const confirmMsg = `Xác nhận thay thế "${this.selectedDevice.AssetCode}" bằng "${replaceName ? replaceName.AssetCode : this.selectedReplaceId}"?`
            if (!confirm(confirmMsg)) return
            const currentUser = JSON.parse(localStorage.getItem('current_user'))
            $.ajax({
                url: '/api/device/replace',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    OldInventoryId: this.selectedDevice.Id,
                    NewInventoryId: this.selectedReplaceId,
                    ReplacedBy: currentUser ? currentUser.Id : 1,
                    Note: this.replaceNote
                }),
                success: () => {
                    alert('✅ Thay thế tài sản thành công! Lịch sử bệnh án đã được ghi nhận.')
                    this.showReplaceModal = false
                    this.showModal = false
                    this.loadDevices()
                },
                error: (xhr) => {
                    alert('Thay thế thất bại: ' + xhr.responseText)
                }
            })
        },

        showMaintenanceSchedule() {
            if (!this.selectedDevice) return

            const user = JSON.parse(localStorage.getItem('current_user'))

            this.scheduleForm = {
                InventoryId: this.selectedDevice.Id,
                ScheduleName: '',
                MaintenanceType: 'preventive',
                LastMaintenanceDate: null,
                NextMaintenanceDate: null,
                ReminderDays: 15,
                IsRecurring: true,
                RecurringMonths: 3,
                CreatedBy: user ? user.Id : 1
            }

            this.loadMaintenanceSchedules(this.selectedDevice.Id)
            this.showScheduleModal = true
        },

        loadMaintenanceSchedules(inventoryId) {
            $.get('/api/maintenance-schedule/list?inventoryId=' + inventoryId, (res) => {
                this.maintenanceSchedules = res
            })
        },

        saveSchedule() {
            if (!this.scheduleForm.ScheduleName.trim()) {
                alert('Nhập tên lịch bảo trì')
                return
            }
            if (!this.scheduleForm.NextMaintenanceDate) {
                alert('Chọn ngày bảo trì tiếp theo')
                return
            }

            $.ajax({
                url: '/api/maintenance-schedule/create',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(this.scheduleForm),
                success: () => {
                    alert('Đã thêm lịch bảo trì')
                    this.loadMaintenanceSchedules(this.scheduleForm.InventoryId)
                    // Reset form nhưng giữ modal mở để thêm tiếp
                    this.scheduleForm.ScheduleName = ''
                    this.scheduleForm.LastMaintenanceDate = null
                    this.scheduleForm.NextMaintenanceDate = null
                },
                error: (xhr) => {
                    alert('Lỗi: ' + xhr.responseText)
                }
            })
        },

        deleteSchedule(id) {
            if (!confirm('Xóa lịch bảo trì này?')) return

            const user = JSON.parse(localStorage.getItem('current_user'))

            $.ajax({
                url: '/api/maintenance-schedule/delete',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ Id: id, DeletedBy: user ? user.Id : 1 }),
                success: () => {
                    this.loadMaintenanceSchedules(this.selectedDevice.Id)
                },
                error: () => {
                    alert('Xóa thất bại')
                }
            })
        },

        // Badge màu dựa vào số ngày còn lại
        getUrgencyMeta(days) {
            if (days < 0) return { text: 'Quá hạn', bg: '#fee2e2', color: '#dc2626' }
            if (days <= 3) return { text: days + ' ngày', bg: '#fee2e2', color: '#dc2626' }
            if (days <= 7) return { text: days + ' ngày', bg: '#ffedd5', color: '#ea580c' }
            if (days <= 15) return { text: days + ' ngày', bg: '#fef9c3', color: '#ca8a04' }
            return { text: days + ' ngày', bg: '#dcfce7', color: '#16a34a' }
        },


        selectTicket(ticket) {

            this.errorForm.IdTicket =
                ticket.Id

            this.showTicketDropdown =
                false

            this.ticketSearch = ''
        },

        onQrScanned(decodedText) {

            const url = new URL(decodedText)

            const inventoryId =
                url.searchParams.get('inventoryId')

            if (!inventoryId)
                return

            $.get(
                '/api/device/detail?id=' + inventoryId,
                (res) => {

                    this.qrDevice = res

                    this.showQrModal = false

                    this.showQrActionModal = true
                }
            )
        },

        viewQrDevice() {

            //this.showQrActionModal = false

            //this.selectedDevice =
            //    this.qrDevice

            //this.showModal = true
            if (!this.qrDevice)
                return

            this.showQrActionModal = false

            this.openDetail(this.qrDevice.Id)
        },

        requestRepair() {

            //this.showQrActionModal = false

            //this.openErrorModal(this.qrDevice)
            this.showQrActionModal = false
            if (this.qrDevice && this.qrDevice.AssetCode) {
                window.location.href = '/CreateTicket/Index?ticketType=REPAIR&assetType=' + encodeURIComponent('Khác') + '&assetCode=' + encodeURIComponent(this.qrDevice.AssetCode);
            } else {
                window.location.href = '/CreateTicket/Index?ticketType=REPAIR&assetType=' + encodeURIComponent('Khác');
            }
        },

        openChecklist() {

            if (!this.qrDevice) {

                this.showQrActionModal = false

                window.location.href = '/Checklists/Index?inventoryId=' + this.qrDevice.Id
            }
            else{
                alert("Không tìm thấy tài sản")
            }

        },


        //showDeviceQr() {
        //    this.showQrInDetail = true
        //    this.$nextTick(() => {
        //        const container = document.getElementById('detail-qr-container')
        //        if (!container) return
        //        container.innerHTML = ''
        //        const url = window.location.origin + '/Inventory/Index?inventoryId=' + this.selectedDevice.Id
        //        new QRCode(container, {
        //            text: url,
        //            width: 180,
        //            height: 180,
        //            colorDark: '#1a1a1a',
        //            colorLight: '#ffffff',
        //            correctLevel: QRCode.CorrectLevel.H
        //        })
        //    })
        //},
        showDeviceQr() {
            this.showQrInDetail = true
            this.$nextTick(() => {
                const container = document.getElementById('detail-qr-container')
                if (!container) return
                container.innerHTML = ''
                const url = window.location.origin + '/Inventory/Index?actionQr=' + this.selectedDevice.Id
                new QRCode(container, {
                    text: url,
                    width: 180,
                    height: 180,
                    colorDark: '#1a1a1a',
                    colorLight: '#ffffff',
                    correctLevel: QRCode.CorrectLevel.H
                })
            })
        },

        downloadDetailQr() {
            const canvas = document.querySelector('#detail-qr-container canvas')
            if (!canvas) return
            const link = document.createElement('a')
            link.download = 'QR-' + this.selectedDevice.AssetCode + '.png'
            link.href = canvas.toDataURL()
            link.click()
        },

        printDetailQr() {
            const canvas = document.querySelector('#detail-qr-container canvas')
            if (!canvas) return
            const win = window.open('')
            win.document.write(`
        <div style="font-family:sans-serif;text-align:center;padding:20px">
            <img src="${canvas.toDataURL()}" style="width:200px"/>
            <p style="font-size:14px;font-weight:bold;margin-top:8px">
                ${this.selectedDevice.AssetCode}
            </p>
            <p style="font-size:12px;color:#555">
                ${this.selectedDevice.ItemName}
            </p>
        </div>
    `)
            win.print()
            win.close()
        },

        openErrorModal(device) {

            this.errorForm = {

                inventoryId: device.Id,

                //ticketId: device.IdTicket || null,
                IdTicket: device.IdTicket || null,

                title: '',

                errorDescription: '',

                priority: 'normal',
                stillWorking: false 
            }

            $('#errorModal').modal('show')
        },

        saveError() {

            if (!this.errorForm.title.trim()) {

                alert('Nhập tiêu đề lỗi')
                return
            }

            if (!this.errorForm.errorDescription.trim()) {

                alert('Nhập mô tả lỗi')
                return
            }

            const currentUser =
                JSON.parse(localStorage.getItem('current_user'));

            $.ajax({

                url: '/api/device/report-error',

                type: 'POST',

                contentType: 'application/json',

                data: JSON.stringify({

                    InventoryId: this.errorForm.inventoryId,

                    TicketId: this.errorForm.IdTicket,

                    Title: this.errorForm.title,

                    ErrorDescription: this.errorForm.errorDescription,

                    Priority: this.errorForm.priority,

                    ReportedBy: currentUser.Id,
                    StillWorking: this.errorForm.stillWorking 
                }),

                success: () => {

                    alert('Báo lỗi thành công')

                    $('#errorModal').modal('hide')

                    this.loadDevices()

                    if (this.selectedDevice) {
                        this.loadMaintenanceHistory(this.selectedDevice.Id)
                    }
                },

                error: (xhr) => {

                    console.log(xhr)

                    alert('Gửi báo lỗi thất bại')
                }
            })
        },

        formatDate(date) {

            if (!date)
                return null

            const d = new Date(date)

            if (isNaN(d.getTime()))
                return null

            return d.toISOString().split('T')[0]
        },

        loadMaintenanceHistory(id) {

            this.activeTab = "history";

            $.get('/api/device/history/' + id, (res) => {

                this.maintenanceHistory = res;

            }).fail(() => {

                alert('Không tải được lịch sử bảo trì');

            });
        },

        openHistoryDetail(record) {
            this.selectedHistory = record;
            this.showHistoryDetail = true;
        },

        loadDropdowns() {

            // ITEMS
            $.get('/api/device/items', (res) => {

                this.items = res

            })

            // DEPARTMENTS
            $.get('/api/device/departments', (res) => {

                this.departments = res

            })

            // LOCATIONS
            $.get('/api/device/locations', (res) => {

                this.locationsData = res

            })

            // TICKETS
            $.get('/api/device/tickets', (res) => {
                console.log(this.tickets)
                this.tickets = res

            })

            // GROUPS
            $.get('/api/device/groups', (res) => {

                this.groupsData = res

            })

            // CHECK CYCLES
            $.get('/api/device/checkcycles')
                .done((res) => {

                    console.log('checkcycles', res)

                    this.checkCycles = res

                })
        },

        getLifeStatusMeta(status) {

            switch (status) {

                case "active":
                    return {
                        text: "Đang hoạt động",
                        bg: "#dcfce7",
                        color: "#16a34a",
                        icon: "🟢"
                    }

                case "suspended":
                    return {
                        text: "Tạm ngưng",
                        bg: "#f1f5f9",
                        color: "#64748b",
                        icon: "⏸️"
                    }

                case "maintenance_bv":
                    return {
                        text: "BV bảo trì",
                        bg: "#ffedd5",
                        color: "#ea580c",
                        icon: "🛠️"
                    }

                case "maintenance_hang":
                    return {
                        text: "Hãng bảo hành",
                        bg: "#ede9fe",
                        color: "#7c3aed",
                        icon: "🏭"
                    }

                case "Send_for_warranty":
                    return {
                        text: "Gửi bảo hành",
                        bg: "#dbeafe",
                        color: "#2563eb",
                        icon: "📦"
                    }

                case "replaced":
                    return {
                        text: "Đã thay thế",
                        bg: "#e0f2fe",
                        color: "#0369a1",
                        icon: "🔁"
                    }

                default:
                    return {
                        text: "Không xác định",
                        bg: "#fee2e2",
                        color: "#dc2626",
                        icon: "❓"
                    }
            }
        },

        changeStatus(item) {

            $.ajax({

                url: '/api/device/status',

                type: 'POST',

                contentType: 'application/json',

                data: JSON.stringify({

                    Id: item.Id,
                    Status: item.LifeStatus

                }),

                success: () => {

                    //console.log('updated')
                    //alert('Đã chuyển sang trạng thái ' + getLifeStatusMeta(item.LifeStatus).text)
                    const statusText =
                        this.getLifeStatusMeta(item.LifeStatus).text

                    alert(`Đã chuyển sang trạng thái: ${statusText}`)

                },

                error: () => {

                    alert('Đổi trạng thái thất bại')

                    this.loadDevices()
                }
            })
        },

        statusLabel(status) {
            return STATUS[status]?.label || status
        },

        statusClass(status) {
            return STATUS[status]?.class || ''
        },

        sortBy(key) {

            if (this.sort.key == key) {
                this.sort.dir *= -1
            }
            else {

                this.sort.key = key
                this.sort.dir = 1
            }
        },

        goDetail() {

            window.location.href =
                '/Inventory/Detail'
        },

        openSuspendModal(device) {

            this.selectedSuspendId = device.Id

            this.suspendReason = ''

            this.showSuspendModal = true
        },

        //Pagination
        changePage(page) {

            if (page < 1 || page > this.totalPages)
                return

            this.currentPage = page
        },

        nextPage() {

            if (this.currentPage < this.totalPages) {
                this.currentPage++
            }
        },

        prevPage() {

            if (this.currentPage > 1) {
                this.currentPage--
            }
        },


        //loadDevices() {

        //    $.ajax({

        //        url: '/api/device/list',

        //        type: 'GET',

        //        success: (res) => {

        //            this.devices = res

        //            // Tự động mở chi tiết thiết bị nếu trỏ từ Alerts Center qua inventoryId
        //            const urlParams = new URLSearchParams(window.location.search);
        //            const invId = urlParams.get('inventoryId');
        //            if (invId) {
        //                const targetId = parseInt(invId);
        //                const device = this.devices.find(d => d.Id === targetId);
        //                if (device) {
        //                    if (!this.searchQuery && device.AssetCode) {
        //                        this.searchQuery = device.AssetCode;
        //                    }
        //                    this.openDetail(targetId);
        //                }
        //            }
        //        },

        //        error: () => {

        //            alert('Load dữ liệu thất bại')
        //        }
        //    })
        //},

        //loadDevices() {

        //    $.ajax({
        //        url: '/api/device/list',
        //        type: 'GET',

        //        success: (res) => {

        //            this.devices = res;
        //            // Tự động mở chi tiết thiết bị nếu trỏ từ Alerts Center qua inventoryId
        //            const urlParams = new URLSearchParams(window.location.search);
        //            //const invId = urlParams.get('inventoryId');
        //            //if (invId) {
        //            //    const targetId = parseInt(invId);
        //            //    const device = this.devices.find(d => d.Id === targetId);
        //            //    if (device) {
        //            //        if (!this.searchQuery && device.AssetCode) {
        //            //            this.searchQuery = device.AssetCode;
        //            //        }
        //            //        this.openDetail(targetId);
        //            //    }
        //            //}
        //            const params =
        //                new URLSearchParams(window.location.search);

        //            const actionQr =
        //                params.get('actionQr');

        //            if (actionQr) {

        //                const targetId =
        //                    parseInt(actionQr);

        //                this.selectedQrInventoryId =
        //                    targetId;

        //                this.showQrActionModal =
        //                    true;
        //            }


        //            //const invId =
        //            //    new URLSearchParams(window.location.search)
        //            //        .get('inventoryId');

        //            if (invId) {
        //                this.openDetail(parseInt(invId));
        //            }
        //        }
        //    });
        //},

        loadDevices() {

            $.ajax({
                url: '/api/device/list',
                type: 'GET',

                success: (res) => {

                    this.devices = res;

                    const params =
                        new URLSearchParams(window.location.search);

                    // QR ACTION
                    const actionQr = params.get('actionQr')

                    if (actionQr) {

                        $.get('/api/device/detail?id=' + actionQr, (res) => {

                            this.qrDevice = res

                            this.showQrActionModal = true

                        })

                        return
                    }

                    // OPEN DETAIL TRUYỀN THẲNG
                    const invId =
                        params.get('inventoryId');

                    if (invId) {

                        const targetId =
                            parseInt(invId);

                        this.openDetail(targetId);
                    }
                },

                error: () => {

                    alert('Load dữ liệu thất bại');
                }
            });
        },


        //openDetail(id) {

        //    $.ajax({

        //        url: '/api/device/detail?id=' + id,

        //        type: 'GET',

        //        success: (res) => {

        //            this.selectedDevice = res

        //            this.showModal = true
        //        },

        //        error: () => {

        //            alert('Không load được chi tiết')
        //        }
        //    })
        //}
        openDetail(id) {
            console.log('Calling detail for id:', id) // check xem id đúng chưa

            this.activeTab = "detail";
            this.maintenanceHistory = [];

            $.ajax({
                url: '/api/device/detail?id=' + id,
                type: 'GET',
                success: (res) => {
                    console.log('Response:', res) // res là object hay array?
                    this.selectedDevice = res
                    this.showModal = true
                },
                error: (xhr) => {
                    console.log('Error:', xhr.status, xhr.responseText)
                    alert('Không load được chi tiết')
                }
            })
        },

        submitSuspend() {

            if (!this.suspendReason.trim()) {

                alert('Nhập lý do lỗi')

                return
            }

            $.ajax({

                url: '/api/device/suspend',

                type: 'POST',

                data: {
                    id: this.selectedSuspendId,
                    reason: this.suspendReason
                },

                success: (res) => {

                    alert('Đã chuyển sang trạng thái tạm ngưng')

                    this.showSuspendModal = false

                    this.showModal = false

                    this.loadDevices()
                },

                error: () => {

                    alert('Có lỗi xảy ra')
                }
            })

        },

        //createInventory() {

        //    if (!this.createForm.ImportDate)
        //        this.createForm.ImportDate = null

        //    if (!this.createForm.ExpiryDate)
        //        this.createForm.ExpiryDate = null

        //    if (!this.createForm.WarrantyExpiry)
        //        this.createForm.WarrantyExpiry = null

        //    $.ajax({
        //        url: '/api/device/create',
        //        type: 'POST',
        //        contentType: 'application/json',
        //        data: JSON.stringify(this.createForm),

        //        success: () => {

        //            alert('Thêm thiết bị thành công')

        //            this.showManualModal = false

        //            this.loadDevices()

        //        },

        //        error: (err) => {

        //            console.log(err)

        //            alert('Thêm thất bại')
        //        }
        //    })
        //},


        // Update createInventory method
        createInventory() {
            if (!this.createForm.ImportDate) this.createForm.ImportDate = null
            if (!this.createForm.ExpiryDate) this.createForm.ExpiryDate = null
            if (!this.createForm.WarrantyExpiry) this.createForm.WarrantyExpiry = null

            $.ajax({
                url: '/api/device/create',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(this.createForm),
                success: (res) => {
                    this.showManualModal = false
                    this.loadDevices()

                    // Hiện modal QR code
                    this.newInventoryQr = {
                        id: res.id,
                        url: res.qrUrl,
                        token: res.qrToken
                    }
                    this.showQrResultModal = true

                    // Gen QR image sau khi modal render
                    this.$nextTick(() => {
                        this.generateQrCode(res.qrUrl)
                    })
                },
                error: (err) => {
                    console.log(err)
                    alert('Thêm thất bại')
                }
            })
        },

        generateQrCode(url) {
            const container = document.getElementById('qr-code-container')
            if (!container) return
            container.innerHTML = ''
            new QRCode(container, {
                text: url,
                width: 220,
                height: 220,
                colorDark: '#1a1a1a',
                colorLight: '#ffffff',
                correctLevel: QRCode.CorrectLevel.H
            })
        },

        downloadQr() {
            const canvas = document.querySelector('#qr-code-container canvas')
            if (!canvas) return
            const link = document.createElement('a')
            link.download = 'QR-' + this.newInventoryQr.id + '.png'
            link.href = canvas.toDataURL()
            link.click()
        },

        //printQr() {
        //    const canvas = document.querySelector('#qr-code-container canvas')
        //    if (!canvas) return
        //    const dataUrl = canvas.toDataURL()
        //    const win = window.open('')
        //    win.document.write('<img src="' + dataUrl + '" style="width:220px"/>')
        //    win.document.write('<p style="font-family:sans-serif;font-size:13px">Mã: ' + this.newInventoryQr.token + '</p>')
        //    win.document.write('<p style="font-family:sans-serif;font-size:11px;color:#555">' + this.newInventoryQr.url + '</p>')
        //    win.print()
        //    win.close()
        //},

        printQr() {
            const canvas = document.querySelector('#qr-code-container canvas')
            if (!canvas) return

            const dataUrl = canvas.toDataURL()

            const win = window.open('')

            win.document.write(`
                <html>
                <head>
                    <style>
                        @@page{
                            size:50mm 50mm;
                            margin:0;
                        }

                        body{
                            margin:0;
                            padding:0;
                            text-align:center;
                            font-family:Arial;
                        }

                        img{
                            width:40mm;
                            height:40mm;
                        }
                    </style>
                </head>
                <body>
                    <img src="${dataUrl}" />
                </body>
                </html>
            `)

            win.document.close()
            win.focus()
            win.print()
        },

        openEditModal(id) {

            $.get('/api/device/detail?id=' + id, (res) => {

                console.log(res)

                console.log(typeof res.ImportDate)
                console.log(res.ImportDate)

                this.editForm = {

                    Id: res.Id,

                    AssetCode: res.AssetCode,
                    ItemId: res.ItemId,

                    SerialNumber: res.SerialNumber,

                    Quantity: res.Quantity,

                    DepartmentId: res.DepartmentId,
                    LocationId: res.LocationId,

                    ImportDate: this.formatDate(res.ImportDate),
                    ExpiryDate: this.formatDate(res.ExpiryDate),
                    WarrantyExpiry: this.formatDate(res.WarrantyExpiry),

                    CheckCycleId: res.CheckCycleId,

                    UnitPrice: res.UnitPrice,

                    DepreciationRate: res.DepreciationRate,
                    DepreciationYears: res.DepreciationYears,
                    ResidualValue: res.ResidualValue,

                    YearManufactured: res.YearManufactured,
                    YearInUse: res.YearInUse,
                    UsageYears: res.UsageYears,

                    AssetCategory: res.AssetCategory,

                    GroupAssetCode: res.GroupAssetCode,
                    AccountingCode: res.AccountingCode,
                    InsuranceCode: res.InsuranceCode,

                    CountryManufactured: res.CountryManufactured,
                    Manufacturer: res.Manufacturer,
                    SupplierName: res.SupplierName,

                    QrCode: res.QrCode,

                    Note: res.Note,

                    IdTicket: res.IdTicket
                }

                this.showEditModal = true
            })
        },

        updateInventory() {
 
            $.ajax({

                url: '/api/device/update',

                type: 'POST',

                contentType: 'application/json',

                data: JSON.stringify(this.editForm),

                success: () => {

                    alert('Cập nhật thành công')

                    this.showEditModal = false

                    this.showModal = false

                    this.loadDevices()
                },

                error: () => {

                    alert('Cập nhật thất bại')
                }
            })
        },



        openManualModal() {

            this.showAddOptionModal = false

            this.showManualModal = true
        }
    },

    watch: {

        showModal(val) {
            if (!val) this.showQrInDetail = false
        },

        pageSize() {
            this.currentPage = 1
        },

        searchQuery() {
            this.currentPage = 1
        },

        // filterLoca() {
        //     this.currentPage = 1
        // },

        filterDept() { this.currentPage = 1 }, 

        filterGroup() {
            this.currentPage = 1
        },

        filterStatus() {
            this.currentPage = 1
        }
    },

    mounted() {
        this.loadDropdowns()
        this.loadDevices()

        // Tự động lọc thiết bị nếu trỏ từ Alerts Center qua searchCode
        const urlParams = new URLSearchParams(window.location.search);
        const code = urlParams.get('searchCode');
        if (code) {
            this.searchQuery = code.trim();
        }
    }
})