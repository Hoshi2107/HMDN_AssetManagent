/**
 * Hệ thống Quản lý Vật tư Bệnh viện Hoàn Mỹ Đồng Nai - Module Thống kê & Dashboard
 * Logic Vue.js v2 - Đã đồng bộ cấu trúc Database SQL Server [HospitalAssetDB]
 */
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
            filterYear: null,
            filterDept: null,
            filterGroup: null,
            filterStatus: null, // BẮT BUỘC PHẢI CÓ DÒNG NÀY: Để Vue.js liên kết (Binding) với v-model ngoài View
            lookups: { Departments: [], Groups: [] },
            inventoryList: [],
            availableYears: [],

            // Các thực thể đồ thị Chart.js
            pieChart: null,
            barChart: null,
            monthlyMaintenanceChart: null,
            todayChecklistChart: null,

            currentPage: 1,
            pageSize: 15
        },
        mounted: function () {
            this.generateAvailableYears();
            this.fetchDropdownLookups();
            this.fetchKpiOverview();
            this.fetchCostData();
            this.fetchInventoryReport();
            this.initRealtimeSync();
            this.fetchMonthlyMaintenanceData();
            this.fetchTodayChecklistData();
        },
        computed: {
            filteredInventory: function () {
                return this.inventoryList;
            },
            filteredInventoryLength: function () {
                return this.filteredInventory.length;
            },
            totalPages: function () {
                return Math.ceil(this.filteredInventoryLength / this.pageSize);
            },
            paginatedInventory: function () {
                var start = (this.currentPage - 1) * this.pageSize;
                var end = start + this.pageSize;
                return this.filteredInventory.slice(start, end);
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
                    /* ĐỒNG BỘ MAPPING: Đổi từ TotalActive/TotalSuspended sang OperatingWell/BrokenAssets 
                       để khớp chính xác với DashboardOverviewModel trả về từ C# API.
                    */
                    var responseData = data || {
                        TotalAssets: 0,
                        OperatingWell: 0,
                        BrokenAssets: 0,
                        ActivePercentage: 0,
                        BrokenPercentage: 0,
                        HospitalMaintenanceCount: 0,
                        VendorMaintenanceCount: 0
                    };

                    var totalValid = (responseData.OperatingWell || 0) + (responseData.BrokenAssets || 0);

                    if (totalValid > 0) {
                        responseData.ActivePercentage = (responseData.OperatingWell / totalValid) * 100;
                        responseData.BrokenPercentage = (responseData.BrokenAssets / totalValid) * 100;
                    } else {
                        responseData.ActivePercentage = 0;
                        responseData.BrokenPercentage = 0;
                    }

                    vm.kpi = responseData;
                    vm.renderPieChart(vm.kpi); // Truyền đối tượng đã chuẩn hóa dữ liệu vào biểu đồ tròn
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
            fetchInventoryReport: function () {
                var vm = this;
                var params = {
                    departmentId: vm.filterDept,
                    groupId: vm.filterGroup,
                    year: vm.filterYear,
                    status: vm.filterStatus // Gửi chuỗi 'active', 'suspended', 'maintenance_bv', 'maintenance_hang' lên API
                };

                // Gọi AJAX lấy dữ liệu lọc trực tiếp từ câu lệnh SQL Stored Procedure
                $.getJSON(window.AnalyticsEndpoints.getReport, params, function (data) {
                    vm.inventoryList = data || [];
                    vm.currentPage = 1; // Reset về trang số 1 sau khi lọc dữ liệu thành công
                });
            },
            nextPage: function () {
                if (this.currentPage < this.totalPages) this.currentPage++;
            },
            prevPage: function () {
                if (this.currentPage > 1) this.currentPage--;
            },
            resetFilters: function () {
                this.filterDept = null;
                this.filterGroup = null;
                this.filterYear = null;
                this.filterStatus = null;
                this.fetchInventoryReport();
            },
            formatMoney: function (val) {
                if (!val && val !== 0) return '0';
                return new Intl.NumberFormat('vi-VN').format(val);
            },
            formatPercent: function (val) {
                if (!val) return '0';
                return parseFloat(val).toFixed(1);
            },
            renderPieChart: function (data) {
                var chartElement = document.getElementById('statusPieChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');
                if (this.pieChart) this.pieChart.destroy();
                this.pieChart = new Chart(ctx, {
                    type: 'doughnut',
                    data: {
                        labels: ['Hoạt động tốt', 'Báo hỏng'],
                        datasets: [{
                            data: [data.OperatingWell, data.BrokenAssets], // Đã sửa đổi gán chuẩn theo cột Database
                            backgroundColor: ['#10b981', '#ef4444'],
                            borderWidth: 3
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { position: 'bottom' } },
                        cutout: '75%'
                    }
                });
            },
            renderBarChart: function (costData) {
                var chartElement = document.getElementById('costBarChart');
                if (!chartElement) return;

                var vm = this;
                var ctx = chartElement.getContext('2d');
                if (this.barChart) this.barChart.destroy();

                this.barChart = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        // Đã tối ưu cắt chuỗi: Nếu chữ quá dài (như Thiết bị chẩn đoán hình ảnh) sẽ tự rút gọn thành "Thiết bị chẩn đo..." cho đẹp
                        labels: costData.map(function (x) {
                            var name = x.CategoryName || 'Chưa phân loại';
                            return name.length > 15 ? name.substring(0, 15) + '...' : name;
                        }),
                        datasets: [{
                            label: 'Chi phí bảo trì (VNĐ)',
                            data: costData.map(function (x) { return x.TotalCost; }),
                            backgroundColor: '#2563eb',
                            borderRadius: 6,
                            maxBarThickness: 45 // Giới hạn độ rộng thanh cột để biểu đồ thanh thoát, không bị phình to
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        scales: {
                            y: {
                                beginAtZero: true,
                                ticks: {
                                    font: { size: 11, weight: 'bold' }
                                }
                            },
                            x: {
                                ticks: {
                                    maxRotation: 0, // ĐÃ SỬA: Khóa chữ nằm ngang cố định 0 độ, tuyệt đối không cho xoay nghiêng rối mắt
                                    minRotation: 0,
                                    font: { size: 11, weight: '600' }, // Làm đậm font chữ nhãn trục hoành cho sắc nét
                                    color: '#4b5563'
                                },
                                grid: { display: false } // Ẩn đường lưới dọc để biểu đồ thông thoáng
                            }
                        },
                        plugins: {
                            legend: { display: false } // Ẩn nhãn chú thích dư thừa phía trên vì tiêu đề đã ghi rõ ràng
                        },
                        onClick: function (evt, elements) {
                            if (elements && elements.length > 0) {
                                var activeElement = elements[0];
                                var clickedGroupLabel = vm.barChart.data.labels[activeElement.index];

                                var foundGroup = vm.lookups.Groups.find(function (g) {
                                    var shortName = g.Name.length > 15 ? g.Name.substring(0, 15) + '...' : g.Name;
                                    return shortName.trim() === clickedGroupLabel.trim();
                                });

                                if (foundGroup) {
                                    vm.filterGroup = foundGroup.Id;
                                    vm.fetchInventoryReport();

                                    var tableSection = document.getElementById('target-inventory-table');
                                    if (tableSection) {
                                        tableSection.scrollIntoView({ behavior: 'smooth', block: 'start' });
                                    }
                                }
                            }
                        }
                    }
                });
            },
            fetchMonthlyMaintenanceData: function () {
                var vm = this;
                // Gọi trúng endpoint lấy tần suất bảo trì theo năm
                $.getJSON(window.AnalyticsEndpoints.getFrequency, { year: vm.selectedYear }, function (res) {
                    if (res && res.length > 0) {
                        // Thực hiện bóc tách mảng object từ C# API gửi về thành 2 mảng đơn cho Chart.js nhận diện
                        var dynamicLabels = res.map(function (x) { return x.MonthLabel; });
                        var dynamicCounts = res.map(function (x) { return x.MaintenanceCount; });

                        vm.renderMonthlyMaintenanceChart(dynamicLabels, dynamicCounts);
                    } else {
                        // Mảng cứu cánh phòng trường hợp năm đó chưa phát sinh ca sửa chữa nào
                        var defaultLabels = ['Tháng 1', 'Tháng 2', 'Tháng 3', 'Tháng 4', 'Tháng 5', 'Tháng 6', 'Tháng 7', 'Tháng 8', 'Tháng 9', 'Tháng 10', 'Tháng 11', 'Tháng 12'];
                        var defaultCounts = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0];
                        vm.renderMonthlyMaintenanceChart(defaultLabels, defaultCounts);
                    }
                });
            },
            renderMonthlyMaintenanceChart: function (labels, maintenanceCounts) {
                var chartElement = document.getElementById('maintenanceMonthlyChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');
                if (this.monthlyMaintenanceChart) this.monthlyMaintenanceChart.destroy();

                this.monthlyMaintenanceChart = new Chart(ctx, {
                    type: 'line',
                    data: {
                        labels: labels,
                        datasets: [
                            {
                                label: 'Số lượt bảo trì / sửa chữa',
                                data: maintenanceCounts,
                                borderColor: '#f59e0b',
                                backgroundColor: 'rgba(245, 158, 11, 0.1)',
                                fill: true,
                                tension: 0.3,
                                borderWidth: 2.5,
                                pointRadius: 4
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: { legend: { position: 'top', labels: { boxWidth: 12, font: { size: 11 } } } },
                        scales: { y: { beginAtZero: true, ticks: { stepSize: 1 } } }
                    }
                });
            },
            fetchTodayChecklistData: function () {
                var vm = this;
                $.getJSON(window.AnalyticsEndpoints.getChecklist, function (res) {
                    vm.renderTodayChecklistChart(res.DoneCount, res.PendingCount, res.TotalSchedules);
                });
            },
            renderTodayChecklistChart: function (done, pending, total) {
                var chartElement = document.getElementById('todayChecklistChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');
                if (this.todayChecklistChart) this.todayChecklistChart.destroy();

                var chartLabels = total === 0 ? ['Chưa có lịch trình checklist'] : ['Đã Checklist', 'Chưa làm'];
                var chartData = total === 0 ? [1] : [done, pending];
                var chartColors = total === 0 ? ['#cbd5e1'] : ['#3b82f6', '#e2e8f0'];

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
                            legend: { position: 'bottom', labels: { boxWidth: 10, font: { size: 11 } } },
                            tooltip: { enabled: total > 0 }
                        },
                        cutout: '70%'
                    }
                });
            },
            initRealtimeSync: function () {
                var vm = this;
                try {
                    var socket = io(window.AnalyticsEndpoints.socketServer, { transports: ['websocket'] });
                    socket.on('assetStatusChanged', function () {
                        vm.fetchKpiOverview();
                        vm.fetchCostData();
                        vm.fetchInventoryReport();
                        vm.fetchMonthlyMaintenanceData();
                        vm.fetchTodayChecklistData();
                    });
                } catch (error) {
                    console.warn('Kết nối Socket bảo trì gặp sự cố. Đã chuyển sang chế độ tự động đồng bộ.', error);
                }
            }
        }
    });
});