new Vue({
    el: '#app',
    data: {
        searchQuery: '',
        filterStatus: '',
        filterYear: '2024',
        availableYears: [],
        activeDropdown: null,
        
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
            return this.processedDevices.reduce((sum, d) => sum + (d.closingValue || 0), 0);
        }
    },
    watch: {
        processedDevices() { 
            this.currentPage = 1; 
        },
        paginatedDevices() {
            this.$nextTick(() => {
                this.updateTableWidth();
            });
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
            let csvContent = "data:text/csv;charset=utf-8,\uFEFF";
            csvContent += "Mã tài sản,Tên thiết bị,Ngày nhập,Hạn BH,Hạn SD,Trạng thái,Nguyên giá,Khấu hao lũy kế,Giá trị còn lại,Thay thế bởi\n";
            this.processedDevices.forEach(d => {
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
            link.setAttribute("download", "DanhSachKhauHao_" + this.filterYear + ".csv");
            document.body.appendChild(link);
            link.click();
            document.body.removeChild(link);
        },
        countStatus(st) { return this.devices.filter(d => d.status === st).length; },
        statusText(st) {
            return { active:'Đang sử dụng', suspended:'Tạm ngưng', disposed:'Đã thanh lý', replaced:'Đã thay mới' }[st] || st;
        },
        formatMoney(val) {
            return new Intl.NumberFormat('vi-VN').format(val);
        },
        toggleDropdown(id) {
            this.activeDropdown = this.activeDropdown === id ? null : id;
        },
        closeDropdowns() { this.activeDropdown = null; },

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
            if(!this.selectedDevice) return;
            
            const formData = new FormData();
            formData.append('id', this.selectedDevice.dbId);
            formData.append('status', this.formStatus);
            formData.append('replacedBy', this.formReplacedBy || '');
            formData.append('reason', this.formReason || '');

            fetch('/VongDoiKhauHao/UpdateStatus', {
                method: 'POST',
                body: formData
            })
            .then(res => res.json())
            .then(data => {
                if(data.success) {
                    this.selectedDevice.status = this.formStatus;
                    this.selectedDevice.replacedBy = this.formStatus === 'replaced' ? this.formReplacedBy : null;
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
            const formData = new FormData();
            formData.append('calculateYear', this.filterYear);

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
            formData.append('depreciationRate', '');
            formData.append('depreciationYears', '');
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
        for (let i = 0; i < 10; i++) {
            this.availableYears.push(currentYear - i);
        }
        this.filterYear = currentYear.toString();

        document.addEventListener('click', this.closeDropdowns);
        window.addEventListener('resize', this.updateTableWidth);
        setTimeout(() => this.updateTableWidth(), 300);
    },
    beforeDestroy() {
        document.removeEventListener('click', this.closeDropdowns);
        window.removeEventListener('resize', this.updateTableWidth);
    }
});