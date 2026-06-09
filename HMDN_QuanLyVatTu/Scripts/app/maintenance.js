/* ═══════════════════════════════════════════════════════════
   maintenance.js — Vue 2 Instance cho Nhật ký bệnh án thiết bị
   Tuân thủ: Vue 2.7.16 CDN, jQuery AJAX, delimiters ['${', '}']
   ═══════════════════════════════════════════════════════════ */

var app = new Vue({
    el: '#app',

    delimiters: ['${', '}'],

    data: {
        // Danh sách thiết bị (Inventory)
        devices: [],

        // Bộ lọc danh sách thiết bị
        searchQuery: '',
        filterLogStatus: '', // '' = tất cả, 'has_logs' = có lịch sử, 'no_logs' = chưa có
        filterDepartment: '',
        filterLifeStatus: '', // Bộ lọc trạng thái hoạt động
        kpiFilter: 'bao_hong', // bao_hong | pending | in_progress | repaired | from_ticket

        // Phân trang
        currentPage: 1,
        pageSize: 15,

        // KPI
        kpi: {
            totalDevices: 0,
            devicesWithLogs: 0,
            activeIssues: 0,
            totalRepairs: 0,
            fromTicket: 0
        },

        // Modal lịch sử thiết bị
        showHistoryModal: false,
        selectedDevice: null,
        summaryData: {},
        deviceLogs: [],
        historyLoading: false,

        // Modal chi tiết 1 ca sửa chữa
        showDetailModal: false,
        detail: null,
        showCloseForm: false,
        closeForm: {
            ActionTaken: '',
            PartReplaced: '',
            Vendor: '',
            Cost: null
        },

        // Modal tạo mới
        showCreateModal: false,
        createFromDevice: null, // pre-fill khi tạo từ modal lịch sử
        newLog: {
            InventoryId: 0,
            MaintenanceType: 'corrective',
            Title: '',
            ErrorDescription: '',
            Description: '',
            Priority: 'normal',
            StartDate: ''
        },

        // Danh sách thiết bị (cho dropdown tạo mới)
        deviceList: [],

        // Loading state
        loading: false,

        // Top scrollbar
        tableScrollWidth: 0,
        ignoreScroll: false,

        // Attachments
        attachments: [],
        uploading: false
    },

    computed: {

        filteredDevices: function () {
            var vm = this;
            var list = vm.devices.slice();

            // Chỉ hiển thị thiết bị báo hỏng (hỏng / tạm ngưng / BV bảo trì)
            list = list.filter(function (d) {
                return vm.isBaoHong(d);
            });

            // Lọc theo KPI card được chọn
            if (vm.kpiFilter === 'bao_hong') {
                // Giữ toàn bộ thiết bị báo hỏng
            } else if (vm.kpiFilter === 'pending') {
                list = list.filter(function (d) {
                    return vm.isChuaSuaChua(d);
                });
            } else if (vm.kpiFilter === 'in_progress') {
                list = list.filter(function (d) {
                    return vm.isDangSuaChua(d);
                });
            } else if (vm.kpiFilter === 'repaired') {
                list = list.filter(function (d) {
                    return vm.isDaSuaChua(d);
                });
            } else if (vm.kpiFilter === 'from_ticket') {
                list = list.filter(function (d) {
                    return vm.isFromTicket(d);
                });
            }

            // 1. Tìm kiếm
            if (vm.searchQuery) {
                var q = vm.searchQuery.toLowerCase().trim();
                list = list.filter(function (d) {
                    return (d.AssetCode && d.AssetCode.toLowerCase().indexOf(q) > -1) ||
                           (d.ItemName && d.ItemName.toLowerCase().indexOf(q) > -1) ||
                           (d.SerialNumber && d.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                           (d.DepartmentName && d.DepartmentName.toLowerCase().indexOf(q) > -1);
                });
            }

            // Lọc theo trạng thái hoạt động (nếu có chọn)
            if (vm.filterLifeStatus) {
                list = list.filter(function (d) {
                    return d.LifeStatus === vm.filterLifeStatus;
                });
            }

            // Lọc theo lịch sử sửa chữa
            if (vm.filterLogStatus === 'has_logs') {
                list = list.filter(function (d) { return d.TotalLogs > 0; });
            } else if (vm.filterLogStatus === 'no_logs') {
                list = list.filter(function (d) { return d.TotalLogs === 0; });
            }

            // Lọc theo khoa phòng
            if (vm.filterDepartment) {
                list = list.filter(function (d) {
                    return d.DepartmentName === vm.filterDepartment;
                });
            }

            // Sắp xếp: thiết bị sửa chữa sớm nhất lên đầu
            list.sort(function (a, b) {
                var aTime = a.FirstLogDate ? new Date(a.FirstLogDate).getTime() : Number.MAX_SAFE_INTEGER;
                var bTime = b.FirstLogDate ? new Date(b.FirstLogDate).getTime() : Number.MAX_SAFE_INTEGER;
                if (aTime !== bTime) return aTime - bTime;
                return a.Id - b.Id;
            });

            return list;
        },

        paginatedDevices: function () {
            var start = (this.currentPage - 1) * this.pageSize;
            return this.filteredDevices.slice(start, start + this.pageSize);
        },

        totalPages: function () {
            return Math.ceil(this.filteredDevices.length / this.pageSize);
        },

        pages: function () {
            var total = this.totalPages;
            var current = this.currentPage;
            var pages = [];
            var start = Math.max(1, current - 3);
            var end = Math.min(total, current + 3);
            for (var i = start; i <= end; i++) {
                pages.push(i);
            }
            return pages;
        },

        // Danh sách khoa phòng (unique) cho dropdown lọc
        departmentList: function () {
            var depts = {};
            this.devices.forEach(function (d) {
                if (d.DepartmentName && d.DepartmentName !== '—') {
                    depts[d.DepartmentName] = true;
                }
            });
            return Object.keys(depts).sort();
        }
    },

    watch: {
        searchQuery: function () { this.currentPage = 1; },
        filterLogStatus: function () { this.currentPage = 1; },
        filterDepartment: function () { this.currentPage = 1; },
        filterLifeStatus: function () { this.currentPage = 1; },
        kpiFilter: function () { this.currentPage = 1; },
        pageSize: function () { this.currentPage = 1; }
    },

    methods: {

        // ── Helpers ──

        statusLabel: function (status) {
            var map = {
                'open': 'Chờ xử lý',
                'in_progress': 'Đang sửa',
                'closed': 'Đã đóng'
            };
            return map[status] || status;
        },

        priorityLabel: function (priority) {
            var map = {
                'low': 'Thấp',
                'normal': 'Bình thường',
                'high': 'Cao',
                'critical': 'Khẩn cấp'
            };
            return map[priority] || priority;
        },

        maintenanceTypeLabel: function (type) {
            var map = {
                'corrective': 'Sửa chữa đột xuất',
                'preventive': 'Bảo trì định kỳ',
                'replacement_part': 'Thay thế linh kiện'
            };
            return map[type] || type || 'N/A';
        },

        lifeStatusLabel: function (status) {
            var map = {
                'active': 'Đang sử dụng',
                'suspended': 'Tạm ngưng',
                'disposed': 'Thanh lý',
                'broken': 'Hỏng',
                'maintenance_bv': 'BV Bảo trì'
            };
            return map[status] || status || 'N/A';
        },

        formatCurrency: function (value) {
            if (!value && value !== 0) return '—';
            return Number(value).toLocaleString('vi-VN') + ' ₫';
        },

        isBaoHong: function (d) {
            return d.LifeStatus === 'broken' ||
                   d.LifeStatus === 'suspended' ||
                   d.LifeStatus === 'maintenance_bv';
        },

        isDangSuaChua: function (d) {
            // Chỉ ca đã bắt đầu sửa (in_progress), không tính ca "Chờ xử lý" (open)
            return d.InProgressLogs > 0;
        },

        isDaSuaChua: function (d) {
            return d.ClosedLogs > 0 && d.InProgressLogs === 0 && d.OpenLogs === 0;
        },

        isChuaSuaChua: function (d) {
            // Báo hỏng nhưng chưa bắt đầu sửa (kể cả có phiếu chờ xử lý)
            if (!this.isBaoHong(d)) return false;
            if (this.isDangSuaChua(d)) return false;
            if (this.isDaSuaChua(d)) return false;
            return true;
        },

        isFromTicket: function (d) {
            return d.FromTicket === true;
        },

        setKpiFilter: function (filter) {
            this.kpiFilter = filter;
            this.currentPage = 1;
        },

        calculateKPI: function () {
            var vm = this;

            vm.kpi.totalDevices = vm.devices.filter(function (d) {
                return vm.isChuaSuaChua(d);
            }).length;

            vm.kpi.devicesWithLogs = vm.devices.filter(function (d) {
                return vm.isDangSuaChua(d);
            }).length;

            vm.kpi.activeIssues = vm.devices.filter(function (d) {
                return vm.isDaSuaChua(d);
            }).length;

            vm.kpi.fromTicket = vm.devices.filter(function (d) {
                return vm.isFromTicket(d) && vm.isBaoHong(d);
            }).length;

            vm.kpi.totalRepairs = vm.devices.reduce(function (sum, d) {
                return sum + (Number(d.TotalMaintenanceCost) || 0);
            }, 0);
        },

        // ── Thay đổi trạng thái thiết bị ──
        updateDeviceStatus: function (inventoryId, newStatus) {
            var vm = this;
            $.ajax({
                url: '/api/maintenance/update-device-status',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ InventoryId: inventoryId, LifeStatus: newStatus }),
                success: function (res) {
                    if (res.success) {
                        // Cập nhật lại trong danh sách
                        var device = vm.devices.find(function(d) { return d.Id === inventoryId; });
                        if (device) device.LifeStatus = newStatus;
                        
                        // Cập nhật lại KPI
                        vm.calculateKPI();

                        if (window.MedEquip && MedEquip.toast) {
                            MedEquip.toast('Thành công', 'Đã cập nhật trạng thái thiết bị.', 'success');
                        }
                    } else {
                        alert(res.message || 'Lỗi cập nhật trạng thái');
                    }
                },
                error: function (xhr) {
                    alert('Lỗi hệ thống khi cập nhật trạng thái.');
                    // Rollback: reload lại selectedDevice để dropdown trở về đúng
                    if (vm.showHistoryModal && vm.selectedDevice) {
                        vm.openHistory(vm.selectedDevice.Id);
                    }
                }
            });
        },


        // ── API Calls ──

        loadDevices: function () {
            var vm = this;
            vm.loading = true;

            $.ajax({
                url: '/api/maintenance/inventory-list',
                type: 'GET',
                success: function (res) {
                    vm.devices = res;
                    vm.calculateKPI();
                    vm.loading = false;

                    // Tự động xử lý query parameters trỏ từ Alerts Center qua
                    var urlParams = new URLSearchParams(window.location.search);
                    var invId = urlParams.get('inventoryId');
                    var trigger = urlParams.get('triggerCreate');
                    if (invId) {
                        var targetId = parseInt(invId);
                        var device = vm.devices.find(function (d) { return d.Id === targetId; });
                        if (device) {
                            vm.searchQuery = device.AssetCode;
                            if (trigger === '1') {
                                vm.newLog.InventoryId = targetId;
                                vm.createFromDevice = device;
                                vm.showCreateModal = true;
                                vm.loadDeviceDropdown();
                            }
                        }
                    }
                },
                error: function (xhr) {
                    console.error('Lỗi tải danh sách thiết bị:', xhr.responseText);
                    if (window.MedEquip && MedEquip.toast) {
                        MedEquip.toast('Lỗi', 'Không thể tải danh sách thiết bị.', 'danger');
                    }
                    vm.loading = false;
                }
            });
        },

        openHistory: function (inventoryId) {
            var vm = this;
            vm.historyLoading = true;
            vm.showHistoryModal = true;
            vm.selectedDevice = null;
            vm.deviceLogs = [];

            $.ajax({
                url: '/api/maintenance/history',
                type: 'GET',
                data: { inventoryId: inventoryId },
                success: function (res) {
                    vm.selectedDevice = res.Device;
                    vm.summaryData = {
                        TotalMaintenanceCost: res.TotalMaintenanceCost,
                        TotalLogs: res.TotalLogs,
                        LastMaintenanceDate: res.LastMaintenanceDate,
                        Uptime: res.Uptime
                    };
                    vm.deviceLogs = res.Logs;
                    vm.historyLoading = false;
                    vm.loadAttachments(inventoryId);
                },
                error: function (xhr) {
                    console.error('Lỗi tải lịch sử:', xhr.responseText);
                    if (window.MedEquip && MedEquip.toast) {
                        MedEquip.toast('Lỗi', 'Không thể tải lịch sử thiết bị.', 'danger');
                    }
                    vm.historyLoading = false;
                }
            });
        },

        openDetail: function (id) {
            var vm = this;
            vm.showCloseForm = false;
            vm.closeForm = { ActionTaken: '', PartReplaced: '', Vendor: '', Cost: null };

            $.ajax({
                url: '/api/maintenance/detail?id=' + id,
                type: 'GET',
                success: function (res) {
                    vm.detail = res;
                    vm.showDetailModal = true;
                },
                error: function (xhr) {
                    console.error('Lỗi tải chi tiết:', xhr.responseText);
                    if (window.MedEquip && MedEquip.toast) {
                        MedEquip.toast('Lỗi', 'Không thể tải chi tiết ca sửa chữa.', 'danger');
                    }
                }
            });
        },

        loadDeviceDropdown: function () {
            var vm = this;
            if (vm.deviceList.length > 0) return;

            $.ajax({
                url: '/api/maintenance/devices',
                type: 'GET',
                success: function (res) {
                    vm.deviceList = res;
                },
                error: function (xhr) {
                    console.error('Lỗi tải thiết bị:', xhr.responseText);
                }
            });
        },

        openCreateFromHistory: function () {
            var vm = this;
            if (vm.selectedDevice) {
                vm.newLog.InventoryId = vm.selectedDevice.Id;
                vm.createFromDevice = vm.selectedDevice;
            }
            vm.loadDeviceDropdown();
            vm.showCreateModal = true;
        },

        createLog: function () {
            var vm = this;

            if (!vm.newLog.InventoryId || !vm.newLog.Title) {
                if (window.MedEquip && MedEquip.toast) {
                    MedEquip.toast('Thiếu thông tin', 'Vui lòng chọn thiết bị và nhập tiêu đề.', 'warning');
                }
                return;
            }

            $.ajax({
                url: '/api/maintenance/create',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(vm.newLog),
                success: function (res) {
                    if (res.success) {
                        vm.showCreateModal = false;
                        vm.resetNewLog();
                        vm.loadDevices(); // Reload danh sách thiết bị

                        // Nếu đang ở modal lịch sử, reload lịch sử
                        if (vm.showHistoryModal && vm.selectedDevice) {
                            vm.openHistory(vm.selectedDevice.Id);
                        }

                        if (window.MedEquip && MedEquip.toast) {
                            MedEquip.toast('Thành công', res.message || 'Đã tạo ca sửa chữa.', 'success');
                        }
                    }
                },
                error: function (xhr) {
                    console.error('Lỗi tạo ca:', xhr.responseText);
                    if (window.MedEquip && MedEquip.toast) {
                        MedEquip.toast('Lỗi', 'Không thể tạo ca sửa chữa.', 'danger');
                    }
                }
            });
        },

        updateStatus: function (id, status) {
            var vm = this;

            $.ajax({
                url: '/api/maintenance/update-status',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({ Id: id, Status: status }),
                success: function (res) {
                    if (res.success) {
                        vm.showDetailModal = false;
                        vm.loadDevices();

                        // Reload lịch sử nếu đang mở
                        if (vm.showHistoryModal && vm.selectedDevice) {
                            vm.openHistory(vm.selectedDevice.Id);
                        }

                        if (window.MedEquip && MedEquip.toast) {
                            MedEquip.toast('Thành công', res.message, 'success');
                        }
                    }
                },
                error: function (xhr) {
                    console.error('Lỗi cập nhật:', xhr.responseText);
                    if (window.MedEquip && MedEquip.toast) {
                        MedEquip.toast('Lỗi', 'Không thể cập nhật trạng thái.', 'danger');
                    }
                }
            });
        },

        closeLog: function () {
            var vm = this;

            if (!vm.closeForm.ActionTaken || !vm.closeForm.Cost) {
                if (window.MedEquip && MedEquip.toast) {
                    MedEquip.toast('Thiếu thông tin', 'Vui lòng nhập Biện pháp đã xử lý và Chi phí.', 'warning');
                } else {
                    alert('Vui lòng nhập Biện pháp đã xử lý và Chi phí.');
                }
                return;
            }

            $.ajax({
                url: '/api/maintenance/update-status',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    Id: vm.detail.Id,
                    Status: 'closed',
                    ActionTaken: vm.closeForm.ActionTaken,
                    PartReplaced: vm.closeForm.PartReplaced,
                    Vendor: vm.closeForm.Vendor,
                    Cost: vm.closeForm.Cost ? Number(vm.closeForm.Cost.toString().replace(/[^0-9]/g, '')) : null
                }),
                success: function (res) {
                    if (res.success) {
                        vm.showDetailModal = false;
                        vm.showCloseForm = false;
                        vm.loadDevices();

                        // Reload lịch sử nếu đang mở
                        if (vm.showHistoryModal && vm.selectedDevice) {
                            vm.openHistory(vm.selectedDevice.Id);
                        }

                        if (window.MedEquip && MedEquip.toast) {
                            MedEquip.toast('Hoàn tất', 'Ca sửa chữa đã được đóng thành công.', 'success');
                        }
                    }
                },
                error: function (xhr) {
                    console.error('Lỗi đóng ca:', xhr.responseText);
                    if (window.MedEquip && MedEquip.toast) {
                        MedEquip.toast('Lỗi', 'Không thể đóng ca sửa chữa.', 'danger');
                    }
                }
            });
        },

        resetNewLog: function () {
            this.newLog = {
                InventoryId: 0,
                MaintenanceType: 'corrective',
                Title: '',
                ErrorDescription: '',
                Description: '',
                Priority: 'normal',
                StartDate: ''
            };
            this.createFromDevice = null;
        },

        syncTopScroll: function (e) {
            if (!this.ignoreScroll) {
                this.ignoreScroll = true;
                if (this.$refs.bottomScroll) {
                    this.$refs.bottomScroll.scrollLeft = e.target.scrollLeft;
                }
                var vm = this;
                setTimeout(function() { vm.ignoreScroll = false; }, 20);
            }
        },

        syncBottomScroll: function (e) {
            if (!this.ignoreScroll) {
                this.ignoreScroll = true;
                if (this.$refs.topScroll) {
                    this.$refs.topScroll.scrollLeft = e.target.scrollLeft;
                }
                var vm = this;
                setTimeout(function() { vm.ignoreScroll = false; }, 20);
            }
        },

        updateScrollWidth: function () {
            if (this.$refs.maintTable) {
                this.tableScrollWidth = this.$refs.maintTable.scrollWidth;
            } else {
                this.tableScrollWidth = 0;
            }
        },

        loadAttachments: function (deviceId) {
            var vm = this;
            $.getJSON('/api/maintenance/attachments/' + deviceId, function (data) {
                vm.attachments = data;
            });
        },

        uploadAttachment: function (event) {
            var vm = this;
            var fileInput = event.target;
            if (fileInput.files.length === 0) return;

            var formData = new FormData();
            formData.append('InventoryId', vm.selectedDevice.Id);
            for (var i = 0; i < fileInput.files.length; i++) {
                formData.append('file' + i, fileInput.files[i]);
            }

            vm.uploading = true;
            $.ajax({
                url: '/api/maintenance/upload-attachment',
                type: 'POST',
                data: formData,
                contentType: false,
                processData: false,
                success: function (res) {
                    if (res.success) {
                        vm.loadAttachments(vm.selectedDevice.Id);
                    } else {
                        alert(res.message || 'Lỗi tải file');
                    }
                },
                error: function (xhr) {
                    var errorMsg = 'Lỗi hệ thống khi tải file.';
                    if (xhr.responseText) {
                        try {
                            var response = JSON.parse(xhr.responseText);
                            errorMsg += '\nChi tiết: ' + (response.ExceptionMessage || response.Message || xhr.responseText);
                        } catch(e) {
                            errorMsg += '\n' + xhr.responseText;
                        }
                    }
                    alert(errorMsg);
                },
                complete: function () {
                    vm.uploading = false;
                    fileInput.value = ''; // reset
                }
            });
        },

        formatFileSize: function(bytes) {
            if (bytes === 0) return '0 Byte';
            var i = parseInt(Math.floor(Math.log(bytes) / Math.log(1024)));
            return Math.round(bytes / Math.pow(1024, i), 2) + ' ' + ['Bytes', 'KB', 'MB', 'GB', 'TB'][i];
        }
    },

    mounted: function () {
        this.loadDevices();
        this.updateScrollWidth();
        window.addEventListener('resize', this.updateScrollWidth);
    },

    updated: function () {
        this.updateScrollWidth();
    },

    beforeDestroy: function () {
        window.removeEventListener('resize', this.updateScrollWidth);
    }
});
