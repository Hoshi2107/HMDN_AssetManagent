/**
 * Hệ thống Quản lý Vật tư Bệnh viện - Module Thống kê & Dashboard
 * Logic Vue.js v2, Tương tác đồ thị Drill-down & Trích xuất file Excel phân nhóm
 */
window.addEventListener('DOMContentLoaded', function () {

    new Vue({
        el: '#analytics-module-hub',
        data: {
            kpi: { TotalAssets: 0, TotalActive: 0, TotalSuspended: 0, ActivePercentage: 0, SuspendedPercentage: 0 },
            selectedYear: 2026,
            filterYear: null, // Mặc định để null để hiển thị toàn bộ thiết bị khi mới load trang
            filterDept: null,
            filterGroup: null,
            lookups: { Departments: [], Groups: [] },
            inventoryList: [],
            availableYears: [],
            pieChart: null,
            barChart: null,

            // Cấu hình phân trang danh sách bảng dữ liệu
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
            filteredInventoryLength: function () {
                return this.inventoryList.length;
            },
            totalPages: function () {
                return Math.ceil(this.filteredInventoryLength / this.pageSize);
            },
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
                    vm.currentPage = 1;
                });
            },

            // TÍNH NĂNG XUẤT EXCEL: Gọi chuẩn Action Controller phân nhóm Khoa phòng
            exportExcelReport: function () {
                var dept = this.filterDept !== null ? this.filterDept : "";
                var group = this.filterGroup !== null ? this.filterGroup : "";
                var yr = this.filterYear !== null ? this.filterYear : "";

                // Gọi thẳng tên Controller/Action chuẩn của MVC truyền thống để chạy mượt mà nhất
                var downloadUrl = "/Analytics/ExportInventoryToExcel?departmentId=" + dept + "&groupId=" + group + "&year=" + yr;

                // Thực hiện lệnh kích hoạt tải file
                window.location.href = downloadUrl;
            }, // ĐÃ SỬA: Bổ sung dấu phẩy ngăn cách chí mạng ở đây

            nextPage: function () {
                if (this.currentPage < this.totalPages) this.currentPage++;
            },
            prevPage: function () {
                if (this.currentPage > 1) this.currentPage--;
            },
            resetFilters: function () {
                this.filterDept = null;
                this.filterGroup = null;
                this.filterYear = null; // Trả về null để khi bấm làm mới sẽ hiện lại tất cả các năm
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
                        labels: ['Hoạt động tốt', 'Hỏng / Bảo trì'],
                        datasets: [{ data: [data.TotalActive, data.TotalSuspended], backgroundColor: ['#10b981', '#ef4444'], borderWidth: 3 }]
                    },
                    options: { responsive: true, maintainAspectRatio: false, plugins: { legend: { position: 'bottom' } }, cutout: '75%' }
                });
            },

            // TÍNH NĂNG TƯƠNG TÁC ĐỒ THỊ: Click cột tự động gán bộ lọc và cuộn màn hình xuống bảng
            renderBarChart: function (costData) {
                var chartElement = document.getElementById('costBarChart');
                if (!chartElement) return;

                var vm = this;
                var ctx = chartElement.getContext('2d');
                if (this.barChart) this.barChart.destroy();

                this.barChart = new Chart(ctx, {
                    type: 'bar',
                    data: {
                        labels: costData.map(function (x) { return x.CategoryName; }),
                        datasets: [{ label: 'Chi phí bảo trì (VNĐ)', data: costData.map(function (x) { return x.TotalCost; }), backgroundColor: '#2563eb', borderRadius: 4 }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        scales: { y: { beginAtZero: true } },
                        onClick: function (evt, elements) {
                            if (elements && elements.length > 0) {
                                var activeElement = elements[0];
                                var clickedGroupLabel = vm.barChart.data.labels[activeElement.index];

                                var foundGroup = vm.lookups.Groups.find(function (g) {
                                    return g.Name.trim() === clickedGroupLabel.trim();
                                });

                                if (foundGroup) {
                                    vm.filterGroup = foundGroup.Id;
                                    vm.fetchInventoryReport();

                                    // Tự động cuộn màn hình xuống vùng bảng danh sách
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