new Vue({
    el: '#app',
    data: {
        searchQuery: '',
        filterStatus: '',
        filterYear: '',
        availableYears: [],
        activeDropdown: null,
        selectedRows: [],
        
        sortKey: '',
        sortAsc: true,
        currentPage: 1,
        pageSize: 15,
        
        tableWidth: 1200,
        isSyncingTop: false,
        isSyncingBottom: false,

        showStatusModal: false,
        showLogModal: false,
        showAddAssetModal: false,
        isSavingAsset: false,
        selectedDevice: null,
        formStatus: '',
        formReplacedBy: '',
        formReason: '',

        devices: window.DevicesJsonData || [],
        groupsList: window.GroupsJsonData || [],
        departmentsList: window.DepartmentsJsonData || [],
        locationsList: window.LocationsJsonData || [],

        formAddAsset: {
            assetCode: '',
            itemName: '',
            groupId: '',
            serialNumber: '',
            quantity: 1,
            locationId: '',
            departmentId: '',
            importDate: new Date().toISOString().substring(0, 10),
            expiryDate: '',
            warrantyExpiry: '',
            unitPrice: 0,
            note: ''
        },

        mockLogs: []
    },
    computed: {
        processedDevices() {
            let list = this.devices;
            if(this.filterStatus) list = list.filter(d => d.status === this.filterStatus);
            if(this.filterYear) {
                list = list.filter(d => d.expiryDate && d.expiryDate.endsWith(this.filterYear));
            }
            if(this.searchQuery) {
                const q = this.searchQuery.toLowerCase();
                list = list.filter(d => d.id.toLowerCase().includes(q) || d.name.toLowerCase().includes(q));
            }
            if (this.sortKey) {
                list = list.sort((a, b) => {
                    let valA = a[this.sortKey];
                    let valB = b[this.sortKey];
                    if (valA == null) valA = '';
                    if (valB == null) valB = '';
                    if (typeof valA === 'string') valA = valA.toLowerCase();
                    if (typeof valB === 'string') valB = valB.toLowerCase();
                    if (valA < valB) return this.sortAsc ? -1 : 1;
                    if (valA > valB) return this.sortAsc ? 1 : -1;
                    return 0;
                });
            }
            return list;
        },
        paginatedDevices() {
            const start = (this.currentPage - 1) * this.pageSize;
            return this.processedDevices.slice(start, start + this.pageSize);
        },
        totalPages() {
            return Math.ceil(this.processedDevices.length / this.pageSize) || 1;
        },
        visiblePages() {
            let pages = [];
            let start = Math.max(1, this.currentPage - 2);
            let end = Math.min(this.totalPages, start + 4);
            if (end - start < 4) start = Math.max(1, end - 4);
            for(let i=start; i<=end; i++) pages.push(i);
            return pages;
        },
        totalClosingValue() {
            return this.devices.reduce((sum, d) => sum + (d.closingValue || 0), 0);
        },
        isAllSelected() {
            return this.paginatedDevices.length > 0 && 
                   this.paginatedDevices.every(d => this.selectedRows.includes(d.id));
        }
    },
    watch: {
        processedDevices() { 
            this.currentPage = 1;
            this.selectedRows = []; 
        },
        paginatedDevices() {
            this.$nextTick(() => {
                this.updateTableWidth();
            });
        },
        devices: {
            deep: true,
            handler() {
                this.$nextTick(() => this.renderCharts());
            }
        }
    },
    methods: {
        sortBy(key) {
            if (this.sortKey === key) {
                this.sortAsc = !this.sortAsc;
            } else {
                this.sortKey = key;
                this.sortAsc = true;
            }
        },
        exportCSV() {
            this.downloadCSV(this.processedDevices, "DanhSachKhauHao_" + this.filterYear + ".csv");
        },
        exportSelectedCSV() {
            const selectedData = this.devices.filter(d => this.selectedRows.includes(d.id));
            this.downloadCSV(selectedData, "KhauHao_DaChon.csv");
        },
        downloadCSV(dataArray, filename) {
            let csvContent = "data:text/csv;charset=utf-8,\uFEFF";
            csvContent += "Mã tài sản,Tên thiết bị,Ngày nhập,Hạn BH,Hạn SD,Trạng thái,Nguyên giá,Khấu hao lũy kế,Giá trị còn lại,Thay thế bởi\n";
            dataArray.forEach(d => {
                let row = [
                    `"${d.id}"`,
                    `"${d.name}"`,
                    `"${d.importDate || ''}"`,
                    `"${d.warrantyExpiry || ''}"`,
                    `"${d.expiryDate || ''}"`,
                    `"${this.statusText(d.status)}"`,
                    d.openingValue || 0,
                    d.depreciation || 0,
                    d.closingValue || 0,
                    `"${d.replacedBy || ''}"`
                ];
                csvContent += row.join(",") + "\n";
            });
            const encodedUri = encodeURI(csvContent);
            const link = document.createElement("a");
            link.setAttribute("href", encodedUri);
            link.setAttribute("download", filename);
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        },
        openBatchStatusModal() {
            this.selectedDevice = null; // null means batch update
            this.formStatus = 'active';
            this.formReplacedBy = '';
            this.formReason = '';
            this.showStatusModal = true;
            this.closeDropdowns();
        },
        countStatus(st) { return this.devices.filter(d => d.status === st).length; },
        statusText(st) {
            return { active:'Đang sử dụng', suspended:'Tạm ngưng', disposed:'Đã thanh lý', replaced:'Đã thay mới' }[st] || st;
        },
        calcDepPercent(d) {
            if (!d.openingValue || d.openingValue === 0) return 0;
            let percent = (d.depreciation / d.openingValue) * 100;
            if (percent > 100) percent = 100;
            if (percent < 0) percent = 0;
            return Number(percent.toFixed(2));
        },
        formatMoney(val) {
            return new Intl.NumberFormat('vi-VN').format(val);
        },
        toggleSelectAll(e) {
            if (e.target.checked) {
                // Select all on current page
                const ids = this.paginatedDevices.map(d => d.id);
                this.selectedRows = Array.from(new Set([...this.selectedRows, ...ids]));
            } else {
                // Deselect all on current page
                const ids = this.paginatedDevices.map(d => d.id);
                this.selectedRows = this.selectedRows.filter(id => !ids.includes(id));
            }
        },
        toggleDropdown(id) {
            this.activeDropdown = this.activeDropdown === id ? null : id;
        },
        closeDropdowns() { this.activeDropdown = null; },

        renderCharts() {
            if (typeof Chart === 'undefined') return;

            // STATUS CHART
            const ctxStatus = document.getElementById('statusChart');
            if (ctxStatus) {
                if (window.statusChartInstance) window.statusChartInstance.destroy();
                
                const active = this.countStatus('active');
                const suspended = this.countStatus('suspended');
                const disposed = this.countStatus('disposed');
                const replaced = this.countStatus('replaced');

                window.statusChartInstance = new Chart(ctxStatus, {
                    type: 'doughnut',
                    data: {
                        labels: ['Đang sử dụng', 'Tạm ngưng', 'Đã thanh lý', 'Đã thay mới'],
                        datasets: [{
                            data: [active, suspended, disposed, replaced],
                            backgroundColor: ['#10b981', '#f59e0b', '#94a3b8', '#3b82f6'],
                            borderWidth: 0,
                            hoverOffset: 4
                        }]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        plugins: {
                            legend: { position: 'right', labels: { usePointStyle: true, boxWidth: 8, font: { size: 11, family: 'Inter' } } }
                        },
                        cutout: '70%'
                    }
                });
            }

            // VALUE CHART
            const ctxValue = document.getElementById('valueChart');
            if (ctxValue) {
                if (window.valueChartInstance) window.valueChartInstance.destroy();
                
                let totalDepreciation = this.devices.reduce((sum, d) => sum + (d.depreciation || 0), 0);
                let totalClosing = this.devices.reduce((sum, d) => sum + (d.closingValue || 0), 0);

                window.valueChartInstance = new Chart(ctxValue, {
                    type: 'bar',
                    data: {
                        labels: ['Tổng Giá Trị Tài Sản'],
                        datasets: [
                            {
                                label: 'Giá trị còn lại (VNĐ)',
                                data: [totalClosing],
                                backgroundColor: '#10b981',
                                borderRadius: 6
                            },
                            {
                                label: 'Khấu hao lũy kế (VNĐ)',
                                data: [totalDepreciation],
                                backgroundColor: '#ef4444',
                                borderRadius: 6
                            }
                        ]
                    },
                    options: {
                        responsive: true,
                        maintainAspectRatio: false,
                        indexAxis: 'y',
                        plugins: {
                            legend: { position: 'bottom', labels: { usePointStyle: true, boxWidth: 8, font: { size: 11 } } },
                            tooltip: {
                                callbacks: {
                                    label: function(context) {
                                        let label = context.dataset.label || '';
                                        if (label) {
                                            label += ': ';
                                        }
                                        if (context.parsed.x !== null) {
                                            label += new Intl.NumberFormat('vi-VN').format(context.parsed.x) + ' đ';
                                        }
                                        return label;
                                    }
                                }
                            }
                        },
                        scales: {
                            x: { stacked: true, display: false },
                            y: { stacked: true, display: false }
                        }
                    }
                });
            }
        },

        updateTableWidth() {
            if (this.$refs.mainTable) {
                this.tableWidth = this.$refs.mainTable.offsetWidth;
            }
        },
        syncTopScroll() {
            if (this.isSyncingBottom) {
                this.isSyncingBottom = false;
                return;
            }
            this.isSyncingTop = true;
            if (this.$refs.bottomScroll && this.$refs.topScroll) {
                this.$refs.bottomScroll.scrollLeft = this.$refs.topScroll.scrollLeft;
            }
        },
        syncBottomScroll() {
            if (this.isSyncingTop) {
                this.isSyncingTop = false;
                return;
            }
            this.isSyncingBottom = true;
            if (this.$refs.topScroll && this.$refs.bottomScroll) {
                this.$refs.topScroll.scrollLeft = this.$refs.bottomScroll.scrollLeft;
            }
        },

        openStatusModal(device) {
            this.selectedDevice = device;
            this.formStatus = device.status;
            this.formReplacedBy = device.replacedBy || '';
            this.formReason = '';
            this.showStatusModal = true;
            this.closeDropdowns();
        },
        saveStatus() {
            const isBatch = !this.selectedDevice;
            
            let url = '/VongDoiKhauHao/UpdateStatus';
            let options = {};
            
            if (isBatch) {
                if(this.selectedRows.length === 0) return;
                url = '/VongDoiKhauHao/BatchUpdateStatus';
                options = {
                    method: 'POST',
                    body: (() => {
                        const fd = new FormData();
                        this.selectedRows.forEach(code => fd.append('assetCodes', code));
                        fd.append('status', this.formStatus);
                        fd.append('replacedBy', this.formReplacedBy || '');
                        fd.append('reason', this.formReason || '');
                        return fd;
                    })()
                };
            } else {
                const formData = new FormData();
                formData.append('id', this.selectedDevice.dbId);
                formData.append('status', this.formStatus);
                formData.append('replacedBy', this.formReplacedBy || '');
                formData.append('reason', this.formReason || '');
                options = {
                    method: 'POST',
                    body: formData
                };
            }

            fetch(url, options)
            .then(res => res.json())
            .then(data => {
                if(data.success) {
                    if (isBatch) {
                        this.devices.forEach(d => {
                            if(this.selectedRows.includes(d.id)) {
                                d.status = this.formStatus;
                                d.replacedBy = this.formStatus === 'replaced' ? this.formReplacedBy : null;
                            }
                        });
                        this.selectedRows = [];
                    } else {
                        this.selectedDevice.status = this.formStatus;
                        this.selectedDevice.replacedBy = this.formStatus === 'replaced' ? this.formReplacedBy : null;
                    }
                    this.showStatusModal = false;
                    if (window.MedEquip && window.MedEquip.toast) {
                        window.MedEquip.toast('Thành công', data.message, 'success');
                    } else {
                        alert(data.message);
                    }
                } else {
                    if (window.MedEquip && window.MedEquip.toast) {
                        window.MedEquip.toast('Lỗi', data.message || 'Có lỗi xảy ra!', 'danger');
                    } else {
                        alert(data.message || 'Có lỗi xảy ra!');
                    }
                }
            })
            .catch(err => {
                console.error(err);
                if (window.MedEquip && window.MedEquip.toast) {
                    window.MedEquip.toast('Lỗi', 'Lỗi kết nối đến máy chủ!', 'danger');
                } else {
                    alert('Lỗi kết nối đến máy chủ!');
                }
            });
        },

        openLogModal(device) {
            this.selectedDevice = device;
            this.showLogModal = true;
            this.closeDropdowns();
            
            fetch('/VongDoiKhauHao/GetDepreciationLogs?inventoryId=' + device.dbId)
            .then(res => res.json())
            .then(data => {
                if(data.success) {
                    this.mockLogs = data.logs;
                } else {
                    this.mockLogs = [];
                }
            })
            .catch(err => {
                console.error("Error fetching logs:", err);
                this.mockLogs = [];
            });
        },
        refreshData() {
            let calcYear = this.filterYear || new Date().getFullYear();
            const formData = new FormData();
            formData.append('calculateYear', calcYear);

            fetch('/VongDoiKhauHao/CalculateDepreciation', {
                method: 'POST',
                body: formData
            })
            .then(res => res.json())
            .then(data => {
                if (data.success) {
                    if (window.MedEquip && window.MedEquip.toast) {
                        window.MedEquip.toast('Thành công', data.message, 'success');
                    } else {
                        alert(data.message);
                    }
                    setTimeout(() => { window.location.reload(); }, 1500);
                } else {
                    if (window.MedEquip && window.MedEquip.toast) {
                        window.MedEquip.toast('Lỗi', data.message || 'Có lỗi xảy ra!', 'danger');
                    } else {
                        alert(data.message || 'Có lỗi xảy ra!');
                    }
                }
            })
            .catch(err => {
                console.error(err);
                if (window.MedEquip && window.MedEquip.toast) {
                    window.MedEquip.toast('Lỗi', 'Lỗi kết nối đến máy chủ!', 'danger');
                } else {
                    alert('Lỗi kết nối đến máy chủ!');
                }
            });
        },

        openAddAssetModal() {
            this.formAddAsset = {
                assetCode: '',
                itemName: '',
                groupId: '',
                serialNumber: '',
                quantity: 1,
                locationId: '',
                departmentId: '',
                importDate: new Date().toISOString().substring(0, 10),
                expiryDate: '',
                warrantyExpiry: '',
                unitPrice: 0,
                depreciationRate: 25,
                depreciationYears: 4,
                note: ''
            };
            this.showAddAssetModal = true;
        },
        saveAddAsset() {
            if (!this.formAddAsset.assetCode) return alert('Vui lòng nhập Mã tài sản!');
            if (!this.formAddAsset.itemName) return alert('Vui lòng nhập Tên thiết bị!');
            if (!this.formAddAsset.groupId) return alert('Vui lòng chọn Nhóm thiết bị!');
            if (!this.formAddAsset.departmentId) return alert('Vui lòng chọn Khoa/Phòng!');
            if (!this.formAddAsset.locationId) return alert('Vui lòng chọn Vị trí lắp đặt!');
            if (!this.formAddAsset.importDate) return alert('Vui lòng chọn ngày nhập kho!');
            if (!this.formAddAsset.unitPrice || this.formAddAsset.unitPrice <= 0) return alert('Vui lòng nhập đơn giá hợp lệ!');

            this.isSavingAsset = true;

            const formData = new FormData();
            formData.append('assetCode', this.formAddAsset.assetCode);
            formData.append('itemName', this.formAddAsset.itemName);
            formData.append('groupId', this.formAddAsset.groupId);
            formData.append('serialNumber', this.formAddAsset.serialNumber || '');
            formData.append('quantity', this.formAddAsset.quantity || 1);
            formData.append('locationId', this.formAddAsset.locationId);
            formData.append('departmentId', this.formAddAsset.departmentId);
            formData.append('importDateStr', this.formAddAsset.importDate);
            formData.append('expiryDateStr', this.formAddAsset.expiryDate || '');
            formData.append('warrantyExpiryStr', this.formAddAsset.warrantyExpiry || '');
            formData.append('unitPrice', this.formAddAsset.unitPrice);
            formData.append('totalPrice', this.formAddAsset.unitPrice * this.formAddAsset.quantity);
            formData.append('depreciationRate', this.formAddAsset.depreciationRate || '');
            formData.append('depreciationYears', this.formAddAsset.depreciationYears || '');
            formData.append('note', this.formAddAsset.note || '');

            fetch('/VongDoiKhauHao/AddAsset', {
                method: 'POST',
                body: formData
            })
            .then(res => res.json())
            .then(data => {
                this.isSavingAsset = false;
                if(data.success) {
                    this.showAddAssetModal = false;
                    if (window.MedEquip && window.MedEquip.toast) {
                        window.MedEquip.toast('Thành công', data.message, 'success');
                    } else {
                        alert(data.message);
                    }
                    setTimeout(() => { window.location.reload(); }, 1500);
                } else {
                    if (window.MedEquip && window.MedEquip.toast) {
                        window.MedEquip.toast('Lỗi', data.message || 'Có lỗi xảy ra!', 'danger');
                    } else {
                        alert(data.message || 'Có lỗi xảy ra!');
                    }
                }
            })
            .catch(err => {
                console.error(err);
                this.isSavingAsset = false;
                if (window.MedEquip && window.MedEquip.toast) {
                    window.MedEquip.toast('Lỗi', 'Lỗi kết nối đến máy chủ!', 'danger');
                } else {
                    alert('Lỗi kết nối đến máy chủ!');
                }
            });
        }
    },
    mounted() {
        const currentYear = new Date().getFullYear();
        for (let i = -10; i <= 10; i++) {
            this.availableYears.push(currentYear + i);
        }
        this.availableYears.sort((a,b) => b - a);
        this.filterYear = '';

        // Tự động lọc thiết bị nếu trỏ từ Alerts Center qua searchCode và inventoryId
        const urlParams = new URLSearchParams(window.location.search);
        const code = urlParams.get('searchCode');
        const invId = urlParams.get('inventoryId');
        if (code) {
            this.searchQuery = code.trim();
        }
        this.$nextTick(() => {
            if (invId) {
                const targetId = parseInt(invId);
                const matched = this.devices.find(d => d.dbId === targetId);
                if (matched) {
                    if (!this.searchQuery) {
                        this.searchQuery = matched.id;
                    }
                    this.openStatusModal(matched);
                }
            } else if (code) {
                const matched = this.devices.find(d => d.id.toLowerCase() === code.trim().toLowerCase());
                if (matched) {
                    this.openStatusModal(matched);
                }
            }
        });

        document.addEventListener('click', this.closeDropdowns);
        window.addEventListener('resize', this.updateTableWidth);
        setTimeout(() => this.updateTableWidth(), 300);
        setTimeout(() => this.renderCharts(), 500);
    },
    beforeDestroy() {
        document.removeEventListener('click', this.closeDropdowns);
        window.removeEventListener('resize', this.updateTableWidth);
    }
});