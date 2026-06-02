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

        // Tab cấu hình quy tắc hiện tại: 'rules', 'consumables', 'routing', 'logs'
        configTab: 'rules',

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
            checklistDue: { id: 0, code: 'CHECKLIST_DUE_3D', isActive: true, days: 3, overrides: [] },
            maintenance: { id: 0, code: 'CHECKLIST_OVERDUE', isActive: true, tolerance: 5, overrides: [] },
            multiFault: { id: 0, code: 'MULTI_FAULT_3X', isActive: true, count: 3, period: 30, overrides: [] },
            depreciation: { id: 0, code: 'DEPRECIATION_END_30D', isActive: true, days: 30, overrides: [] },
            expiry: { id: 0, code: 'EXPIRY_SOON_60D', isActive: true, days: 60, overrides: [] },
            warranty: { id: 0, code: 'WARRANTY_EXPIRY_30D', isActive: true, days: 30, costReport: true, overrides: [] },
            consumables: { id: 0, code: 'CONSUMABLES_LOW', isActive: true, printer: 10, battery: 5, office: 15, cdha: 20, hscc: 8, phongmo: 12, xetnghiem: 25 },
            methods: {
                email: true,
                sms: false,
                webPush: true,
                socket: true
            }
        },

        // Nhóm thiết bị khả dụng để cấu hình đặc thù
        deviceGroups: [
            { code: 'CRITICAL', name: 'Thiết bị hồi sức/phẫu thuật' },
            { code: 'IMAGING', name: 'Thiết bị chẩn đoán hình ảnh' },
            { code: 'LAB', name: 'Thiết bị xét nghiệm' },
            { code: 'OFFICE', name: 'Thiết bị văn phòng' }
        ],

        // Phân phối nhận cảnh báo theo vai trò (Tech, Stock keeper, Head dept, Director)
        roleRouting: {
            'MULTI_FAULT_3X': ['tech', 'head'],
            'WARRANTY_EXPIRY_30D': ['tech'],
            'CHECKLIST_OVERDUE': ['tech', 'head'],
            'CHECKLIST_DUE_3D': ['tech'],
            'CONSUMABLES_LOW': ['stock'],
            'EXPIRY_SOON_60D': ['tech', 'director'],
            'DEPRECIATION_END_30D': ['director']
        },

        // Lịch sử logs thay đổi cấu hình
        configLogs: [],

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

            // Load role routing và history logs từ localStorage
            var savedRouting = localStorage.getItem('role_routing_config');
            if (savedRouting) {
                try { vm.roleRouting = JSON.parse(savedRouting); } catch (e) {}
            }
            var savedLogs = localStorage.getItem('config_change_logs');
            if (savedLogs) {
                try { vm.configLogs = JSON.parse(savedLogs); } catch (e) {}
            } else {
                // Seed some realistic change logs
                vm.configLogs = [
                    { time: '01/06/2026 08:30', user: 'Nguyễn Văn Quản Trị', action: 'Thiết lập trễ checklist tối đa (5 ngày)' },
                    { time: '30/05/2026 14:15', user: 'Nguyễn Văn Quản Trị', action: 'Bật quy tắc cảnh báo vật tư tiêu hao' }
                ];
                localStorage.setItem('config_change_logs', JSON.stringify(vm.configLogs));
            }

            $.ajax({
                url: '/api/alerts/rules',
                type: 'GET',
                success: function (res) {
                    vm.rules = res;
                    
                    // Helper to parse Description JSON
                    var parseDesc = function (desc) {
                        if (!desc) return { text: '', overrides: [] };
                        var trimmed = desc.trim();
                        if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
                            try { return JSON.parse(trimmed); } catch (e) {}
                        }
                        return { text: desc, overrides: [] };
                    };

                    // Map dữ liệu từ database vào form cấu hình
                    var ruleCheckDue = res.find(function (r) { return r.Code === 'CHECKLIST_DUE_3D'; });
                    if (ruleCheckDue) {
                        vm.configForm.checklistDue.id = ruleCheckDue.Id;
                        vm.configForm.checklistDue.isActive = ruleCheckDue.IsActive;
                        vm.configForm.checklistDue.days = ruleCheckDue.ThresholdDays !== null ? ruleCheckDue.ThresholdDays : 3;
                        var d = parseDesc(ruleCheckDue.Description);
                        vm.configForm.checklistDue.overrides = d.overrides || [];
                    }

                    var ruleMaint = res.find(function (r) { return r.Code === 'CHECKLIST_OVERDUE'; });
                    if (ruleMaint) {
                        vm.configForm.maintenance.id = ruleMaint.Id;
                        vm.configForm.maintenance.isActive = ruleMaint.IsActive;
                        vm.configForm.maintenance.tolerance = ruleMaint.ThresholdDays !== null ? ruleMaint.ThresholdDays : 5;
                        var d = parseDesc(ruleMaint.Description);
                        vm.configForm.maintenance.overrides = d.overrides || [];
                    }

                    var ruleFault = res.find(function (r) { return r.Code === 'MULTI_FAULT_3X'; });
                    if (ruleFault) {
                        vm.configForm.multiFault.id = ruleFault.Id;
                        vm.configForm.multiFault.isActive = ruleFault.IsActive;
                        vm.configForm.multiFault.count = ruleFault.ThresholdCount !== null ? ruleFault.ThresholdCount : 3;
                        vm.configForm.multiFault.period = ruleFault.ThresholdPeriodDays !== null ? ruleFault.ThresholdPeriodDays : 30;
                        var d = parseDesc(ruleFault.Description);
                        vm.configForm.multiFault.overrides = d.overrides || [];
                    }

                    var ruleDeprec = res.find(function (r) { return r.Code === 'DEPRECIATION_END_30D'; });
                    if (ruleDeprec) {
                        vm.configForm.depreciation.id = ruleDeprec.Id;
                        vm.configForm.depreciation.isActive = ruleDeprec.IsActive;
                        vm.configForm.depreciation.days = ruleDeprec.ThresholdDays !== null ? ruleDeprec.ThresholdDays : 30;
                        var d = parseDesc(ruleDeprec.Description);
                        vm.configForm.depreciation.overrides = d.overrides || [];
                    }

                    var ruleExpiry = res.find(function (r) { return r.Code === 'EXPIRY_SOON_60D'; });
                    if (ruleExpiry) {
                        vm.configForm.expiry.id = ruleExpiry.Id;
                        vm.configForm.expiry.isActive = ruleExpiry.IsActive;
                        vm.configForm.expiry.days = ruleExpiry.ThresholdDays !== null ? ruleExpiry.ThresholdDays : 60;
                        var d = parseDesc(ruleExpiry.Description);
                        vm.configForm.expiry.overrides = d.overrides || [];
                    }

                    var ruleWarranty = res.find(function (r) { return r.Code === 'WARRANTY_EXPIRY_30D'; });
                    if (ruleWarranty) {
                        vm.configForm.warranty.id = ruleWarranty.Id;
                        vm.configForm.warranty.isActive = ruleWarranty.IsActive;
                        vm.configForm.warranty.days = ruleWarranty.ThresholdDays !== null ? ruleWarranty.ThresholdDays : 30;
                        var d = parseDesc(ruleWarranty.Description);
                        vm.configForm.warranty.overrides = d.overrides || [];
                    }

                    var ruleConsumables = res.find(function (r) { return r.Code === 'CONSUMABLES_LOW'; });
                    if (ruleConsumables) {
                        vm.configForm.consumables.id = ruleConsumables.Id;
                        vm.configForm.consumables.isActive = ruleConsumables.IsActive;
                        vm.configForm.consumables.printer = ruleConsumables.ThresholdCount !== null ? ruleConsumables.ThresholdCount : 10;
                        vm.configForm.consumables.battery = ruleConsumables.ThresholdDays !== null ? ruleConsumables.ThresholdDays : 5;
                        vm.configForm.consumables.office = 15;
                        vm.configForm.consumables.cdha = 20;
                        vm.configForm.consumables.hscc = 8;
                        vm.configForm.consumables.phongmo = 12;
                        vm.configForm.consumables.xetnghiem = 25;

                        if (ruleConsumables.Description) {
                            var d = parseDesc(ruleConsumables.Description);
                            if (d && d.thresholds) {
                                if (d.thresholds.PRINTER !== undefined) vm.configForm.consumables.printer = d.thresholds.PRINTER;
                                if (d.thresholds.UPS !== undefined) vm.configForm.consumables.battery = d.thresholds.UPS;
                                if (d.thresholds.OFFICE !== undefined) vm.configForm.consumables.office = d.thresholds.OFFICE;
                                if (d.thresholds.CDHA !== undefined) vm.configForm.consumables.cdha = d.thresholds.CDHA;
                                if (d.thresholds.HSCC !== undefined) vm.configForm.consumables.hscc = d.thresholds.HSCC;
                                if (d.thresholds.PHONGMO !== undefined) vm.configForm.consumables.phongmo = d.thresholds.PHONGMO;
                                if (d.thresholds.XETNGHIEM !== undefined) vm.configForm.consumables.xetnghiem = d.thresholds.XETNGHIEM;
                            }
                        }
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
                    Id: vm.configForm.checklistDue.id,
                    IsActive: vm.configForm.checklistDue.isActive,
                    ThresholdDays: parseInt(vm.configForm.checklistDue.days),
                    Description: JSON.stringify({
                        text: 'Cảnh báo nhắc lịch bảo trì sắp đến hạn kiểm tra định kỳ.',
                        overrides: vm.configForm.checklistDue.overrides
                    })
                },
                {
                    Id: vm.configForm.maintenance.id,
                    IsActive: vm.configForm.maintenance.isActive,
                    ThresholdDays: parseInt(vm.configForm.maintenance.tolerance),
                    Description: JSON.stringify({
                        text: 'Cảnh báo khi kế hoạch checklist bảo trì của thiết bị quá hạn.',
                        overrides: vm.configForm.maintenance.overrides
                    })
                },
                {
                    Id: vm.configForm.multiFault.id,
                    IsActive: vm.configForm.multiFault.isActive,
                    ThresholdCount: parseInt(vm.configForm.multiFault.count),
                    ThresholdPeriodDays: parseInt(vm.configForm.multiFault.period),
                    Description: JSON.stringify({
                        text: 'Cảnh báo tự động đề xuất thanh lý khi thiết bị hỏng vượt ngưỡng tần suất.',
                        overrides: vm.configForm.multiFault.overrides
                    })
                },
                {
                    Id: vm.configForm.depreciation.id,
                    IsActive: vm.configForm.depreciation.isActive,
                    ThresholdDays: parseInt(vm.configForm.depreciation.days),
                    Description: JSON.stringify({
                        text: 'Cảnh báo khi chu kỳ khấu hao của thiết bị sắp kết thúc.',
                        overrides: vm.configForm.depreciation.overrides
                    })
                },
                {
                    Id: vm.configForm.expiry.id,
                    IsActive: vm.configForm.expiry.isActive,
                    ThresholdDays: parseInt(vm.configForm.expiry.days),
                    Description: JSON.stringify({
                        text: 'Cảnh báo thiết bị y tế gần hết thời hạn sử dụng hữu ích.',
                        overrides: vm.configForm.expiry.overrides
                    })
                },
                {
                    Id: vm.configForm.warranty.id,
                    IsActive: vm.configForm.warranty.isActive,
                    ThresholdDays: parseInt(vm.configForm.warranty.days),
                    Description: JSON.stringify({
                        text: 'Cảnh báo thời gian bảo hành chính hãng sắp hết hạn.',
                        overrides: vm.configForm.warranty.overrides
                    })
                },
                {
                    Id: vm.configForm.consumables.id,
                    IsActive: vm.configForm.consumables.isActive,
                    ThresholdCount: parseInt(vm.configForm.consumables.printer),
                    ThresholdDays: parseInt(vm.configForm.consumables.battery),
                    Description: JSON.stringify({
                        text: 'Cảnh báo khi lượng tồn kho của vật tư tiêu hao xuống dưới mức tối thiểu.',
                        thresholds: {
                            PRINTER: parseInt(vm.configForm.consumables.printer),
                            UPS: parseInt(vm.configForm.consumables.battery),
                            OFFICE: parseInt(vm.configForm.consumables.office),
                            CDHA: parseInt(vm.configForm.consumables.cdha),
                            HSCC: parseInt(vm.configForm.consumables.hscc),
                            PHONGMO: parseInt(vm.configForm.consumables.phongmo),
                            XETNGHIEM: parseInt(vm.configForm.consumables.xetnghiem)
                        }
                    })
                }
            ];

            // Save role routing và generate log mới
            localStorage.setItem('role_routing_config', JSON.stringify(vm.roleRouting));

            // Log update
            var nowStr = new Date().toLocaleString('vi-VN', { hour12: false }).replace(/:[^:]*$/, '');
            var logText = 'Cập nhật tham số quy tắc thành công';
            vm.configLogs.unshift({
                time: nowStr,
                user: 'Nguyễn Văn Quản Trị',
                action: logText
            });
            // Keep top 10 logs
            if (vm.configLogs.length > 10) vm.configLogs = vm.configLogs.slice(0, 10);
            localStorage.setItem('config_change_logs', JSON.stringify(vm.configLogs));

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

        goToPerformChecklist: function (inventoryId) {
            window.location.href = '/Checklists/Index?inventoryId=' + inventoryId;
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
                        // Trên màn hình Trung tâm cảnh báo, nếu tổng số cảnh báo chưa xử lý thay đổi thì tự động tải lại UI
                        if (vm.activeView === 'alerts' && res.length !== vm.alerts.length) {
                            vm.loadAlerts();
                        }
                    }
                },
                error: function (xhr) {
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

        // Click vào card để chuyển hướng đến chi tiết thiết bị (trừ khi click vào nút hoặc input)
        onCardClick: function (alert, event) {
            var target = event.target;
            if (target.closest('.alert-dismiss-btn') || 
                target.closest('.alert-card-actions') || 
                target.closest('.inline-restock-group') || 
                target.closest('button') || 
                target.closest('input') || 
                target.closest('select') || 
                target.closest('a')) {
                return;
            }
            if (alert.RuleCode === 'CHECKLIST_OVERDUE' || alert.RuleCode === 'CHECKLIST_DUE_3D') {
                this.handleAction(alert, 'checklist');
            } else {
                this.handleAction(alert, 'view');
            }
        },

        // Hiển thị Toast thông báo sử dụng thư viện chung MedEquip.toast
        showToast: function (title, msg, type, alertObj) {
            if (window.MedEquip && window.MedEquip.toast) {
                window.MedEquip.toast(title, msg, type, alertObj);
            } else {
                alert(title + ': ' + msg);
            }
        },

        // Thêm cấu hình đắc thù theo nhóm thiết bị
        addOverride: function (ruleKey) {
            var vm = this;
            var group = vm.deviceGroups[0]; // Mặc định nhóm đầu
            var newOverride = {};
            
            if (ruleKey === 'multiFault') {
                newOverride = { groupCode: group.code, groupName: group.name, count: 3, period: 30 };
            } else if (ruleKey === 'maintenance') {
                newOverride = { groupCode: group.code, groupName: group.name, tolerance: 5 };
            } else {
                newOverride = { groupCode: group.code, groupName: group.name, days: 30 };
            }

            vm.configForm[ruleKey].overrides.push(newOverride);
        },

        // Xóa cấu hình đặc thù
        removeOverride: function (ruleKey, index) {
            this.configForm[ruleKey].overrides.splice(index, 1);
        },

        // Cập nhật tên nhóm khi người dùng đổi select
        onOverrideGroupChange: function (ruleKey, index, groupCode) {
            var vm = this;
            var gr = vm.deviceGroups.find(function(g) { return g.code === groupCode; });
            if (gr) {
                vm.configForm[ruleKey].overrides[index].groupName = gr.name;
            }
        },

        // Ép chạy Engine chẩn đoán khẩn cấp và làm mới cảnh báo
        forceScan: function () {
            var vm = this;
            vm.saving = true;
            vm.showToast('Bắt đầu quét', 'Đang khởi chạy Engine chẩn đoán khẩn cấp...', 'info');

            $.ajax({
                url: '/api/alerts/diagnostics',
                type: 'POST',
                success: function (res) {
                    vm.saving = false;
                    vm.showToast('Thành công', 'Đã quét và cập nhật danh sách cảnh báo mới nhất.', 'success');
                    vm.setView('alerts');
                },
                error: function (xhr) {
                    console.error('Lỗi khởi chạy chẩn đoán:', xhr.responseText);
                    vm.showToast('Lỗi', 'Không thể khởi chạy quét cảnh báo.', 'danger');
                    vm.saving = false;
                }
            });
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
