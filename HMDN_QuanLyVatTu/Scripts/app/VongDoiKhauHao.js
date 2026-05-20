new Vue({
    el: '#app',
    data: {
        searchQuery: '',
        filterStatus: '',
        filterYear: '2024',
        activeDropdown: null,

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
        filteredDevices() {
            let list = this.devices;
            if(this.filterStatus) list = list.filter(d => d.status === this.filterStatus);
            if(this.searchQuery) {
                const q = this.searchQuery.toLowerCase();
                list = list.filter(d => d.id.toLowerCase().includes(q) || d.name.toLowerCase().includes(q));
            }
            return list;
        },
        totalClosingValue() {
            return this.devices.reduce((sum, d) => sum + (d.closingValue || 0), 0);
        }
    },
    methods: {
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
        document.addEventListener('click', this.closeDropdowns);
    },
    beforeDestroy() {
        document.removeEventListener('click', this.closeDropdowns);
    }
});