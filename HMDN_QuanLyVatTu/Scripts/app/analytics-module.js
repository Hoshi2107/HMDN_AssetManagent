window.addEventListener('DOMContentLoaded', function () {

    new Vue({
        el: '#analytics-module-hub',
        data: {
            kpi: { TotalAssets: 0, TotalActive: 0, TotalSuspended: 0, ActivePercentage: 0, SuspendedPercentage: 0 },
            selectedYear: 2026,
            filterYear: 2026,
            filterDept: null,
            filterGroup: null,
            lookups: { Departments: [], Groups: [] },
            inventoryList: [],
            availableYears: [],
            pieChart: null,
            barChart: null,

            // Khai báo biến hỗ trợ phân trang dữ liệu bảng
            currentPage: 1,
            pageSize: 15
        },
        mounted: function () {
            this.generateAvailableYears();
            this.fetchKpiOverview();
            this.fetchCostData();
            this.fetchDropdownLookups();
            this.fetchInventoryReport();
            this.initRealtimeSync();
        },
        computed: {
            // Đếm tổng số lượng thiết bị sau khi lọc
            filteredInventoryLength: function () {
                return this.inventoryList.length;
            },
            // Tính toán tổng số trang dựa trên mốc 15 dòng/trang
            totalPages: function () {
                return Math.ceil(this.filteredInventoryLength / this.pageSize);
            },
            // Tự động cắt mảng dữ liệu để hiển thị đúng trang hiện hành
            paginatedInventory: function () {
                var start = (this.currentPage - 1) * this.pageSize;
                var end = start + this.pageSize;
                return this.inventoryList.slice(start, end);
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
                    vm.kpi = data;
                    vm.renderPieChart(data);
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
                    vm.lookups = data;
                });
            },
            fetchInventoryReport: function () {
                var vm = this;
                var params = {
                    departmentId: vm.filterDept,
                    groupId: vm.filterGroup,
                    year: vm.filterYear
                };
                $.getJSON(window.AnalyticsEndpoints.getReport, params, function (data) {
                    vm.inventoryList = data;
                    vm.currentPage = 1; // Đưa về trang thứ nhất mỗi khi thay đổi bộ lọc
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
                this.filterYear = 2026;
                this.fetchInventoryReport();
            },
            formatMoney: function (val) {
                if (!val && val !== 0) return '0';
                return new Intl.NumberFormat('vi-VN').format(val);
            },
            // Hàm xử lý định dạng làm tròn số phần trăm đẹp mắt (Chỉ lấy tối đa 1 hoặc 2 chữ số thập phân)
            formatPercent: function (val) {
                if (!val) return '0';
                return parseFloat(val).toFixed(1); // Trả về dạng 90.9% hoặc 9.1% thay vì chuỗi vô tận
            },
            renderPieChart: function (data) {
                var chartElement = document.getElementById('statusPieChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');
                if (this.pieChart) this.pieChart.destroy();
                this.pieChart = new Chart(ctx, {
                    type: 'doughnut',
                    data: {
                        labels: ['Hoạt động tốt', 'Hỏng / Bảo trì'],
                        datasets: [{ data: [data.TotalActive, data.TotalSuspended], backgroundColor: ['#10b981', '#ef4444'], borderWidth: 3 }]
                    },
                    options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }, cutout: '75%' }
                });
            },
            renderBarChart: function (costData) {
                var chartElement = document.getElementById('costBarChart');
                if (!chartElement) return;
                var ctx = chartElement.getContext('2d');
                if (this.barChart) this.barChart.destroy();
                this.barChart = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: costData.map(function (x) { return x.CategoryName; }),
                        datasets: [{ label: 'Chi phí bảo trì (VNĐ)', data: costData.map(function (x) { return x.TotalCost; }), backgroundColor: '#2563eb', borderRadius: 4 }]
                    },
                    options: { responsive: true, maintainAspectRatio: false, scales: { y: { beginAtZero: true } } }
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
                    });
                } catch (error) {
                    console.warn('Socket.IO disconnected. Realtime disabled.', error);
                }
            }
        }
    });
});