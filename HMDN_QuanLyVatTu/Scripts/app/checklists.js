new Vue({
    el: '#app',
    delimiters: ['${', '}'],
    data: {
        activeTab: 'schedules',
        currentUser: { Id: 1, FullName: 'Kỹ thuật viên' },

        // Lists
        schedules: [],
        logs: [],
        checklistItems: [], // Hạng mục của thiết bị đang làm checklist

        // KPIs
        kpi: {
            pending: 0,
            overdue: 0,
            completed: 0,
            passRate: 100
        },

        // Filters
        schedulesFilter: {
            query: '',
            status: 'pending',
            cycleType: '',
            onlyOverdue: false,
            fromDate: '',
            toDate: ''
        },
        logsFilter: {
            query: '',
            result: ''
        },

        // Pagination
        schedulesPage: 1,
        schedulesPerPage: 10,
        logsPage: 1,
        logsPerPage: 10,

        // Modals visibility
        showGenerateModal: false,
        showPerformModal: false,
        showDetailsModal: false,

        // Modals loading states
        performLoading: false,
        isGenerating: false,
        isSubmitting: false,

        // Modal states
        generateForm: {
            fromDate: '',
            toDate: ''
        },
        activeSchedule: {},
        performForm: {
            qrScanned: false,
            note: ''
        },
        showQrScannerArea: false,
        scannedQrInput: '',
        logDetails: null
    },

    watch: {
        'schedulesFilter.query': function () { this.schedulesPage = 1; },
        'schedulesFilter.status': function () { this.schedulesPage = 1; this.schedulesFilter.onlyOverdue = false; },
        'schedulesFilter.cycleType': function () { this.schedulesPage = 1; },
        'schedulesFilter.fromDate': function () { this.schedulesPage = 1; },
        'schedulesFilter.toDate': function () { this.schedulesPage = 1; },
        'logsFilter.query': function () { this.logsPage = 1; },
        'logsFilter.result': function () { this.logsPage = 1; }
    },

    computed: {
        filteredSchedules() {
            var vm = this;
            var q = (vm.schedulesFilter.query || '').trim().toLowerCase();
            return vm.schedules.filter(function (s) {
                var matchQuery = !q || 
                    (s.AssetCode && s.AssetCode.toLowerCase().indexOf(q) > -1) ||
                    (s.ItemName && s.ItemName.toLowerCase().indexOf(q) > -1) ||
                    (s.SerialNumber && s.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                    (s.DepartmentName && s.DepartmentName.toLowerCase().indexOf(q) > -1);
                
                var matchStatus = !vm.schedulesFilter.status || s.Status === vm.schedulesFilter.status;
                var matchCycle = !vm.schedulesFilter.cycleType || s.CycleType === vm.schedulesFilter.cycleType;
                
                var matchOverdue = true;
                if (vm.schedulesFilter.onlyOverdue) {
                    matchOverdue = s.Status === 'pending' && vm.isOverdue(s.DueDate);
                }

                var matchFrom = !vm.schedulesFilter.fromDate || s.ScheduledDate >= vm.schedulesFilter.fromDate;
                var matchTo = !vm.schedulesFilter.toDate || s.ScheduledDate <= vm.schedulesFilter.toDate;
                
                return matchQuery && matchStatus && matchCycle && matchOverdue && matchFrom && matchTo;
            });
        },

        paginatedSchedules() {
            var start = (this.schedulesPage - 1) * this.schedulesPerPage;
            var end = start + this.schedulesPerPage;
            return this.filteredSchedules.slice(start, end);
        },

        schedulesTotalPages() {
            var total = Math.ceil(this.filteredSchedules.length / this.schedulesPerPage);
            return total > 0 ? total : 1;
        },

        filteredLogs() {
            var vm = this;
            var q = (vm.logsFilter.query || '').trim().toLowerCase();
            return vm.logs.filter(function (l) {
                var matchQuery = !q || 
                    (l.AssetCode && l.AssetCode.toLowerCase().indexOf(q) > -1) ||
                    (l.ItemName && l.ItemName.toLowerCase().indexOf(q) > -1) ||
                    (l.SerialNumber && l.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                    (l.DepartmentName && l.DepartmentName.toLowerCase().indexOf(q) > -1);
                
                var matchResult = !vm.logsFilter.result || l.OverallResult === vm.logsFilter.result;
                
                return matchQuery && matchResult;
            });
        },

        paginatedLogs() {
            var start = (this.logsPage - 1) * this.logsPerPage;
            var end = start + this.logsPerPage;
            return this.filteredLogs.slice(start, end);
        },

        logsTotalPages() {
            var total = Math.ceil(this.filteredLogs.length / this.logsPerPage);
            return total > 0 ? total : 1;
        }
    },

    methods: {
        // ── LOAD DATA ──
        loadSchedules() {
            var vm = this;
            var now = new Date();
            var start = new Date(now.getFullYear() - 1, now.getMonth(), now.getDate());
            var end = new Date(now.getFullYear(), now.getMonth() + 3, now.getDate());
            
            var startStr = start.toISOString().substring(0, 10);
            var endStr = end.toISOString().substring(0, 10);

            fetch('/api/checklists/schedules?fromDate=' + startStr + '&toDate=' + endStr)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.schedules = res.data;
                        vm.calculateKPIs();
                        vm.checkUrlParams();
                    } else {
                        vm.toast('Lỗi', res.message, 'danger');
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi mạng', 'Không thể tải lịch trình checklist.', 'danger');
                });
        },

        loadLogs() {
            var vm = this;
            fetch('/api/checklists/logs')
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.logs = res.data;
                        vm.calculateKPIs();
                    } else {
                        vm.toast('Lỗi', res.message, 'danger');
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi mạng', 'Không thể tải nhật ký.', 'danger');
                });
        },

        // ── KPIS CALCULATION ──
        calculateKPIs() {
            var vm = this;
            var todayStr = new Date().toISOString().substring(0, 10);

            // 1. Pending & Overdue
            var pending = 0;
            var overdue = 0;
            vm.schedules.forEach(function (s) {
                if (s.Status === 'pending') {
                    pending++;
                    if (s.DueDate < todayStr) {
                        overdue++;
                    }
                }
            });

            // 2. Completed (in current month)
            var completed = 0;
            var currentMonth = new Date().toISOString().substring(0, 7); // yyyy-MM
            vm.logs.forEach(function (l) {
                if (l.CheckedAt.substring(0, 7) === currentMonth) {
                    completed++;
                }
            });

            // 3. Pass Rate
            var totalLogs = vm.logs.length;
            var passLogs = vm.logs.filter(function (l) { return l.OverallResult === 'pass'; }).length;
            var passRate = totalLogs > 0 ? Math.round((passLogs / totalLogs) * 100) : 100;

            vm.kpi = {
                pending: pending,
                overdue: overdue,
                completed: completed,
                passRate: passRate
            };
        },

        // ── PAGINATION CONTROLS ──
        setSchedulesPage(p) {
            if (p >= 1 && p <= this.schedulesTotalPages) {
                this.schedulesPage = p;
            }
        },

        setLogsPage(p) {
            if (p >= 1 && p <= this.logsTotalPages) {
                this.logsPage = p;
            }
        },

        // ── KPI CARD FILTERING ──
        filterByKpi(type) {
            var vm = this;
            if (type === 'pending') {
                vm.activeTab = 'schedules';
                vm.schedulesFilter.status = 'pending';
                vm.schedulesFilter.onlyOverdue = false;
                vm.schedulesFilter.query = '';
                vm.schedulesFilter.cycleType = '';
                vm.schedulesFilter.fromDate = '';
                vm.schedulesFilter.toDate = '';
            } else if (type === 'overdue') {
                vm.activeTab = 'schedules';
                vm.schedulesFilter.status = 'pending';
                vm.schedulesFilter.onlyOverdue = true;
                vm.schedulesFilter.query = '';
                vm.schedulesFilter.cycleType = '';
                vm.schedulesFilter.fromDate = '';
                vm.schedulesFilter.toDate = '';
            } else if (type === 'completed') {
                vm.activeTab = 'logs';
                vm.logsFilter.result = '';
                vm.logsFilter.query = '';
            }
        },

        resetFilters() {
            this.schedulesFilter = {
                query: '',
                status: 'pending',
                cycleType: '',
                onlyOverdue: false,
                fromDate: '',
                toDate: ''
            };
        },

        // ── URL QUERY ROUTING PARSING ──
        checkUrlParams() {
            var vm = this;
            var params = new URLSearchParams(window.location.search);
            
            var tab = params.get('tab');
            if (tab) {
                vm.activeTab = tab;
            }

            var status = params.get('status');
            if (status !== null) {
                vm.schedulesFilter.status = status;
            }

            var fromDate = params.get('fromDate');
            if (fromDate) {
                vm.schedulesFilter.fromDate = fromDate;
            }
            var toDate = params.get('toDate');
            if (toDate) {
                vm.schedulesFilter.toDate = toDate;
            }

            var inventoryIdStr = params.get('inventoryId');
            if (inventoryIdStr) {
                var inventoryId = parseInt(inventoryIdStr);
                if (!isNaN(inventoryId)) {
                    var sch = vm.schedules.find(function (s) {
                        return s.InventoryId === inventoryId && s.Status === 'pending';
                    });
                    
                    if (sch) {
                        vm.openPerformModal(sch);
                        var newUrl = window.location.protocol + "//" + window.location.host + window.location.pathname;
                        window.history.replaceState({ path: newUrl }, '', newUrl);
                    } else {
                        var anySch = vm.schedules.find(function (s) {
                            return s.InventoryId === inventoryId;
                        });
                        if (anySch && anySch.AssetCode) {
                            vm.schedulesFilter.query = anySch.AssetCode;
                            vm.schedulesFilter.status = '';
                            vm.toast('Kiểm tra hoàn thành', 'Không có lịch kiểm tra chờ xử lý cho thiết bị này. Lần kiểm tra này có thể đã được hoàn thành trước đó.', 'warning');
                        } else {
                            vm.toast('Không tìm thấy lịch trình', 'Thiết bị được yêu cầu hiện tại không có lịch trình checklist nào trong hệ thống.', 'danger');
                        }
                        var newUrl = window.location.protocol + "//" + window.location.host + window.location.pathname;
                        window.history.replaceState({ path: newUrl }, '', newUrl);
                    }
                }
            }
        },

        // ── SIMULATED QR CODE SCAN ──
        openQrScanner() {
            this.showQrScannerArea = true;
            this.scannedQrInput = '';
        },

        autoFillQrCode() {
            this.scannedQrInput = this.activeSchedule.QrCode || ('QR-' + this.activeSchedule.AssetCode);
        },

        verifyQrCode() {
            var vm = this;
            var expected = (vm.activeSchedule.QrCode || ('QR-' + vm.activeSchedule.AssetCode)).trim().toLowerCase();
            var input = (vm.scannedQrInput || '').trim().toLowerCase();
            
            if (!input) {
                vm.toast('Cảnh báo', 'Vui lòng nhập hoặc quét mã QR thiết bị.', 'warning');
                return;
            }

            if (input === expected) {
                vm.performForm.qrScanned = true;
                vm.showQrScannerArea = false;
                vm.toast('Thành công', 'Xác thực QR Code trùng khớp thiết bị: ' + vm.activeSchedule.ItemName, 'success');
            } else {
                vm.performForm.qrScanned = false;
                vm.toast('Lỗi xác thực', 'Mã QR không khớp với thiết bị ' + vm.activeSchedule.ItemName + '. Vui lòng quét lại.', 'danger');
            }
        },

        // ── ACTIONS ──
        generateSchedules() {
            var vm = this;
            if (!vm.generateForm.fromDate || !vm.generateForm.toDate) {
                vm.toast('Cảnh báo', 'Vui lòng chọn đầy đủ thời gian.', 'warning');
                return;
            }
            if (vm.generateForm.fromDate > vm.generateForm.toDate) {
                vm.toast('Cảnh báo', 'Ngày bắt đầu không được lớn hơn ngày kết thúc.', 'warning');
                return;
            }

            vm.isGenerating = true;
            fetch('/api/checklists/generate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    FromDate: vm.generateForm.fromDate,
                    ToDate: vm.generateForm.toDate
                })
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isGenerating = false;
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    vm.showGenerateModal = false;
                    vm.loadSchedules();
                } else {
                    vm.toast('Lỗi', res.message, 'danger');
                }
            })
            .catch(function (err) {
                vm.isGenerating = false;
                console.error(err);
                vm.toast('Lỗi', 'Không thể kết nối máy chủ.', 'danger');
            });
        },

        openPerformModal(schedule) {
            var vm = this;
            vm.activeSchedule = schedule;
            vm.performForm = {
                qrScanned: false,
                note: ''
            };
            vm.checklistItems = [];
            vm.showPerformModal = true;
            vm.performLoading = true;

            fetch('/api/checklists/device-checklist?inventoryId=' + schedule.InventoryId + '&cycleType=' + schedule.CycleType)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    vm.performLoading = false;
                    if (res.success) {
                        vm.checklistItems = res.data.map(function (item) {
                            return {
                                DefinitionId: item.Id,
                                CheckName: item.CheckName,
                                Description: item.Description,
                                IsRequired: item.IsRequired,
                                isPassed: null, // Bắt buộc chọn Đạt/Lỗi
                                note: ''
                            };
                        });
                    } else {
                        vm.toast('Lỗi', res.message, 'danger');
                    }
                })
                .catch(function (err) {
                    vm.performLoading = false;
                    console.error(err);
                    vm.toast('Lỗi', 'Không thể tải danh sách câu hỏi checklist.', 'danger');
                });
        },

        setItemPassed(index, val) {
            this.checklistItems[index].isPassed = val;
        },

        bulkSetAllPassed() {
            this.checklistItems.forEach(function (item) {
                item.isPassed = true;
            });
            this.toast('Thành công', 'Đã đặt kết quả ĐẠT cho toàn bộ ' + this.checklistItems.length + ' hạng mục.', 'success');
        },

        submitChecklist() {
            var vm = this;

            // 1. Kiểm tra ràng buộc
            var missing = vm.checklistItems.filter(function (item) {
                return item.IsRequired && item.isPassed === null;
            });

            if (missing.length > 0) {
                vm.toast('Cảnh báo', 'Vui lòng đánh giá đầy đủ các hạng mục bắt buộc (*) trước khi lưu.', 'warning');
                return;
            }

            // 2. Tính toán kết quả chung
            var overall = 'pass';
            var hasPassed = vm.checklistItems.some(function (i) { return i.isPassed === true; });
            var hasFailed = vm.checklistItems.some(function (i) { return i.isPassed === false; });

            if (hasFailed && hasPassed) {
                overall = 'partial';
            } else if (hasFailed && !hasPassed) {
                overall = 'fail';
            }

            vm.isSubmitting = true;

            var payload = {
                ScheduleId: vm.activeSchedule.Id,
                InventoryId: vm.activeSchedule.InventoryId,
                CheckedBy: vm.currentUser.Id,
                CycleType: vm.activeSchedule.CycleType,
                OverallResult: overall,
                Note: vm.performForm.note,
                QrScanned: vm.performForm.qrScanned,
                QrLocation: vm.activeSchedule.DepartmentName,
                ImageUrls: '',
                Items: vm.checklistItems.filter(function (i) { return i.isPassed !== null; }).map(function (i) {
                    return {
                        DefinitionId: i.DefinitionId,
                        IsPassed: i.isPassed,
                        Note: i.note
                    };
                })
            };

            fetch('/api/checklists/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isSubmitting = false;
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    vm.showPerformModal = false;
                    vm.loadSchedules();
                    vm.loadLogs();
                } else {
                    vm.toast('Lỗi', res.message, 'danger');
                }
            })
            .catch(function (err) {
                vm.isSubmitting = false;
                console.error(err);
                vm.toast('Lỗi', 'Lỗi kết nối máy chủ.', 'danger');
            });
        },

        openDetailsModal(logId) {
            var vm = this;
            vm.logDetails = null;
            vm.showDetailsModal = true;

            fetch('/api/checklists/log-details?logId=' + logId)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.logDetails = res.data;
                    } else {
                        vm.toast('Lỗi', res.message, 'danger');
                        vm.showDetailsModal = false;
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi', 'Lỗi tải chi tiết nhật ký.', 'danger');
                    vm.showDetailsModal = false;
                });
        },

        // ── LABELS & HELPERS ──
        cycleLabel(type) {
            switch (type) {
                case 'daily': return 'Hàng ngày';
                case 'weekly': return 'Hàng tuần';
                case 'monthly': return 'Hàng tháng';
                case 'quarterly': return 'Hàng quý';
                case 'yearly': return 'Hàng năm';
                default: return type || 'Không định kỳ';
            }
        },

        statusLabel(status) {
            switch (status) {
                case 'pending': return 'Chờ xử lý';
                case 'completed': return 'Đã xong';
                case 'done': return 'Đã xong';
                case 'skipped': return 'Bỏ qua';
                default: return status;
            }
        },

        resultLabel(res) {
            switch (res) {
                case 'pass': return 'Đạt chuẩn';
                case 'fail': return 'Không đạt';
                case 'partial': return 'Đạt một phần';
                default: return res;
            }
        },

        isOverdue(dueDateStr) {
            var todayStr = new Date().toISOString().substring(0, 10);
            return dueDateStr < todayStr;
        },

        toast(title, msg, type) {
            if (window.MedEquip && typeof window.MedEquip.toast === 'function') {
                window.MedEquip.toast(title, msg, type);
            } else {
                alert(title + ': ' + msg);
            }
        }
    },

    mounted() {
        var vm = this;
        var userStr = localStorage.getItem('current_user');
        if (userStr) {
            try {
                var cu = JSON.parse(userStr);
                if (cu && cu.Id) vm.currentUser = cu;
            } catch (e) { /* ignore */ }
        }

        var now = new Date();
        var y = now.getFullYear();
        var m = now.getMonth();
        var fromDate = new Date(y, m, 1);
        var toDate = new Date(y, m + 1, 0); // ngày cuối tháng
        vm.generateForm = {
            fromDate: fromDate.toISOString().substring(0, 10),
            toDate: toDate.toISOString().substring(0, 10)
        };

        vm.loadSchedules();
        vm.loadLogs();
    }
});
