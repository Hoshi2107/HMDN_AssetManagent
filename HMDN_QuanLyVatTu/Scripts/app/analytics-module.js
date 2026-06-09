/**
 * Hệ thống Quản lý Vật tư Bệnh viện Hoàn Mỹ Đồng Nai - Module Thống kê & Dashboard Vue.js
 */

// Helper: Loại bỏ dấu tiếng Việt chuẩn xác
function removeVietnameseTones(str) {
    if (!str) return '';
    str = str.replace(/à|á|ạ|ả|ã|â|ầ|ấ|ậ|ẩ|ẫ|ă|ằ|ắ|ặ|ẳ|ẵ/g, "a");
    str = str.replace(/è|é|ẹ|ẻ|ẽ|ê|ề|ế|ệ|ể|ễ/g, "e");
    str = str.replace(/ì|í|ị|ỉ|ĩ/g, "i");
    str = str.replace(/ò|ó|ọ|ỏ|õ|ô|ồ|ố|ộ|ổ|ỗ|ơ|ờ|ớ|ợ|ở|ỡ/g, "o");
    str = str.replace(/ù|ú|ụ|ủ|ũ|ư|ừ|ứ|ự|ử|ữ/g, "u");
    str = str.replace(/ỳ|ý|ỵ|ỷ|ỹ/g, "y");
    str = str.replace(/đ/g, "d");
    str = str.replace(/À|Á|Ạ|Ả|Ã|Â|Ầ|Ấ|Ậ|Ẩ|Ẫ|Ă|Ằ|Ắ|Ặ|Ẳ|Ẵ/g, "A");
    str = str.replace(/È|É|Ẹ|Ẻ|Ẽ|Ê|Ề|Ế|Ệ|Ể|Ễ/g, "E");
    str = str.replace(/Ì|Í|Ị|Ỉ|Ĩ/g, "I");
    str = str.replace(/Ò|Ó|Ọ|Ỏ|Õ|Ô|Ồ|Ố|Ộ|Ổ|Ỗ|Ơ|Ờ|Ớ|Ợ|Ở|Ỡ/g, "O");
    str = str.replace(/Ù|Ú|Ụ|Ủ|Ũ|Ư|Ừ|Ứ|Ự|Ử|Ữ/g, "U");
    str = str.replace(/Ỳ|Ý|Ỵ|Ỷ|Ỹ/g, "Y");
    str = str.replace(/Đ/g, "D");
    str = str.normalize('NFD').replace(/[\u0300-\u036f]/g, "");
    return str.toLowerCase().trim();
}

// Helper: Debounce trì hoãn gọi hàm
function debounce(func, wait) {
    var timeout;
    return function () {
        var context = this, args = arguments;
        clearTimeout(timeout);
        timeout = setTimeout(function () {
            func.apply(context, args);
        }, wait);
    };
}

window.addEventListener('DOMContentLoaded', function () {

    new Vue({
        el: '#analytics-module-hub',
        data: {
            kpi: {
                TotalAssets: 0,
                OperatingWell: 0,
                BrokenAssets: 0,
                ActivePercentage: 0,
                BrokenPercentage: 0,
                HospitalMaintenanceCount: 0,
                VendorMaintenanceCount: 0
            },
            selectedYear: 2026,
            checklistRange: 'today',
            showCostChart: true,
            lookups: { Departments: [], Groups: [] },
            inventoryList: [],
            availableYears: [],
            checklistProgress: { done: 0, pending: 0, total: 0 },

            pieChart: null,
            barChart: null,
            monthlyMaintenanceChart: null,
            todayChecklistChart: null,

            // Trạng thái cho popup xem danh sách theo trạng thái
            statusModal: {
                status: '',
                title: '',
                devices: [],
                searchQuery: '',
                filterDept: null,
                filterGroup: null,
                currentPage: 1,
                pageSize: 6,
                viewMode: 'list',
                selectedDevice: null,
                loading: false
            },

            // Trạng thái cho popup xem danh sách checklist logs
            checklistModal: {
                range: 'today',
                tab: 'completed', // 'completed' | 'pending'
                logs: [],
                pendingSchedules: [],
                filterUser: '',
                filterDevice: '',
                filterResult: '',
                currentPage: 1,
                pageSize: 6,
                viewMode: 'list',
                selectedLog: null,
                loading: false
            }
        },
        mounted: function () {
            this.generateAvailableYears();
            this.fetchDropdownLookups();
            this.fetchKpiOverview();
            this.fetchCostData();
            this.fetchInventoryReport(); // Tải dữ liệu báo cáo danh sách ngay lập tức
            this.initRealtimeSync();
            this.fetchMonthlyMaintenanceData();
            this.fetchTodayChecklistData();
        },
        watch: {
            // Khi metrics thay đổi (do lọc/tìm kiếm), cập nhật lại dữ liệu biểu đồ Doughnut
            dashboardMetrics: {
                handler: function (newMetrics) {
                    this.updatePieChart(newMetrics);
                },
                deep: true
            },
            'statusModal.filterDept': function () {
                this.statusModal.currentPage = 1;
            },
            'statusModal.filterGroup': function () {
                this.statusModal.currentPage = 1;
            },
            'checklistModal.filterUser': function () {
                this.checklistModal.currentPage = 1;
            },
            'checklistModal.filterDevice': function () {
                this.checklistModal.currentPage = 1;
            },
            'checklistModal.filterResult': function () {
                this.checklistModal.currentPage = 1;
            }
        },
        computed: {
            dashboardMetrics: function () {
                var vm = this;
                var total = vm.kpi.TotalAssets || 0;
                var active = vm.kpi.OperatingWell || 0;
                var broken = vm.kpi.BrokenAssets || 0;
                var maintBv = vm.kpi.HospitalMaintenanceCount || 0;
                var maintHang = vm.kpi.VendorMaintenanceCount || 0;

                var totalMaint = maintBv + maintHang;
                var activePercent = total > 0 ? (active / total) * 100 : 0;
                var brokenPercent = total > 0 ? (broken / total) * 100 : 0;

                // Tính tổng tiền an toàn (null-safe) từ danh sách đầy đủ
                var totalValue = 0;
                for (var i = 0; i < vm.inventoryList.length; i++) {
                    totalValue += (vm.inventoryList[i].TotalPrice || 0);
                }

                return {
                    total: total,
                    active: active,
                    broken: broken,
                    maintBv: maintBv,
                    maintHang: maintHang,
                    totalMaint: totalMaint,
                    activePercent: activePercent,
                    brokenPercent: brokenPercent,
                    totalValue: totalValue
                };
            },
            filteredPopupDevices: function () {
                var vm = this;
                var list = vm.statusModal.devices || [];

                // Lọc theo khoa phòng ban nếu được chọn
                if (vm.statusModal.filterDept) {
                    list = list.filter(function (item) {
                        return item.DepartmentId == vm.statusModal.filterDept;
                    });
                }

                // Lọc theo nhóm thiết bị y tế nếu được chọn
                if (vm.statusModal.filterGroup) {
                    list = list.filter(function (item) {
                        return item.GroupId == vm.statusModal.filterGroup;
                    });
                }

                if (vm.statusModal.searchQuery) {
                    var queryNormalized = removeVietnameseTones(vm.statusModal.searchQuery);
                    list = list.filter(function (item) {
                        var code = removeVietnameseTones(item.AssetCode || '');
                        var name = removeVietnameseTones(item.ItemName || '');
                        return code.indexOf(queryNormalized) !== -1 || name.indexOf(queryNormalized) !== -1;
                    });
                }
                return list;
            },
            popupTotalPages: function () {
                var len = this.filteredPopupDevices.length;
                return Math.ceil(len / this.statusModal.pageSize);
            },
            paginatedPopupDevices: function () {
                var start = (this.statusModal.currentPage - 1) * this.statusModal.pageSize;
                var end = start + this.statusModal.pageSize;
                return this.filteredPopupDevices.slice(start, end);
            },
            checklistCompliance: function () {
                var total = this.checklistProgress.total;
                var done = this.checklistProgress.done;
                return total > 0 ? Math.round((done / total) * 100) : 100;
            },
            checklistRangeText: function () {
                var range = this.checklistRange;
                if (range === 'week') return 'Tuần này';
                if (range === 'month') return 'Tháng này';
                if (range === 'quarter') return 'Quý này';
                if (range === 'year') return 'Năm nay';
                return 'Hôm nay';
            },
            filteredChecklistLogs: function () {
                var vm = this;
                if (vm.checklistModal.tab === 'completed') {
                    var list = vm.checklistModal.logs || [];

                    if (vm.checklistModal.filterUser) {
                        list = list.filter(function (item) {
                            return item.CheckedByName === vm.checklistModal.filterUser;
                        });
                    }

                    if (vm.checklistModal.filterDevice) {
                        list = list.filter(function (item) {
                            return item.ItemName === vm.checklistModal.filterDevice;
                        });
                    }

                    if (vm.checklistModal.filterResult) {
                        list = list.filter(function (item) {
                            return item.OverallResult === vm.checklistModal.filterResult;
                        });
                    }

                    return list;
                } else {
                    var list = vm.checklistModal.pendingSchedules || [];

                    if (vm.checklistModal.filterDevice) {
                        list = list.filter(function (item) {
                            return item.ItemName === vm.checklistModal.filterDevice;
                        });
                    }

                    return list;
                }
            },
            checklistTotalPages: function () {
                var len = this.filteredChecklistLogs.length;
                return Math.ceil(len / this.checklistModal.pageSize);
            },
            paginatedChecklistLogs: function () {
                var start = (this.checklistModal.currentPage - 1) * this.checklistModal.pageSize;
                var end = start + this.checklistModal.pageSize;
                return this.filteredChecklistLogs.slice(start, end);
            },
            checklistUniqueUsers: function () {
                var list = this.checklistModal.logs || [];
                var users = [];
                for (var i = 0; i < list.length; i++) {
                    var u = list[i].CheckedByName;
                    if (u && users.indexOf(u) === -1) {
                        users.push(u);
                    }
                }
                return users.sort();
            },
            checklistUniqueDevices: function () {
                var list = this.checklistModal.tab === 'completed' 
                    ? (this.checklistModal.logs || []) 
                    : (this.checklistModal.pendingSchedules || []);
                var devices = [];
                for (var i = 0; i < list.length; i++) {
                    var d = list[i].ItemName;
                    if (d && devices.indexOf(d) === -1) {
                        devices.push(d);
                    }
                }
                return devices.sort();
            },
            checklistModalRangeText: function () {
                var range = this.checklistModal.range;
                if (range === 'week') return 'Tuần này';
                if (range === 'month') return 'Tháng này';
                if (range === 'quarter') return 'Quý này';
                if (range === 'year') return 'Năm nay';
                return 'Hôm nay';
            }
        },
        methods: {
            generateAvailableYears: function () {
                var startYear = 2026;
                var currentYear = new Date().getFullYear();
                if (currentYear < startYear) currentYear = startYear;
                var years = [];
                for (var y = startYear; y <= currentYear + 1; y++) {
                    years.push(y);
                }
                this.availableYears = years;
            },
            fetchKpiOverview: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getSummary, function (data) {
                    var responseData = data || {
                        TotalAssets: 0,
                        OperatingWell: 0,
                        BrokenAssets: 0,
                        ActivePercentage: 0,
                        BrokenPercentage: 0,
                        HospitalMaintenanceCount: 0,
                        VendorMaintenanceCount: 0
                    };
                    vm.kpi = responseData;
                    vm.renderPieChart();
                });
            },
            fetchCostData: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getCosts, { year: vm.selectedYear }, function (data) {
                    vm.renderBarChart(data);
                });
            },
            fetchDropdownLookups: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getLookups, function (data) {
                    vm.lookups = data || { Departments: [], Groups: [] };
                });
            },
            toggleCostChart: function () {
                this.showCostChart = !this.showCostChart;
            },
            showDevicesByStatus: function (status) {
                this.openStatusDevicesModal(status);
            },
            openStatusDevicesModal: function (status) {
                var vm = this;
                vm.statusModal.status = status;
                
                // Thiết lập tiêu đề động tương ứng với từng trạng thái
                var statusLabel = '';
                if (status === 'active') {
                    statusLabel = 'Thiết bị đang chạy tốt';
                } else if (status === 'suspended') {
                    statusLabel = 'Thiết bị đang báo hỏng';
                } else if (status === 'maintenance_bv') {
                    statusLabel = 'Thiết bị bệnh viện tự sửa';
                } else if (status === 'maintenance_hang') {
                    statusLabel = 'Thiết bị hãng đối tác sửa';
                } else if (status === 'maintenance') {
                    statusLabel = 'Thiết bị đang bảo trì';
                } else {
                    statusLabel = 'Tất cả thiết bị y tế';
                }
                vm.statusModal.title = statusLabel;
                vm.statusModal.devices = [];
                vm.statusModal.searchQuery = '';
                vm.statusModal.filterDept = null;
                vm.statusModal.filterGroup = null;
                vm.statusModal.currentPage = 1;
                vm.statusModal.viewMode = 'list';
                vm.statusModal.selectedDevice = null;
                vm.statusModal.loading = true;

                // Hiển thị modal bằng Bootstrap
                $('#statusDevicesModal').modal('show');

                // Lấy tham số trạng thái thích hợp để gọi API
                var apiStatus = null;
                if (status === 'active') apiStatus = 'active';
                else if (status === 'suspended') apiStatus = 'suspended';
                else if (status === 'maintenance') apiStatus = 'maintenance_any';
                else if (status === 'maintenance_bv') apiStatus = 'maintenance_bv';
                else if (status === 'maintenance_hang') apiStatus = 'maintenance_hang';

                var params = {
                    status: apiStatus
                };

                $.getJSON(window.AnalyticsEndpoints.getReport, params, function (data) {
                    vm.statusModal.devices = data || [];
                    vm.statusModal.loading = false;
                }).fail(function () {
                    vm.statusModal.loading = false;
                });
            },
            viewPopupDeviceDetails: function (item) {
                this.statusModal.selectedDevice = item;
                this.statusModal.viewMode = 'details';
            },
            nextPopupPage: function () {
                if (this.statusModal.currentPage < this.popupTotalPages) {
                    this.statusModal.currentPage++;
                }
            },
            prevPopupPage: function () {
                if (this.statusModal.currentPage > 1) {
                    this.statusModal.currentPage--;
                }
            },
            fetchInventoryReport: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getReport, function (data) {
                    vm.inventoryList = data || [];
                });
            },
            formatMoney: function (val) {
                if (!val && val !== 0) return '0';
                return new Intl.NumberFormat('vi-VN').format(val);
            },
            formatPercent: function (val) {
                if (!val) return '0';
                return parseFloat(val).toFixed(1);
            },
            // Biểu đồ Doughnut (Cấu hình 3 trạng thái hoặc empty state)
            renderPieChart: function () {
                var chartElement = document.getElementById('statusPieChart');
                if (!chartElement) return;
                var vm = this;
                var ctx = chartElement.getContext('2d');
                if (this.pieChart) {
                    this.pieChart.destroy();
                }

                var metrics = vm.dashboardMetrics;
                var labels = ['Hoạt động tốt', 'Báo hỏng', 'BV tự sửa', 'Hãng sửa'];
                var chartData = [metrics.active, metrics.broken, metrics.maintBv, metrics.maintHang];
                var colors = ['#10b981', '#ef4444', '#f59e0b', '#0ea5e9'];

                // Xử lý empty state cho biểu đồ
                if (metrics.total === 0) {
                    labels = ['Không có dữ liệu'];
                    chartData = [1];
                    colors = ['#cbd5e1'];
                }

                this.pieChart = new Chart(ctx, {
                    type: 'doughnut',
                    data: {
                        labels: labels,
                        datasets: [{
                            data: chartData,
                            backgroundColor: colors,
                            borderWidth: 2
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { 
                            legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10, family: "'Inter', sans-serif" } } },
                            tooltip: {
                                backgroundColor: 'rgba(15, 23, 42, 0.9)',
                                titleFont: { size: 12, weight: 'bold', family: "'Inter', sans-serif" },
                                bodyFont: { size: 12, family: "'Inter', sans-serif" },
                                padding: 10,
                                cornerRadius: 8,
                                boxWidth: 8,
                                boxHeight: 8,
                                usePointStyle: true
                            }
                        },
                        cutout: '75%',
                        onClick: function (evt, elements) {
                            if (elements && elements.length > 0) {
                                var activeElement = elements[0];
                                var index = activeElement.index;
                                // Thống nhất map theo label thay vì index cứng để tránh sai sót
                                var label = vm.pieChart.data.labels[index];

                                if (label === 'Hoạt động tốt') {
                                    vm.showDevicesByStatus('active');
                                } else if (label === 'Báo hỏng') {
                                    vm.showDevicesByStatus('suspended');
                                } else if (label === 'BV tự sửa') {
                                    vm.showDevicesByStatus('maintenance_bv');
                                } else if (label === 'Hãng sửa') {
                                    vm.showDevicesByStatus('maintenance_hang');
                                }
                            }
                        },
                        onHover: function (evt, elements) {
                            evt.native.target.style.cursor = elements.length ? 'pointer' : 'default';
                        }
                    }
                });
            },
            // Cập nhật biểu đồ Doughnut mà không destroy/recreate
            updatePieChart: function (metrics) {
                if (!this.pieChart) return;

                if (metrics.total > 0) {
                    this.pieChart.data.labels = ['Hoạt động tốt', 'Báo hỏng', 'BV tự sửa', 'Hãng sửa'];
                    this.pieChart.data.datasets[0].data = [metrics.active, metrics.broken, metrics.maintBv, metrics.maintHang];
                    this.pieChart.data.datasets[0].backgroundColor = ['#10b981', '#ef4444', '#f59e0b', '#0ea5e9'];
                } else {
                    this.pieChart.data.labels = ['Không có dữ liệu'];
                    this.pieChart.data.datasets[0].data = [1];
                    this.pieChart.data.datasets[0].backgroundColor = ['#cbd5e1'];
                }
                this.pieChart.update();
            },
            // Cập nhật Bar Chart sử dụng .update() thay vì hủy và tạo mới
            renderBarChart: function (costData) {
                var chartElement = document.getElementById('costBarChart');
                if (!chartElement) return;

                var vm = this;
                var ctx = chartElement.getContext('2d');

                var labels = costData.map(function (x) {
                    var name = x.CategoryName || 'Chưa phân loại';
                    return name.length > 15 ? name.substring(0, 15) + '...' : name;
                });
                var dataValues = costData.map(function (x) { return x.TotalCost; });

                if (this.barChart) {
                    this.barChart.data.labels = labels;
                    this.barChart.data.datasets[0].data = dataValues;
                    this.barChart.update();
                    return;
                }

                this.barChart = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Chi phí bảo trì (VNĐ)',
                            data: dataValues,
                            backgroundColor: '#3b82f6',
                            borderRadius: 6,
                            maxBarThickness: 40
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        scales: {
                            y: { 
                                beginAtZero: true, 
                                ticks: { 
                                    font: { size: 10, weight: '600' },
                                    callback: function (value) {
                                        return vm.formatMoney(value);
                                    }
                                } 
                            },
                            x: {
                                ticks: {
                                    maxRotation: 0,
                                    minRotation: 0,
                                    font: { size: 10, weight: '600' },
                                    color: '#64748b'
                                },
                                grid: { display: false }
                            }
                        },
                        plugins: { 
                            legend: { display: false },
                            tooltip: {
                                backgroundColor: 'rgba(15, 23, 42, 0.9)',
                                titleFont: { size: 12, weight: 'bold', family: "'Inter', sans-serif" },
                                bodyFont: { size: 12, family: "'Inter', sans-serif" },
                                padding: 10,
                                cornerRadius: 8,
                                boxWidth: 8,
                                boxHeight: 8,
                                usePointStyle: true,
                                callbacks: {
                                    label: function (context) {
                                        return ' Chi phí: ' + vm.formatMoney(context.raw) + ' VNĐ';
                                    }
                                }
                            }
                        }
                    }
                });
            },
            fetchMonthlyMaintenanceData: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getFrequency, { year: vm.selectedYear }, function (res) {
                    if (res && res.length > 0) {
                        var dynamicLabels = res.map(function (x) { return x.MonthLabel; });
                        var dynamicCounts = res.map(function (x) { return Number(x.MaintenanceCount); });
                        vm.renderMonthlyMaintenanceChart(dynamicLabels, dynamicCounts);
                    } else {
                        var defaultLabels = ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6', 'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'];
                        var defaultCounts = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
                        vm.renderMonthlyMaintenanceChart(defaultLabels, defaultCounts);
                    }
                });
            },
            // Cập nhật Line Chart sử dụng .update()
            renderMonthlyMaintenanceChart: function (labels, maintenanceCounts) {
                var chartElement = document.getElementById('maintenanceMonthlyChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');

                if (this.monthlyMaintenanceChart) {
                    this.monthlyMaintenanceChart.data.labels = labels;
                    this.monthlyMaintenanceChart.data.datasets[0].data = maintenanceCounts;
                    this.monthlyMaintenanceChart.update();
                    return;
                }

                this.monthlyMaintenanceChart = new Chart(ctx, {
                    type: 'line',
                    data: {
                        labels: labels,
                        datasets: [{
                            label: 'Số lượt bảo trì / sửa chữa',
                            data: maintenanceCounts,
                            borderColor: '#f59e0b',
                            backgroundColor: 'rgba(245, 158, 11, 0.05)',
                            fill: true,
                            tension: 0.3,
                            borderWidth: 2,
                            pointRadius: 3
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { 
                            legend: { position: 'top', labels: { boxWidth: 10, font: { size: 10, family: "'Inter', sans-serif" } } },
                            tooltip: {
                                backgroundColor: 'rgba(15, 23, 42, 0.9)',
                                titleFont: { size: 12, weight: 'bold', family: "'Inter', sans-serif" },
                                bodyFont: { size: 12, family: "'Inter', sans-serif" },
                                padding: 10,
                                cornerRadius: 8,
                                boxWidth: 8,
                                boxHeight: 8,
                                usePointStyle: true
                            }
                        },
                        scales: {
                            y: {
                                beginAtZero: true,
                                grace: '10%',
                                ticks: {
                                    stepSize: 1
                                },
                                min: 0
                            }
                        }
                    }
                });
            },
            fetchTodayChecklistData: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getChecklist, { range: vm.checklistRange }, function (res) {
                    vm.checklistProgress.done = res.DoneCount || 0;
                    vm.checklistProgress.pending = res.PendingCount || 0;
                    vm.checklistProgress.total = res.TotalSchedules || 0;
                    vm.renderTodayChecklistChart(res.DoneCount, res.PendingCount, res.TotalSchedules);
                });
            },
            // Cập nhật Checklist Chart bằng cách luôn hủy và tạo mới để tránh lỗi thay đổi độ dài mảng dữ liệu trong Chart.js
            renderTodayChecklistChart: function (done, pending, total) {
                var chartElement = document.getElementById('todayChecklistChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');
                var vm = this;

                // Hủy instance cũ để tránh lỗi vẽ đè hoặc lỗi render khi thay đổi cấu trúc dữ liệu của Chart
                if (this.todayChecklistChart) {
                    this.todayChecklistChart.destroy();
                    this.todayChecklistChart = null;
                }

                var emptyLabel = 'Chưa có lịch trình';
                if (vm.checklistRange === 'week') emptyLabel = 'Chưa có lịch tuần này';
                else if (vm.checklistRange === 'month') emptyLabel = 'Chưa có lịch tháng này';
                else if (vm.checklistRange === 'quarter') emptyLabel = 'Chưa có lịch quý này';
                else if (vm.checklistRange === 'year') emptyLabel = 'Chưa có lịch năm nay';

                // Bổ sung hiển thị trực quan số lượng (done / pending) ngay trên nhãn chú thích (Legend)
                var chartLabels = total === 0 ? [emptyLabel] : ['Đã Checklist (' + done + ')', 'Chưa làm (' + pending + ')'];
                var chartData = total === 0 ? [1] : [done, pending];
                var chartColors = total === 0 ? ['#e2e8f0'] : ['#10b981', '#ef4444'];

                this.todayChecklistChart = new Chart(ctx, {
                    type: 'doughnut',
                    data: {
                        labels: chartLabels,
                        datasets: [{ data: chartData, backgroundColor: chartColors, borderWidth: 0 }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 10, family: "'Inter', sans-serif" } } },
                            tooltip: { 
                                enabled: total > 0,
                                backgroundColor: 'rgba(15, 23, 42, 0.9)',
                                titleFont: { size: 12, weight: 'bold', family: "'Inter', sans-serif" },
                                bodyFont: { size: 12, family: "'Inter', sans-serif" },
                                padding: 10,
                                cornerRadius: 8,
                                boxWidth: 8,
                                boxHeight: 8,
                                usePointStyle: true
                            }
                        },
                        cutout: '70%',
                        onClick: function (evt, elements) {
                            if (elements && elements.length > 0) {
                                var activeElement = elements[0];
                                var index = activeElement.index;
                                var label = vm.todayChecklistChart.data.labels[index];
                                
                                // Thay vì chuyển hướng, mở modal logs
                                vm.openChecklistLogsModal(vm.checklistRange);
                            }
                        },
                        onHover: function (evt, elements) {
                            evt.native.target.style.cursor = elements.length ? 'pointer' : 'default';
                        }
                    }
                });
            },
            initRealtimeSync: function () {
                var vm = this;
                try {
                    var serverUrl = window.AnalyticsEndpoints.socketServer;
                    if (!serverUrl) {
                        return;
                    }
                    var socket = io(serverUrl, { transports: ['websocket'] });
                    socket.on('assetStatusChanged', function () {
                        vm.fetchKpiOverview();
                        vm.fetchCostData();
                        vm.fetchMonthlyMaintenanceData();
                        vm.fetchTodayChecklistData();
                        vm.fetchInventoryReport();
                    });
                } catch (error) {
                    console.warn('Realtime Socket inactive.');
                }
            },
            // Mở Modal xem thông tin chi tiết thiết bị dạng readonly
            openDeviceDetails: function (item) {
                this.selectedDevice = item;
                this.$nextTick(function () {
                    $('#deviceDetailsModal').modal('show');
                });
            },
            closeStatusDevicesModal: function () {
                $('#statusDevicesModal').modal('hide');
            },
            openChecklistLogsModal: function (initialRange, initialTab) {
                var vm = this;
                vm.checklistModal.range = initialRange || vm.checklistRange || 'today';
                vm.checklistModal.tab = initialTab || 'completed';
                vm.checklistModal.logs = [];
                vm.checklistModal.pendingSchedules = [];
                vm.checklistModal.filterUser = '';
                vm.checklistModal.filterDevice = '';
                vm.checklistModal.filterResult = '';
                vm.checklistModal.currentPage = 1;
                vm.checklistModal.viewMode = 'list';
                vm.checklistModal.selectedLog = null;
                vm.checklistModal.loading = true;

                $('#checklistLogsModal').modal('show');
                vm.fetchChecklistLogs();
            },
            setChecklistModalTab: function (tab) {
                var vm = this;
                vm.checklistModal.tab = tab;
                vm.checklistModal.currentPage = 1;
                vm.checklistModal.filterUser = '';
                vm.checklistModal.filterDevice = '';
                vm.checklistModal.filterResult = '';
                vm.fetchChecklistLogs();
            },
            fetchChecklistLogs: function () {
                var vm = this;
                vm.checklistModal.loading = true;
                vm.checklistModal.currentPage = 1;

                var now = new Date();

                function formatLocal(d) {
                    var year = d.getFullYear();
                    var month = ('0' + (d.getMonth() + 1)).slice(-2);
                    var day = ('0' + d.getDate()).slice(-2);
                    return year + '-' + month + '-' + day;
                }

                var fromDateStr = '';
                var toDateStr = '';
                var range = vm.checklistModal.range;

                if (range === 'today') {
                    fromDateStr = formatLocal(now);
                    toDateStr = fromDateStr;
                } else if (range === 'week') {
                    var diff = (7 + (now.getDay() - 1)) % 7;
                    var start = new Date(now.getFullYear(), now.getMonth(), now.getDate() - diff);
                    var end = new Date(now.getFullYear(), now.getMonth(), start.getDate() + 6);
                    fromDateStr = formatLocal(start);
                    toDateStr = formatLocal(end);
                } else if (range === 'month') {
                    var start = new Date(now.getFullYear(), now.getMonth(), 1);
                    var end = new Date(now.getFullYear(), now.getMonth() + 1, 0);
                    fromDateStr = formatLocal(start);
                    toDateStr = formatLocal(end);
                } else if (range === 'quarter') {
                    var qStartMonth = Math.floor(now.getMonth() / 3) * 3;
                    var start = new Date(now.getFullYear(), qStartMonth, 1);
                    var end = new Date(now.getFullYear(), qStartMonth + 3, 0);
                    fromDateStr = formatLocal(start);
                    toDateStr = formatLocal(end);
                } else if (range === 'year') {
                    var start = new Date(now.getFullYear(), 0, 1);
                    var end = new Date(now.getFullYear(), 12, 0);
                    fromDateStr = formatLocal(start);
                    toDateStr = formatLocal(end);
                }

                if (vm.checklistModal.tab === 'completed') {
                    $.getJSON(window.AnalyticsEndpoints.getLogs, { fromDate: fromDateStr, toDate: toDateStr }, function (res) {
                        vm.checklistModal.logs = res.data || [];
                        vm.checklistModal.loading = false;
                    }).fail(function () {
                        vm.checklistModal.loading = false;
                    });
                } else {
                    $.getJSON('/api/checklists/schedules', { fromDate: fromDateStr, toDate: toDateStr, status: 'pending' }, function (res) {
                        vm.checklistModal.pendingSchedules = res.data || [];
                        vm.checklistModal.loading = false;
                    }).fail(function () {
                        vm.checklistModal.loading = false;
                    });
                }
            },
            viewChecklistLogDetails: function (item) {
                var vm = this;
                vm.checklistModal.loading = true;
                vm.checklistModal.selectedLog = null;
                vm.checklistModal.viewMode = 'details';

                $.getJSON(window.AnalyticsEndpoints.getLogDetails, { logId: item.Id }, function (res) {
                    if (res.success) {
                        vm.checklistModal.selectedLog = res.data;
                    }
                    vm.checklistModal.loading = false;
                }).fail(function () {
                    vm.checklistModal.loading = false;
                });
            },
            nextChecklistPage: function () {
                if (this.checklistModal.currentPage < this.checklistTotalPages) {
                    this.checklistModal.currentPage++;
                }
            },
            prevChecklistPage: function () {
                if (this.checklistModal.currentPage > 1) {
                    this.checklistModal.currentPage--;
                }
            },
            closeChecklistLogsModal: function () {
                $('#checklistLogsModal').modal('hide');
            },
            severityLabel: function (severity) {
                if (severity === 'danger') return 'Khẩn cấp';
                if (severity === 'warning') return 'Cảnh báo';
                return 'Thông tin';
            },
            cycleLabel: function (cycle) {
                if (!cycle) return 'N/A';
                var map = {
                    'daily': 'Hàng ngày',
                    'weekly': 'Hàng tuần',
                    'monthly': 'Hàng tháng',
                    'quarterly': 'Hàng quý',
                    'yearly': 'Hàng năm',
                    'adhoc': 'Đột xuất'
                };
                return map[cycle.toLowerCase()] || cycle;
            }
        }
    });
});