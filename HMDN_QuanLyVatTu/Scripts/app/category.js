var appCatalog = new Vue({
    el: '#app-catalog',
    delimiters: ['${', '}'],

    data: {

        showItemForm: false,
        isEditItem: false,

        itemForm: {
            GroupId: 0,
            Code: '',
            Name: '',
            Brand: '',
            Model: '',
            Unit: '',
            Description: '',
            ImageUrl: '',
            IsActive: true
        },

        inventoryCurrentPage: 1,
        inventoryPageSize: 15,

        inventories: [],

        groups: [],
        allItems: [],

        //showItemDetail: false,
        //detailItem: {},
        //detailInventories: [],
        //detailLoading: false,

        items: [],
        showItemDetail: false,
        detailItem: {},
        detailInventories: [],
        detailLoading: false,

        currentTab: 'category',
        inventorySearch: '',
        inventories: [],

        activeGroup: null,

        globalSearch: '',
        itemSearch: '',
        itemFilterStatus: '',

        currentPage: 1,
        pageSize: 10,

        showGroupForm: false,
        showDeleteModal: false,

        isEditGroup: false,

        groupForm: { Id: null, Code: '', Name: '', Icon: '', Description: '', SortOrder: 0, IsActive: true },

        deleteTarget: {},
        deleteType: '',

        // Checklist Definitions
        groupSubTab: 'items',
        checklistDefinitions: [],
        showDefinitionForm: false,
        isEditDefinition: false,
        definitionForm: { Id: 0, GroupId: 0, Scope: 'group', ItemId: '', InventoryId: '', CycleType: '', CheckName: '', Description: '', IsRequired: true, SortOrder: 0, IsActive: true },
        definitionFormInventories: [],
        isSavingDefinition: false,
        showDeleteDefModal: false,
        deleteDefTarget: null,
        expandedInventories: {},
        isLockScope: false,

        toast: { show: false, msg: '' }
    },

    computed: {

        filteredGroups() {
            if (!this.globalSearch) return this.groups
            const q = this.globalSearch.toLowerCase()
            return this.groups.filter(g =>
                ((g.Name || '') + ' ' + (g.Code || '')).toLowerCase().includes(q)
            )
        },

        filteredInventories() {

            let s = this.inventorySearch?.toLowerCase() || '';

            return this.inventories.filter(x => {

                return (
                    (x.AssetCode || '').toLowerCase().includes(s) ||
                    (x.ItemName || '').toLowerCase().includes(s) ||
                    (x.LocationName || '').toLowerCase().includes(s)
                );

            });

        },

        groupItems() {
            return this.items || []
        },

        filteredItems() {

            let list = [...this.groupItems]

            if (this.itemSearch) {

                const q = this.itemSearch
                    .toLowerCase()
                    .trim()

                list = list.filter(x => {

                    const text = `
                ${x.Name || ''}
                ${x.Code || ''}
                ${x.Brand || ''}
                ${x.Model || ''}
                ${x.Unit || ''}
            `
                        .toLowerCase()

                    return text.includes(q)
                })
            }

            if (this.itemFilterStatus !== '') {

                const active = this.itemFilterStatus === '1'

                list = list.filter(x => x.IsActive === active)
            }

            return list
        },

        paginatedItems() {
            const start = (this.currentPage - 1) * this.pageSize
            return this.filteredItems.slice(start, start + this.pageSize)
        },

        totalPages() {
            return Math.max(1, Math.ceil(this.filteredItems.length / this.pageSize))
        },

        inventoryTotalPages() {
            return Math.ceil(this.filteredInventories.length / this.inventoryPageSize) || 1
        },

        inventoryPaginated() {

            const start = (this.inventoryCurrentPage - 1) * this.inventoryPageSize
            const end = start + this.inventoryPageSize

            return this.filteredInventories.slice(start, end)
        },

        inventoryPages() {

            let arr = []

            for (let i = 1; i <= this.inventoryTotalPages; i++) {
                arr.push(i)
            }

            return arr
        },

        inventoryPaginationInfo() {

            if (this.filteredInventories.length === 0)
                return '0'

            const start = (this.inventoryCurrentPage - 1) * this.inventoryPageSize + 1

            const end = Math.min(
                this.inventoryCurrentPage * this.inventoryPageSize,
                this.filteredInventories.length
            )

            return `${start}-${end} / ${this.filteredInventories.length}`
        },

        pages() {
            return Array.from({ length: this.totalPages }, (_, i) => i + 1)
        },

        paginationInfo() {
            const start = (this.currentPage - 1) * this.pageSize + 1
            const end = Math.min(this.currentPage * this.pageSize, this.filteredItems.length)
            return this.filteredItems.length === 0
                ? '0'
                : start + '-' + end + ' của ' + this.filteredItems.length
        }
    },

    methods: {

        getLifeStatusMeta(status) {

            switch (status) {

                case "active":
                    return {
                        text: "Đang hoạt động",
                        bg: "#dcfce7",
                        color: "#16a34a",
                        icon: "🟢"
                    }

                case "suspended":
                    return {
                        text: "Tạm ngưng",
                        bg: "#f1f5f9",
                        color: "#64748b",
                        icon: "⏸️"
                    }

                case "maintenance_bv":
                    return {
                        text: "BV bảo trì",
                        bg: "#ffedd5",
                        color: "#ea580c",
                        icon: "🛠️"
                    }

                case "maintenance_hang":
                    return {
                        text: "Hãng bảo hành",
                        bg: "#ede9fe",
                        color: "#7c3aed",
                        icon: "🏭"
                    }

                default:
                    return {
                        text: "Không xác định",
                        bg: "#fee2e2",
                        color: "#dc2626",
                        icon: "❓"
                    }
            }
        },

        itemCountOf(groupId) {
            return this.allItems.filter(x => x.GroupId === groupId).length
        },

        //showToast(msg) {
        //    this.toast = { show: true, msg }
        //    setTimeout(() => { this.toast.show = false }, 2800)
        //},
        showToast(msg) {

            clearTimeout(this.toastTimer)

            this.toast = {
                show: true,
                msg
            }

            this.toastTimer = setTimeout(() => {

                this.toast.show = false

            }, 2800)
        },

        formatDate(date) {

            if (!date) return '—';

            const d = new Date(date);

            return d.toLocaleDateString('vi-VN');
        },

        formatMoney(value) {

            if (value == null) return '—';

            return Number(value).toLocaleString('vi-VN') + ' đ';
        },

        openDetailItem(item) {

            this.detailItem = item;

            this.showItemDetail = true;

            this.detailLoading = true;

            this.detailInventories = [];

            $.ajax({

                url: `/api/category/item-inventories/${item.Id}`,

                type: 'GET',

                success: (res) => {

                    this.detailInventories = res || [];

                },

                error: () => {

                    this.showToast('Không tải được dữ liệu');

                },

                complete: () => {

                    this.detailLoading = false;

                }

            });

        },

        closeItemDetail() {

            this.showItemDetail = false;
            this.detailInventories = [];
            this.detailItem = {};

        },

        openAddInventoryDefinition(inv) {
            this.isLockScope = true;
            this.isEditDefinition = false;
            this.definitionFormInventories = [];
            this.definitionForm = {
                Id: 0,
                GroupId: this.activeGroup.Id,
                Scope: 'inventory',
                ItemId: this.detailItem.Id,
                InventoryId: inv.Id,
                CycleType: '',
                CheckName: '',
                Description: '',
                IsRequired: true,
                SortOrder: this.checklistDefinitions.filter(d => d.Scope === 'inventory').length + 1,
                IsActive: true
            };
            this.loadItemInventories(this.detailItem.Id);
            this.showDefinitionForm = true;
        },

        // ── LOAD ──
        loadGroups() {

            $.ajax({

                url: '/api/category/groups',

                type: 'GET',

                success: (res) => {

                    this.groups = res

                    if (this.groups.length && !this.activeGroup) {

                        this.selectGroup(this.groups[0])
                    }
                },

                error: () => {

                    this.showToast('❌ Load nhóm thất bại')
                }
            })
        },

        //selectGroup(group) {

        //    this.activeGroup = group;

        //    this.loadItems(group.Id);
        //},

        loadItems(groupId) {

            const vm = this;

            $.ajax({

                url: '/api/category/items/' + groupId,
                type: 'GET',

                success: function (res) {

                    vm.items = res;

                    console.log(res);
                },

                error: function (err) {

                    console.log(err);
                }
            });
        },

        //loadAllItems() {
        //    $.ajax({
        //        url: '/api/categoryapi/items', type: 'GET',
        //        success: res => { this.allItems = res },
        //        error: () => { }
        //    })
        //},

        //selectGroup(g) {
        //    this.activeGroup = g
        //    this.itemSearch = ''
        //    this.itemFilterStatus = ''
        //    this.currentPage = 1
        //},
        selectGroup(group) {

            this.activeGroup = group;

            this.itemSearch = '';
            this.itemFilterStatus = '';
            this.currentPage = 1;
            this.groupSubTab = 'items';

            this.loadItems(group.Id);
            this.loadChecklistDefinitions(group.Id);
        },

        // ── PAGINATION ──
        changePage(p) {
            if (p < 1 || p > this.totalPages) return
            this.currentPage = p
        },
        nextPage() { if (this.currentPage < this.totalPages) this.currentPage++ },
        prevPage() { if (this.currentPage > 1) this.currentPage-- },

        // ── TOGGLE STATUS ──
        toggleItemStatus(item) {

            $.ajax({

                url: '/api/category/item/toggle',

                type: 'PUT',

                contentType: 'application/json',

                data: JSON.stringify({
                    Id: item.Id,
                    IsActive: item.IsActive
                }),

                success: () => {

                    this.showToast(
                        item.IsActive
                            ? '✅ Đã bật!'
                            : '⏸️ Đã tắt!'
                    );
                },

                error: () => {

                    item.IsActive = !item.IsActive;

                    this.showToast(
                        '❌ Không cập nhật được!'
                    );
                }
            });
        },

        // ── GROUP CRUD ──
        openAddGroup() {
            this.isEditGroup = false
            this.groupForm = { Id: null, Code: '', Name: '', Icon: '', Description: '', SortOrder: this.groups.length + 1, IsActive: true }
            this.showGroupForm = true
        },

        openEditGroup(g) {
            this.isEditGroup = true
            this.groupForm = {
                Id: g.Id,
                Code: g.Code,
                Name: g.Name,
                Icon: g.Icon,
                Description: g.Description,
                SortOrder: g.SortOrder,
                IsActive: g.IsActive
            }
            this.showGroupForm = true
        },

        saveGroup() {

            //    if (!this.groupForm.Code.trim()
            //        || !this.groupForm.Name.trim()) {

            //        this.showToast(
            //            '⚠️ Mã và tên nhóm không được trống!'
            //        )

            //        return
            //    }

            //    const url = this.isEditGroup
            //        ? '/api/category/group/update'
            //        : '/api/category/group/create'

            //    const type = this.isEditGroup
            //        ? 'PUT'
            //        : 'POST'

            //    $.ajax({

            //        url: url,

            //        type: type,

            //        contentType: 'application/json',

            //        data: JSON.stringify(this.groupForm),

            //        success: (res) => {

            //            this.showGroupForm = false

            //            this.loadGroups()

            //            this.showToast(
            //                this.isEditGroup
            //                    ? '✅ Đã cập nhật nhóm!'
            //                    : '✅ Đã thêm nhóm mới!'
            //            )
            //        },

            //        error: (xhr) => {

            //            console.log(xhr)

            //            this.showToast(
            //                xhr.responseText || '❌ Có lỗi xảy ra!'
            //            )
            //        }
            //    })
            //},

            if (this.isEditGroup) {

                $.ajax({
                    url: '/api/category/group/update',
                    type: 'PUT',
                    contentType: 'application/json',

                    data: JSON.stringify(this.groupForm),

                    success: (res) => {

                        const index = this.groups.findIndex(
                            x => x.Id === this.groupForm.Id
                        )

                        if (index !== -1) {

                            this.groups.splice(index, 1, {
                                ...this.groups[index],
                                ...this.groupForm
                            })

                            // update activeGroup realtime
                            if (this.activeGroup &&
                                this.activeGroup.Id === this.groupForm.Id) {

                                this.activeGroup = this.groups[index]
                            }
                        }

                        this.showGroupForm = false

                        this.showToast(res.message)
                    },

                    error: (err) => {

                        console.log(err)

                        this.showToast('Cập nhật thất bại')
                    }
                })

                return
            }
        },

        openDeleteGroup(g) {
            this.deleteTarget = g
            this.deleteType = 'group'
            this.showDeleteModal = true
        },

        // ── ITEM CRUD ──
        openAddItem() {
            this.isEditItem = false
            this.itemForm = { Id: null, GroupId: this.activeGroup.Id, Code: '', Name: '', Brand: '', Model: '', Unit: 'Bộ', Description: '', IsActive: true }
            this.showItemForm = true
        },

        openEditItem(item) {
            this.isEditItem = true
            this.itemForm = { ...item }
            this.showItemForm = true
        },

        saveItem() {

            if (!this.itemForm.Code ||
                !this.itemForm.Name ||
                !this.itemForm.Unit) {

                this.showToast("Vui lòng nhập đủ thông tin");
                return;
            }

            $.ajax({
                url: '/api/category/item/create',
                type: 'POST',
                contentType: 'application/json',

                data: JSON.stringify(this.itemForm),

                success: (res) => {

                    this.showToast(res.message);

                    this.showItemForm = false;

                    this.loadItems(this.activeGroup.Id);
                },

                error: (err) => {

                    console.log(err);

                    this.showToast("Có lỗi xảy ra");
                }
            });
        },

        openDeleteItem(item) {
            this.deleteTarget = item
            this.deleteType = 'item'
            this.showDeleteModal = true
        },

        // ── DELETE ──
        confirmDelete() {
            const isGroup = this.deleteType === 'group'
            const url = isGroup
                ? '/api/category/group/delete?id=' + this.deleteTarget.Id
                : '/api/category/item/delete?id=' + this.deleteTarget.Id

            $.ajax({
                url, type: 'DELETE',
                success: () => {
                    this.showDeleteModal = false
                    if (isGroup) {
                        if (this.activeGroup && this.activeGroup.Id === this.deleteTarget.Id)
                            this.activeGroup = null
                        this.loadGroups()
                        this.loadAllItems()
                    } else {
                        this.allItems = this.allItems.filter(x => x.Id !== this.deleteTarget.Id)
                    }
                    this.showToast('🗑️ Đã xoá!')
                },
                error: () => this.showToast('❌ Không thể xoá!')
            })
        },

        changeInventoryPage(page) {

            if (page < 1 || page > this.inventoryTotalPages)
                return

            this.inventoryCurrentPage = page
        },

        nextInventoryPage() {

            if (this.inventoryCurrentPage < this.inventoryTotalPages) {
                this.inventoryCurrentPage++
            }
        },

        prevInventoryPage() {

            if (this.inventoryCurrentPage > 1) {
                this.inventoryCurrentPage--
            }
        },

        // Load inventory
        loadInventories() {

            $.ajax({
                url: '/api/category/inventories',
                method: 'GET',

                success: (res) => {
                    this.inventories = res;
                },

                error: () => {
                    this.showToast('Không tải được tài sản');
                }
            });

        },

        // ── CHECKLIST DEFINITIONS METHODS ──
        loadChecklistDefinitions(groupId) {
            if (!groupId) return;
            $.ajax({
                url: '/api/category/checklist-definitions/' + groupId,
                type: 'GET',
                success: (res) => {
                    this.checklistDefinitions = res || [];
                },
                error: () => {
                    this.showToast('❌ Tải hạng mục checklist thất bại');
                }
            });
        },

        openAddDefinition() {
            this.isLockScope = false;
            this.isEditDefinition = false;
            this.definitionFormInventories = [];
            this.definitionForm = {
                Id: 0,
                GroupId: this.activeGroup.Id,
                Scope: 'group',
                ItemId: '',
                InventoryId: '',
                CycleType: '',
                CheckName: '',
                Description: '',
                IsRequired: true,
                SortOrder: this.checklistDefinitions.filter(d => d.Scope === 'group').length + 1,
                IsActive: true
            };
            this.showDefinitionForm = true;
        },

        openEditDefinition(def) {
            if (def.Scope === 'global') return;
            this.isLockScope = true;
            this.isEditDefinition = true;
            this.definitionFormInventories = [];
            this.definitionForm = {
                Id: def.Id,
                GroupId: def.GroupId,
                Scope: def.Scope || 'group',
                ItemId: (def.ItemId !== null && def.ItemId !== undefined) ? def.ItemId : '',
                InventoryId: (def.InventoryId !== null && def.InventoryId !== undefined) ? def.InventoryId : '',
                CycleType: def.CycleType || '',
                CheckName: def.CheckName,
                Description: def.Description || '',
                IsRequired: def.IsRequired,
                SortOrder: def.SortOrder,
                IsActive: def.IsActive
            };
            if (def.ItemId) {
                this.loadItemInventories(def.ItemId);
            }
            this.showDefinitionForm = true;
        },

        saveDefinition() {
            if (!this.definitionForm.CheckName.trim()) {
                this.showToast('⚠️ Tên hạng mục không được để trống!');
                return;
            }
            if (this.definitionForm.Scope === 'item' && !this.definitionForm.ItemId) {
                this.showToast('⚠️ Vui lòng chọn loại thiết bị cụ thể!');
                return;
            }
            if (this.definitionForm.Scope === 'inventory') {
                if (!this.definitionForm.ItemId) {
                    this.showToast('⚠️ Vui lòng chọn loại thiết bị trước!');
                    return;
                }
                if (!this.definitionForm.InventoryId) {
                    this.showToast('⚠️ Vui lòng chọn thiết bị/tài sản cụ thể!');
                    return;
                }
            }
            this.isSavingDefinition = true;
            $.ajax({
                url: '/api/category/checklist-definition/save',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(this.definitionForm),
                success: (res) => {
                    this.showDefinitionForm = false;
                    this.loadChecklistDefinitions(this.activeGroup.Id);
                    this.showToast(this.isEditDefinition ? '✅ Cập nhật hạng mục thành công!' : '✅ Thêm hạng mục thành công!');
                },
                error: (err) => {
                    this.showToast(err.responseJSON?.message || '❌ Lỗi khi lưu hạng mục!');
                },
                complete: () => {
                    this.isSavingDefinition = false;
                }
            });
        },

        toggleDefinitionStatus(def) {
            $.ajax({
                url: '/api/category/checklist-definition/toggle/' + def.Id,
                type: 'PUT',
                success: (res) => {
                    if (res.success) {
                        this.showToast(res.message);
                    }
                },
                error: () => {
                    def.IsActive = !def.IsActive; // rollback
                    this.showToast('❌ Cập nhật trạng thái thất bại!');
                }
            });
        },

        openDeleteDefinition(def) {
            if (def.Scope === 'global') return;
            this.deleteDefTarget = def;
            this.showDeleteDefModal = true;
        },

        confirmDeleteDefinition() {
            if (!this.deleteDefTarget) return;
            $.ajax({
                url: '/api/category/checklist-definition/' + this.deleteDefTarget.Id,
                type: 'DELETE',
                success: (res) => {
                    this.showDeleteDefModal = false;
                    if (res.success) {
                        this.showToast(res.message || '🗑️ Đã xử lý thành công!');
                        this.loadChecklistDefinitions(this.activeGroup.Id);
                    } else {
                        this.showToast('❌ ' + (res.message || 'Không thể xóa!'));
                    }
                },
                error: (xhr) => {
                    this.showDeleteDefModal = false;
                    this.showToast('❌ ' + (xhr.responseJSON?.message || 'Không thể xóa!'));
                }
            });
        },

        disableDefinition(id) {
            $.ajax({
                url: '/api/category/checklist-definition/toggle/' + id,
                type: 'PUT',
                success: (res) => {
                    if (res.success) {
                        this.showToast('⏸️ Đã vô hiệu hóa hạng mục!');
                        this.loadChecklistDefinitions(this.activeGroup.Id);
                    }
                },
                error: () => {
                    this.showToast('❌ Không thể vô hiệu hóa!');
                }
            });
        },

        getItemName(itemId) {
            if (!itemId) return '';
            const item = this.items.find(x => x.Id === itemId);
            return item ? item.Name : ('ID: ' + itemId);
        },

        onDefinitionScopeChange() {
            this.definitionForm.ItemId = '';
            this.definitionForm.InventoryId = '';
            this.definitionFormInventories = [];
        },

        onDefinitionItemChange() {
            this.definitionForm.InventoryId = '';
            this.definitionFormInventories = [];
            if (this.definitionForm.ItemId) {
                this.loadItemInventories(this.definitionForm.ItemId);
            }
        },

        loadItemInventories(itemId) {
            if (!itemId) return;
            $.ajax({
                url: '/api/category/item-inventories/' + itemId,
                type: 'GET',
                success: (res) => {
                    this.definitionFormInventories = res || [];
                }
            });
        },

        getInventoryLabel(def) {
            if (def.Scope === 'item') {
                return 'Mẫu: ' + this.getItemName(def.ItemId);
            }
            if (def.Scope === 'inventory') {
                return 'Mã TS: ' + (def.InventoryId ? this.getInventoryAssetCode(def.InventoryId) : 'Không xác định');
            }
            return 'Cả nhóm';
        },

        getInventoryAssetCode(inventoryId) {
            const inv = this.inventories.find(x => x.Id === inventoryId);
            return inv ? inv.AssetCode : ('ID: ' + inventoryId);
        },

        toggleExpandInventory(invId) {
            this.$set(this.expandedInventories, invId, !this.expandedInventories[invId]);
        }
    },

    watch: {

        inventorySearch() {
            this.inventoryCurrentPage = 1
        },
        itemSearch() { this.currentPage = 1 },
        itemFilterStatus() { this.currentPage = 1 },
            currentTab(val) {

            if (val === 'inventory') {

                this.loadInventories()
            }
        }

    },

    mounted() {
        this.loadGroups()
        this.loadInventories();
    }
})
