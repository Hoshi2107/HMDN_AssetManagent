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
    }
}

var app = new Vue({
    el: '#app',

    delimiters: ['${', '}'],

    data: {
        STATUS: STATUS,

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
            priority: 'normal'
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

        filterLoca: '',

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
                list = list.filter(x =>
                    x.dept == this.filterDept
                )
            }

            if (this.filterLoca) {
                list = list.filter(x =>
                    x.LocationName == this.filterLoca
                )
            }

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

            return Array.from(
                { length: this.totalPages },
                (_, i) => i + 1
            )
        },

        // Get unique locations for filter dropdown
        locations() {

            return [...new Set(
                this.devices
                    .map(x => x.LocationName)
                    .filter(x => x)
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

        showDeviceQr() {
            this.showQrInDetail = true
            this.$nextTick(() => {
                const container = document.getElementById('detail-qr-container')
                if (!container) return
                container.innerHTML = ''
                const url = window.location.origin + '/Inventory/Index?inventoryId=' + this.selectedDevice.Id
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

                ticketId: device.IdTicket || null,

                title: '',

                errorDescription: '',

                priority: 'normal'
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

                    TicketId: this.errorForm.ticketId,

                    Title: this.errorForm.title,

                    ErrorDescription: this.errorForm.errorDescription,

                    Priority: this.errorForm.priority,

                    ReportedBy: currentUser.Id
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
        loadDevices() {

            $.ajax({
                url: '/api/device/list',
                type: 'GET',

                success: (res) => {

                    this.devices = res;
                    // Tự động mở chi tiết thiết bị nếu trỏ từ Alerts Center qua inventoryId
                    const urlParams = new URLSearchParams(window.location.search);
                    const invId = urlParams.get('inventoryId');
                    if (invId) {
                        const targetId = parseInt(invId);
                        const device = this.devices.find(d => d.Id === targetId);
                        if (device) {
                            if (!this.searchQuery && device.AssetCode) {
                                this.searchQuery = device.AssetCode;
                            }
                            this.openDetail(targetId);
                        }
                    }

                    //const invId =
                    //    new URLSearchParams(window.location.search)
                    //        .get('inventoryId');

                    if (invId) {
                        this.openDetail(parseInt(invId));
                    }
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

        openQrModal() {

            this.showAddOptionModal = false

            this.showQrModal = true
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

        filterLoca() {
            this.currentPage = 1
        },

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