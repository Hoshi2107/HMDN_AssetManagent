/* ═══════════════════════════════════════════════════════════
   alerts.js — Vue 2 Instance cho Trung tâm Cảnh báo & Thông báo
   Tuân thủ: Vue 2.7.16 CDN, jQuery AJAX, delimiters ['${', '}']
   ═══════════════════════════════════════════════════════════ */

var app = new Vue({
    el: '#app',

    delimiters: ['${', '}'],

    data: {
        // Trạng thái màn hình: 'alerts' (Trung tâm cảnh báo), 'config' (Cấu hình quy tắc)
        activeView: 'alerts',

        // Danh sách cảnh báo thực tế
        alerts: [],

        // Số lượng cảnh báo theo từng tab
        counts: {
            All: 0,
            Danger: 0,
            Warning: 0,
            Info: 0
        },

        // Tab lọc hiện tại: 'all', 'danger', 'warning', 'info'
        activeTab: 'all',

        // Tìm kiếm cảnh báo
        searchQuery: '',

        // Danh sách quy tắc cấu hình
        rules: [],

        // Lưu tạm cấu hình đang chỉnh sửa
        configForm: {
            multiFault: { id: 0, code: 'MULTI_FAULT_3X', isActive: true, count: 3, period: 30, severity: 'danger' },
            warranty: { id: 0, code: 'WARRANTY_EXPIRY_30D', isActive: true, days: 30, severity: 'warning', costReport: true },
            maintenance: { id: 0, code: 'CHECKLIST_OVERDUE', isActive: true, days: 0, cycle: 'monthly', tolerance: 5 },
            consumables: { printer: 10, battery: 5 },
            methods: {
                email: true,
                sms: false,
                webPush: true,
                socket: true
            }
        },

        // Trạng thái tải dữ liệu
        loading: false,
        saving: false,

        // Trạng thái xử lý của từng alert (để disable button tránh click đúp)
        processingAlerts: {},

        // Số lượng nhập kho nhanh cho từng consumable alert
        restockQuantities: {}
    },

    computed: {
        filteredAlerts: function () {
            var vm = this;
            var list = vm.alerts.slice();

            if (vm.searchQuery) {
                var q = vm.searchQuery.toLowerCase().trim();
                list = list.filter(function (a) {
                    return (
                        (a.ItemName || '').toLowerCase().includes(q) ||
                        (a.AssetCode || '').toLowerCase().includes(q) ||
                        (a.Title || '').toLowerCase().includes(q) ||
                        (a.Body || '').toLowerCase().includes(q) ||
                        (a.LocationName || '').toLowerCase().includes(q) ||
                        (a.DepartmentName || '').toLowerCase().includes(q)
                    );
                });
            }

            return list;
        }
    },

    methods: {
        // Chuyển đổi màn hình view
        setView: function (view) {
            this.activeView = view;
            if (view === 'config') {
                this.loadRules();
            } else {
                this.loadAlerts();
            }
        },

        // Thiết lập tab hiển thị và load dữ liệu
        setTab: function (tab) {
            this.activeTab = tab;
            this.loadAlerts();
        },

        // Trả về icon phù hợp với severity
        severityIcon: function (severity) {
            if (severity === 'danger') return 'fa-solid fa-triangle-exclamation';
            if (severity === 'warning') return 'fa-solid fa-circle-exclamation';
            return 'fa-solid fa-circle-info';
        },

        // Trả về nhãn mức độ ưu tiên
        severityLabel: function (severity) {
            if (severity === 'danger') return 'Khẩn cấp';
            if (severity === 'warning') return 'Cảnh báo';
            return 'Thông tin';
        },

        // ── API Calls ──

        // 1. Tải danh sách cảnh báo
        loadAlerts: function () {
            var vm = this;
            vm.loading = true;

            $.ajax({
                url: '/api/alerts/list?tab=' + vm.activeTab,
                type: 'GET',
                success: function (res) {
                    vm.alerts = res.alerts;
                    vm.counts.All = res.counts.All;
                    vm.counts.Danger = res.counts.Danger;
                    vm.counts.Warning = res.counts.Warning;
                    vm.counts.Info = res.counts.Info;
                    vm.loading = false;
                },
                error: function (xhr) {
                    console.error('Lỗi tải danh sách cảnh báo:', xhr.responseText);
                    vm.showToast('Lỗi', 'Không thể tải danh sách cảnh báo.', 'danger');
                    vm.loading = false;
                }
            });
        },

        // 2. Tải danh sách quy tắc chẩn đoán
        loadRules: function () {
            var vm = this;
            vm.loading = true;

            $.ajax({
                url: '/api/alerts/rules',
                type: 'GET',
                success: function (res) {
                    vm.rules = res;
                    
                    // Map dữ liệu từ database vào form cấu hình
                    var ruleFault = res.find(function (r) { return r.Code === 'MULTI_FAULT_3X'; });
                    if (ruleFault) {
                        vm.configForm.multiFault.id = ruleFault.Id;
                        vm.configForm.multiFault.isActive = ruleFault.IsActive;
                        vm.configForm.multiFault.count = ruleFault.ThresholdCount || 3;
                        vm.configForm.multiFault.period = ruleFault.ThresholdPeriodDays || 30;
                    }

                    var ruleWarranty = res.find(function (r) { return r.Code === 'WARRANTY_EXPIRY_30D'; });
                    if (ruleWarranty) {
                        vm.configForm.warranty.id = ruleWarranty.Id;
                        vm.configForm.warranty.isActive = ruleWarranty.IsActive;
                        vm.configForm.warranty.days = ruleWarranty.ThresholdDays || 30;
                    }

                    var ruleMaint = res.find(function (r) { return r.Code === 'CHECKLIST_OVERDUE'; });
                    if (ruleMaint) {
                        vm.configForm.maintenance.id = ruleMaint.Id;
                        vm.configForm.maintenance.isActive = ruleMaint.IsActive;
                    }

                    vm.loading = false;
                },
                error: function (xhr) {
                    console.error('Lỗi tải cấu hình quy tắc:', xhr.responseText);
                    vm.showToast('Lỗi', 'Không thể tải cấu hình quy tắc cảnh báo.', 'danger');
                    vm.loading = false;
                }
            });
        },

        // 3. Lưu cấu hình quy tắc
        saveConfig: function () {
            var vm = this;
            vm.saving = true;

            // Xây dựng danh sách các rule để gửi lên API
            var updatedRules = [
                {
                    Id: vm.configForm.multiFault.id,
                    IsActive: vm.configForm.multiFault.isActive,
                    ThresholdCount: parseInt(vm.configForm.multiFault.count),
                    ThresholdPeriodDays: parseInt(vm.configForm.multiFault.period),
                    Description: 'Cảnh báo tự động đề xuất thanh lý khi thiết bị hỏng vượt ngưỡng tần suất.'
                },
                {
                    Id: vm.configForm.warranty.id,
                    IsActive: vm.configForm.warranty.isActive,
                    ThresholdDays: parseInt(vm.configForm.warranty.days),
                    Description: 'Cảnh báo tự động gia hạn bảo dưỡng/bảo hành trước thời hạn.'
                },
                {
                    Id: vm.configForm.maintenance.id,
                    IsActive: vm.configForm.maintenance.isActive,
                    ThresholdDays: 0,
                    Description: 'Cảnh báo khi kế hoạch checklist của thiết bị quá hạn.'
                }
            ];

            $.ajax({
                url: '/api/alerts/rules',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(updatedRules),
                success: function (res) {
                    vm.saving = false;
                    vm.showToast('Thành công', 'Lưu cấu hình quy tắc cảnh báo thành công.', 'success');
                    vm.setView('alerts'); // Quay lại trang danh sách
                },
                error: function (xhr) {
                    console.error('Lỗi lưu cấu hình quy tắc:', xhr.responseText);
                    vm.showToast('Lỗi', 'Không thể lưu cấu hình quy tắc cảnh báo.', 'danger');
                    vm.saving = false;
                }
            });
        },

        // 4. Xử lý cảnh báo (Đánh dấu đã giải quyết)
        resolveAlert: function (id) {
            var vm = this;
            if (vm.processingAlerts[id]) return;
            Vue.set(vm.processingAlerts, id, true);

            $.ajax({
                url: '/api/alerts/resolve/' + id,
                type: 'POST',
                success: function (res) {
                    vm.showToast('Thành công', 'Đã xác nhận xử lý cảnh báo.', 'success');
                    Vue.delete(vm.processingAlerts, id);
                    vm.loadAlerts(); // Tải lại danh sách
                },
                error: function (xhr) {
                    console.error('Lỗi xử lý cảnh báo:', xhr.responseText);
                    vm.showToast('Lỗi', xhr.responseJSON?.message || 'Không thể xử lý cảnh báo.', 'danger');
                    Vue.delete(vm.processingAlerts, id);
                }
            });
        },

        // 4.1. Đề xuất thanh lý thiết bị
        liquidateAsset: function (alert) {
            var vm = this;
            var id = alert.Id;
            if (vm.processingAlerts[id]) return;

            if (!confirm('Bạn có chắc chắn muốn đề xuất thanh lý thiết bị ' + alert.ItemName + ' (' + alert.AssetCode + ') không? Trạng thái thiết bị sẽ chuyển sang "Thanh lý".')) {
                return;
            }

            Vue.set(vm.processingAlerts, id, true);

            $.ajax({
                url: '/api/alerts/liquidate/' + id,
                type: 'POST',
                success: function (res) {
                    vm.showToast('Thành công', res.message || 'Đã đề xuất thanh lý thiết bị thành công.', 'success');
                    Vue.delete(vm.processingAlerts, id);
                    vm.loadAlerts();
                },
                error: function (xhr) {
                    console.error('Lỗi đề xuất thanh lý:', xhr.responseText);
                    var errMsg = xhr.responseJSON?.Message || xhr.responseJSON?.message || 'Không thể thanh lý thiết bị.';
                    vm.showToast('Không thể thực hiện', errMsg, 'danger');
                    Vue.delete(vm.processingAlerts, id);
                }
            });
        },

        // 4.2. Gia hạn bảo hành nhanh (12 tháng)
        extendWarranty: function (alert) {
            var vm = this;
            var id = alert.Id;
            if (vm.processingAlerts[id]) return;

            Vue.set(vm.processingAlerts, id, true);

            $.ajax({
                url: '/api/alerts/extend-warranty/' + id,
                type: 'POST',
                success: function (res) {
                    vm.showToast('Thành công', res.message || 'Đã gia hạn bảo hành thêm 12 tháng.', 'success');
                    Vue.delete(vm.processingAlerts, id);
                    vm.loadAlerts();
                },
                error: function (xhr) {
                    console.error('Lỗi gia hạn bảo hành:', xhr.responseText);
                    vm.showToast('Lỗi', xhr.responseJSON?.message || 'Không thể gia hạn bảo hành.', 'danger');
                    Vue.delete(vm.processingAlerts, id);
                }
            });
        },

        // 4.3. Hoàn thành hoặc bỏ qua checklist
        completeChecklist: function (alert, status) {
            var vm = this;
            var id = alert.Id;
            if (vm.processingAlerts[id]) return;

            var actionName = status === 'done' ? 'hoàn thành kiểm tra' : 'bỏ qua lần này';
            if (!confirm('Xác nhận ' + actionName + ' cho thiết bị ' + alert.ItemName + '?')) {
                return;
            }

            Vue.set(vm.processingAlerts, id, true);

            $.ajax({
                url: '/api/alerts/complete-checklist/' + id + '?status=' + status,
                type: 'POST',
                success: function (res) {
                    vm.showToast('Thành công', res.message || 'Cập nhật trạng thái checklist thành công.', 'success');
                    Vue.delete(vm.processingAlerts, id);
                    vm.loadAlerts();
                },
                error: function (xhr) {
                    console.error('Lỗi cập nhật checklist:', xhr.responseText);
                    vm.showToast('Lỗi', xhr.responseJSON?.message || 'Không thể cập nhật checklist.', 'danger');
                    Vue.delete(vm.processingAlerts, id);
                }
            });
        },

        // 4.4. Nhập kho nhanh vật tư tiêu hao
        restockConsumable: function (alert, quantity) {
            var vm = this;
            var id = alert.Id;
            var qty = parseInt(quantity);
            if (isNaN(qty) || qty <= 0) {
                vm.showToast('Lỗi nhập liệu', 'Vui lòng nhập số lượng lớn hơn 0.', 'warning');
                return;
            }

            if (vm.processingAlerts[id]) return;
            Vue.set(vm.processingAlerts, id, true);

            $.ajax({
                url: '/api/alerts/restock/' + id + '?quantity=' + qty,
                type: 'POST',
                success: function (res) {
                    vm.showToast('Thành công', res.message || 'Đã nhập kho nhanh thành công.', 'success');
                    Vue.delete(vm.processingAlerts, id);
                    // Reset input
                    Vue.delete(vm.restockQuantities, id);
                    vm.loadAlerts();
                },
                error: function (xhr) {
                    console.error('Lỗi nhập kho nhanh:', xhr.responseText);
                    vm.showToast('Lỗi', xhr.responseJSON?.message || 'Không thể nhập kho.', 'danger');
                    Vue.delete(vm.processingAlerts, id);
                }
            });
        },

        // 4.5. Xác nhận tất cả cảnh báo đang hiển thị
        resolveAllVisible: function () {
            var vm = this;
            if (!vm.filteredAlerts.length) return;

            if (!confirm('Bạn có chắc chắn muốn xác nhận xử lý tất cả ' + vm.filteredAlerts.length + ' cảnh báo đang hiển thị không?')) {
                return;
            }

            vm.loading = true;
            var ids = vm.filteredAlerts.map(function (a) { return a.Id; });

            // Set processing for all visible alerts
            ids.forEach(function (id) {
                Vue.set(vm.processingAlerts, id, true);
            });

            $.ajax({
                url: '/api/alerts/resolve-multiple',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(ids),
                success: function (res) {
                    vm.showToast('Thành công', 'Đã xác nhận xử lý tất cả cảnh báo đang hiển thị.', 'success');
                    ids.forEach(function (id) {
                        Vue.delete(vm.processingAlerts, id);
                    });
                    vm.loadAlerts(); // Tải lại danh sách
                },
                error: function (xhr) {
                    console.error('Lỗi xác nhận tất cả cảnh báo:', xhr.responseText);
                    vm.showToast('Lỗi', 'Không thể xác nhận xử lý tất cả cảnh báo.', 'danger');
                    ids.forEach(function (id) {
                        Vue.delete(vm.processingAlerts, id);
                    });
                    vm.loading = false;
                }
            });
        },

        // 5. Chạy quét cảnh báo khẩn cấp realtime (Toast Popup)
        checkRealtimeAlerts: function () {
            var vm = this;

            $.ajax({
                url: '/api/alerts/check-new',
                type: 'GET',
                success: function (res) {
                    if (res && res.length > 0) {
                        // Duyệt qua danh sách cảnh báo mới và bắn thông báo popup
                        res.forEach(function (alert) {
                            var toastType = alert.Severity === 'danger' ? 'danger' :
                                            alert.Severity === 'warning' ? 'warning' : 'success';
                            
                            vm.showToast(
                                alert.Title,
                                alert.Body || (alert.ItemName + ' - Thiết bị gặp sự cố.'),
                                toastType
                            );
                        });

                        // Nếu đang ở màn hình cảnh báo, tải lại danh sách để đồng bộ UI
                        if (vm.activeView === 'alerts') {
                            vm.loadAlerts();
                        }
                    }
                },
                error: function (xhr) {
                    // Fail silently for realtime polling
                    console.warn('Lỗi polling cảnh báo realtime:', xhr.responseText);
                }
            });
        },

        // Hành động điều hướng nhanh từ cảnh báo
        handleAction: function (alert, actionType) {
            var vm = this;
            if (actionType === 'repair') {
                // Đi đến trang sửa chữa (Maintenance)
                window.location.href = '/Maintenance/Index?inventoryId=' + alert.InventoryId + '&triggerCreate=1';
            } else if (actionType === 'warranty') {
                // Đi đến trang chi tiết gia hạn/khấu hao
                window.location.href = '/VongDoiKhauHao/VongDoiKhauHao?searchCode=' + alert.AssetCode;
            } else if (actionType === 'checklist') {
                // Đi đến trang checklist
                window.location.href = '/Checklists/Index?inventoryId=' + alert.InventoryId;
            } else if (actionType === 'order') {
                // Hiển thị toast thông báo đặt hàng thành công
                vm.showToast('Đặt hàng thành công', 'Đã gửi yêu cầu đặt hàng vật tư thay thế tới bộ phận kho.', 'success');
                vm.resolveAlert(alert.Id);
            } else if (actionType === 'ignore') {
                // Bỏ qua cảnh báo bằng cách giải quyết nó
                vm.resolveAlert(alert.Id);
            } else if (actionType === 'view') {
                // Xem chi tiết tài sản
                window.location.href = '/Inventory/Index?searchCode=' + alert.AssetCode + '&inventoryId=' + alert.InventoryId;
            }
        },

        // Hiển thị Toast thông báo sử dụng thư viện chung MedEquip.toast
        showToast: function (title, msg, type) {
            if (window.MedEquip && window.MedEquip.toast) {
                window.MedEquip.toast(title, msg, type);
            } else {
                alert(title + ': ' + msg);
            }
        }
    },

    mounted: function () {
        var vm = this;
        vm.loadAlerts();

        // Thiết lập polling chẩn đoán cảnh báo realtime định kỳ mỗi 10 giây
        setInterval(function () {
            vm.checkRealtimeAlerts();
        }, 10000);
    }
});
