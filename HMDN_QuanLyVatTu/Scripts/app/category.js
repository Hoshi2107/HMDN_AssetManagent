var appCatalog = new Vue({
    el: '#app-catalog',
    delimiters: ['${', '}'],

    data: {
        // ── LOCATION (Khoa phòng) ──
        locDepartments: [],
        locActiveDept: null,
        locSearch: '',
        locAssetSearch: '',
        locInventories: [],
        locCurrentPage: 1,
        locPageSize: 10,

        showLocForm: false,
        isEditLoc: false,
        locForm: { Id: null, Code: '', Name: '', Description: '' },

        showLocStatusModal: false,
        locStatusTarget: null,

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
        expandedInventoryMap: {},
        inventoryChecklistCache: {},
        detailAssetSearch: '',
        editingChecklistId: null,
        inlineAddForm: {},
        loadingChecklist: {},
        savingChecklist: {},
        deletingChecklist: {},

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
        itemDropdownSearch: '',
        inventoryDropdownSearch: '',
        showItemDropdown: false,
        showInventoryDropdown: false,
        defListSearch: '',
        defListScope: 'all',
        defListFilterItemId: '',
        showToolbarItemDropdown: false,
        toolbarItemDropdownSearch: '',

        // ── GROUPED ITEMS (tránh duplicate tên) ──
        expandedGroupedItemNames: {}, // { [normalizedName]: true/false }

        toast: { show: false, msg: '' }
    },

    computed: {

        filteredLocDepartments() {
            if (!this.locSearch) return this.locDepartments
            const q = this.locSearch.toLowerCase()
            return this.locDepartments.filter(d =>
                ((d.Name || '') + ' ' + (d.Code || '')).toLowerCase().includes(q)
            )
        },

        filteredLocInventories() {
            const s = (this.locAssetSearch || '').toLowerCase()
            return this.locInventories.filter(x =>
                (x.AssetCode || '').toLowerCase().includes(s) ||
                (x.ItemName || '').toLowerCase().includes(s) ||
                (x.GroupName || '').toLowerCase().includes(s)
            )
        },

        locTotalPages() {
            return Math.max(1, Math.ceil(this.filteredLocInventories.length / this.locPageSize))
        },

        locPaginatedInventories() {
            const start = (this.locCurrentPage - 1) * this.locPageSize
            return this.filteredLocInventories.slice(start, start + this.locPageSize)
        },

        locPages() {
            return Array.from({ length: this.locTotalPages }, (_, i) => i + 1)
        },

        locPaginationInfo() {
            if (this.filteredLocInventories.length === 0) return '0'
            const start = (this.locCurrentPage - 1) * this.locPageSize + 1
            const end = Math.min(this.locCurrentPage * this.locPageSize, this.filteredLocInventories.length)
            return `${start}-${end} / ${this.filteredLocInventories.length}`
        },

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

        filteredDropdownItems() {
            var q = (this.itemDropdownSearch || '').trim().toLowerCase();
            var uniqueItems = [];
            var seenNames = new Set();
            (this.items || []).forEach(it => {
                var name = (it.Name || '').trim();
                if (!seenNames.has(name)) {
                    seenNames.add(name);
                    uniqueItems.push(it);
                }
            });
            if (!q) return uniqueItems;
            return uniqueItems.filter(it => (it.Name || '').toLowerCase().includes(q));
        },

        filteredDropdownInventories() {
            var q = (this.inventoryDropdownSearch || '').trim().toLowerCase();
            if (!q) return this.definitionFormInventories || [];
            return (this.definitionFormInventories || []).filter(inv => 
                (inv.AssetCode || '').toLowerCase().includes(q) ||
                (inv.LocationName || '').toLowerCase().includes(q) ||
                (inv.SerialNumber || '').toLowerCase().includes(q)
            );
        },

        filteredChecklistDefinitions() {
            var scope = this.defListScope || 'all';
            var q = (this.defListSearch || '').trim().toLowerCase();
            var filterItemId = this.defListFilterItemId;

            return (this.checklistDefinitions || []).filter(def => {
                if (scope !== 'all' && def.Scope !== scope) {
                    return false;
                }
                if (filterItemId) {
                    var isRelevant = false;
                    if (def.Scope === 'global' || def.Scope === 'group') {
                        isRelevant = true;
                    } else if (def.Scope === 'item' && def.ItemId === filterItemId) {
                        isRelevant = true;
                    } else if (def.Scope === 'inventory') {
                        var inv = (this.inventories || []).find(x => x.Id === def.InventoryId);
                        if (inv && inv.ItemId === filterItemId) {
                            isRelevant = true;
                        }
                    }
                    if (!isRelevant) return false;
                }
                if (q) {
                    var matchName = (def.CheckName || '').toLowerCase().includes(q);
                    var matchDesc = (def.Description || '').toLowerCase().includes(q);
                    var matchScopeLabel = false;
                    if (def.Scope === 'item') {
                        var itemName = this.getItemName(def.ItemId) || '';
                        matchScopeLabel = itemName.toLowerCase().includes(q);
                    } else if (def.Scope === 'inventory') {
                        var assetCode = this.getInventoryAssetCode(def.InventoryId) || '';
                        matchScopeLabel = assetCode.toLowerCase().includes(q);
                    }
                    return matchName || matchDesc || matchScopeLabel;
                }
                return true;
            });
        },

        filteredToolbarDropdownItems() {
            var list = this.items || [];
            var q = (this.toolbarItemDropdownSearch || '').trim().toLowerCase();
            if (q) {
                list = list.filter(x => 
                    (x.Name || '').toLowerCase().includes(q) || 
                    (x.Code || '').toLowerCase().includes(q)
                );
            }
            return list;
        },

        groupInventories() {
            var itemIds = (this.items || []).map(x => x.Id);
            return (this.inventories || []).filter(inv => itemIds.indexOf(inv.ItemId) > -1);
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

        // Group items cùng tên lại để tránh duplicate
        groupedFilteredItems() {
            const list = this.filteredItems;
            const map = {}; // key = normalized name
            const order = [];

            list.forEach(item => {
                const key = (item.Name || '').trim().toLowerCase();
                if (!map[key]) {
                    map[key] = {
                        _key: key,
                        _representative: item, // item đầu tiên đại diện
                        _variants: [],
                    };
                    order.push(key);
                }
                map[key]._variants.push(item);
            });

            return order.map(k => map[k]);
        },

        paginatedGroupedItems() {
            const start = (this.currentPage - 1) * this.pageSize;
            return this.groupedFilteredItems.slice(start, start + this.pageSize);
        },

        // Kept for backward compat (checklist uses filteredItems)
        paginatedItems() {
            const start = (this.currentPage - 1) * this.pageSize
            return this.filteredItems.slice(start, start + this.pageSize)
        },

        totalPages() {
            return Math.max(1, Math.ceil(this.groupedFilteredItems.length / this.pageSize))
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

            const total = this.inventoryTotalPages
            const current = this.inventoryCurrentPage
            const delta = 1 // số trang hiện quanh trang hiện tại

            const range = []
            const rangeWithDots = []
            let l

            for (let i = 1; i <= total; i++) {
                if (i === 1 || i === total || (i >= current - delta && i <= current + delta)) {
                    range.push(i)
                }
            }

            range.forEach((i) => {
                if (l) {
                    if (i - l === 2) {
                        rangeWithDots.push(l + 1)
                    } else if (i - l !== 1) {
                        rangeWithDots.push('...')
                    }
                }
                rangeWithDots.push(i)
                l = i
            })

            return rangeWithDots
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
            const total = this.groupedFilteredItems.length;
            if (total === 0) return '0';
            const start = (this.currentPage - 1) * this.pageSize + 1;
            const end = Math.min(this.currentPage * this.pageSize, total);
            return start + '-' + end + ' của ' + total;
        },

        filteredDetailInventories() {
            if (!this.detailAssetSearch) return this.detailInventories;
            const q = this.detailAssetSearch.toLowerCase().trim();
            return this.detailInventories.filter(x => 
                (x.AssetCode || '').toLowerCase().includes(q) ||
                (x.SerialNumber || '').toLowerCase().includes(q) ||
                (x.LocationName || '').toLowerCase().includes(q)
            );
        }
    },

    methods: {

        // ── LOCATION (Khoa phòng) ──
        loadLocDepartments() {
            $.ajax({
                url: '/api/department/list',
                type: 'GET',
                success: (res) => { this.locDepartments = res },
                error: () => { this.showToast('❌ Load khoa phòng thất bại') }
            })
        },

        selectLocDept(d) {
            this.locActiveDept = d
            this.locAssetSearch = ''
            this.locCurrentPage = 1
            this.loadLocInventories(d.Id)
        },

        loadLocInventories(deptId) {
            $.ajax({
                url: '/api/department/inventory?id=' + deptId,
                type: 'GET',
                success: (res) => {
                    this.locInventories = (res || []).filter(x => x.InventoryId)
                },
                error: () => {
                    // Không có tài sản -> API trả 404, coi như rỗng
                    this.locInventories = []
                }
            })
        },

        openAddLoc() {
            this.isEditLoc = false
            this.locForm = { Id: null, Code: '', Name: '', Description: '' }
            this.showLocForm = true
        },

        openEditLoc(d) {
            this.isEditLoc = true
            this.locForm = { Id: d.Id, Code: d.Code, Name: d.Name, Description: d.Description }
            this.showLocForm = true
        },

        saveLoc() {
            if (!this.locForm.Code.trim() || !this.locForm.Name.trim()) {
                this.showToast('⚠️ Vui lòng nhập mã và tên khoa phòng!')
                return
            }

            const url = this.isEditLoc ? '/api/department/update' : '/api/department/create'
            const type = this.isEditLoc ? 'PUT' : 'POST'

            $.ajax({
                url, type,
                contentType: 'application/json',
                data: JSON.stringify(this.locForm),
                success: (res) => {
                    this.showLocForm = false
                    this.loadLocDepartments()
                    this.showToast(res.message || (this.isEditLoc ? '✅ Đã cập nhật!' : '✅ Đã thêm khoa phòng!'))
                },
                error: (xhr) => {
                    this.showToast(xhr.responseText || '❌ Có lỗi xảy ra!')
                }
            })
        },

        openToggleLocStatus(d) {
            this.locStatusTarget = d
            this.showLocStatusModal = true
        },

        confirmToggleLocStatus() {
            if (!this.locStatusTarget) return

            $.ajax({
                url: '/api/department/togglestatus?id=' + this.locStatusTarget.Id,
                type: 'POST',
                success: () => {
                    this.locStatusTarget.IsActive = !this.locStatusTarget.IsActive
                    if (this.locActiveDept && this.locActiveDept.Id === this.locStatusTarget.Id) {
                        this.locActiveDept.IsActive = this.locStatusTarget.IsActive
                    }
                    this.showToast('✅ Cập nhật trạng thái thành công')
                    this.showLocStatusModal = false
                    this.locStatusTarget = null
                },
                error: () => {
                    this.showToast('❌ Cập nhật thất bại')
                }
            })
        },

        // ── LOCATION PAGINATION ──
        locChangePage(p) {
            if (p < 1 || p > this.locTotalPages) return
            this.locCurrentPage = p
        },
        locNextPage() { if (this.locCurrentPage < this.locTotalPages) this.locCurrentPage++ },
        locPrevPage() { if (this.locCurrentPage > 1) this.locCurrentPage-- },

        normalizeNullableIntegers(obj, fields) {
            fields.forEach(field => {
                if (obj[field] === '') {
                    obj[field] = null;
                }
            });
        },

        selectToolbarDropdownItem(item) {
            this.defListFilterItemId = item.Id || '';
            this.showToolbarItemDropdown = false;
            this.toolbarItemDropdownSearch = '';
        },

        getToolbarItemName(itemId) {
            if (!itemId) return '-- Chọn loại thiết bị --';
            var it = (this.items || []).find(x => x.Id === itemId);
            return it ? (it.Name + ' (' + it.Code + ')') : '-- Chọn loại thiết bị --';
        },

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
            this.expandedInventoryMap = {};
            this.inventoryChecklistCache = {};
            this.detailAssetSearch = '';
            this.editingChecklistId = null;
            this.inlineAddForm = {};
            this.loadingChecklist = {};
            this.savingChecklist = {};
            this.deletingChecklist = {};

            $.ajax({
                url: `/api/category/item-inventories/${item.Id}`,
                type: 'GET',
                success: (res) => {
                    console.log('detailInventories response:', res);
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
            this.expandedInventoryMap = {};
            this.inventoryChecklistCache = {};
            this.detailAssetSearch = '';
            this.editingChecklistId = null;
            this.inlineAddForm = {};
            this.loadingChecklist = {};
            this.savingChecklist = {};
            this.deletingChecklist = {};
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
            this.expandedGroupedItemNames = {}; // reset expand state

            this.loadItems(group.Id);
            this.loadChecklistDefinitions(group.Id);
        },

        // Toggle expand/collapse của nhóm items cùng tên
        toggleGroupedItem(key) {
            var current = this.expandedGroupedItemNames[key];
            this.$set(this.expandedGroupedItemNames, key, !current);
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
            this.itemDropdownSearch = '';
            this.inventoryDropdownSearch = '';
            this.showItemDropdown = false;
            this.showInventoryDropdown = false;
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
                IsActive: true,
                ValueType: 'checkbox',
                Unit: '',
                MinVal: null,
                MaxVal: null,
                DefaultVal: null,
                OptionsText: ''
            };
            this.showDefinitionForm = true;
        },

        openEditDefinition(def) {
            if (def.Scope === 'global') return;
            this.isLockScope = true;
            this.isEditDefinition = true;
            this.itemDropdownSearch = '';
            this.inventoryDropdownSearch = '';
            this.showItemDropdown = false;
            this.showInventoryDropdown = false;
            this.definitionFormInventories = [];

            let minVal = null;
            let maxVal = null;
            let defaultVal = null;
            if (def.ValidationRules) {
                try {
                    let rules = typeof def.ValidationRules === 'string' ? JSON.parse(def.ValidationRules) : def.ValidationRules;
                    if (rules) {
                        minVal = rules.min !== undefined ? rules.min : null;
                        maxVal = rules.max !== undefined ? rules.max : null;
                        defaultVal = rules.defaultValue !== undefined ? rules.defaultValue : null;
                    }
                } catch (e) {
                    console.error(e);
                }
            }

            let optionsText = '';
            if (def.Options && def.Options.length > 0) {
                optionsText = def.Options.map(o => (o.IsDefault ? '*' : '') + o.DisplayText).join('\n');
            }

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
                IsActive: def.IsActive,
                ValueType: def.ValueType || 'checkbox',
                Unit: def.Unit || '',
                MinVal: minVal,
                MaxVal: maxVal,
                DefaultVal: defaultVal,
                OptionsText: optionsText
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
            let rules = {};
            if (this.definitionForm.ValueType === 'number') {
                if (this.definitionForm.MinVal !== null && this.definitionForm.MinVal !== undefined && this.definitionForm.MinVal !== '') {
                    rules.min = parseFloat(this.definitionForm.MinVal);
                }
                if (this.definitionForm.MaxVal !== null && this.definitionForm.MaxVal !== undefined && this.definitionForm.MaxVal !== '') {
                    rules.max = parseFloat(this.definitionForm.MaxVal);
                }
                if (this.definitionForm.DefaultVal !== null && this.definitionForm.DefaultVal !== undefined && this.definitionForm.DefaultVal !== '') {
                    rules.defaultValue = parseFloat(this.definitionForm.DefaultVal);
                }
            }
            this.definitionForm.ValidationRules = Object.keys(rules).length > 0 ? JSON.stringify(rules) : null;

            let options = [];
            if (this.definitionForm.ValueType === 'select' && this.definitionForm.OptionsText) {
                let lines = this.definitionForm.OptionsText.split('\n').map(l => l.trim()).filter(l => l !== '');
                options = lines.map((line, idx) => {
                    let isDefault = false;
                    let val = line;
                    if (line.startsWith('*')) {
                        isDefault = true;
                        val = line.substring(1).trim();
                    } else if (idx === 0) {
                        isDefault = true;
                    }
                    // Simple slugify / clean text helper
                    let safeVal = val.toLowerCase().replace(/[^a-z0-9àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ\s-]/g, '').trim().replace(/\s+/g, '_');
                    return {
                        Value: safeVal || 'opt_' + idx,
                        DisplayText: val,
                        SortOrder: idx + 1,
                        IsDefault: isDefault,
                        IsActive: true
                    };
                });
            }

            var postData = Object.assign({}, this.definitionForm);
            postData.Options = options;
            postData.Severity = 'Information';
            this.normalizeNullableIntegers(postData, ['ItemId', 'InventoryId', 'GroupId']);

            this.isSavingDefinition = true;
            $.ajax({
                url: '/api/category/checklist-definition/save',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(postData),
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

        selectDropdownItem(it) {
            this.definitionForm.ItemId = it.Id;
            this.onDefinitionItemChange();
            this.showItemDropdown = false;
        },

        selectDropdownInventory(inv) {
            this.definitionForm.InventoryId = inv.Id;
            this.showInventoryDropdown = false;
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
            const isExpanded = !this.expandedInventoryMap[invId];
            this.$set(this.expandedInventoryMap, invId, isExpanded);
            if (isExpanded) {
                this.loadInventoryChecklist(invId);
            }
        },

        loadInventoryChecklist(invId, forceReload) {
            forceReload = forceReload || false;
            var cleanId = parseInt(invId, 10);
            if (isNaN(cleanId)) {
                this.showToast('❌ ID thiết bị không hợp lệ');
                return;
            }
            if (!forceReload && this.inventoryChecklistCache[cleanId]) {
                return;
            }
            this.$set(this.loadingChecklist, invId, true);
            $.ajax({
                url: '/api/category/checklist-definition/inventory/' + cleanId,
                type: 'GET',
                success: (res) => {
                    this.$set(this.inventoryChecklistCache, cleanId, res || []);
                    // Also support cache by invId to make UI work correctly if keys mismatch
                    if (invId !== cleanId) {
                        this.$set(this.inventoryChecklistCache, invId, res || []);
                    }
                },
                error: () => {
                    this.showToast('❌ Không tải được checklist của thiết bị');
                },
                complete: () => {
                    this.$set(this.loadingChecklist, invId, false);
                }
            });
        },

        saveInlineChecklist(def) {
            if (!def.CheckName || !def.CheckName.trim()) {
                this.showToast('⚠️ Tên checklist không được để trống');
                return;
            }
            if (this.savingChecklist[def.Id]) return;
            this.$set(this.savingChecklist, def.Id, true);

            var cleanInvId = parseInt(def.InventoryId, 10);
            const payload = {
                Id: def.Id,
                GroupId: def.GroupId,
                Scope: 'inventory',
                ItemId: def.ItemId || null,
                InventoryId: cleanInvId,
                CycleType: def.CycleType || null,
                CheckName: def.CheckName.trim(),
                Description: def.Description || '',
                IsRequired: !!def.IsRequired,
                SortOrder: def.SortOrder || 0,
                IsActive: !!def.IsActive
            };

            $.ajax({
                url: '/api/category/checklist-definition/save',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload),
                success: (res) => {
                    this.editingChecklistId = null;
                    if (res && res.success && res.data) {
                        // Update both caches
                        const cache1 = this.inventoryChecklistCache[def.InventoryId] || [];
                        const idx1 = cache1.findIndex(function(c) { return c.Id === def.Id; });
                        if (idx1 !== -1) {
                            this.$set(cache1, idx1, res.data);
                        }
                        if (cleanInvId !== def.InventoryId) {
                            const cache2 = this.inventoryChecklistCache[cleanInvId] || [];
                            const idx2 = cache2.findIndex(function(c) { return c.Id === def.Id; });
                            if (idx2 !== -1) {
                                this.$set(cache2, idx2, res.data);
                            }
                        }
                    }
                    this.showToast('✅ Đã lưu thay đổi!');
                },
                error: (err) => {
                    this.showToast((err.responseJSON && err.responseJSON.message) || '❌ Lỗi khi lưu thay đổi!');
                },
                complete: () => {
                    this.$set(this.savingChecklist, def.Id, false);
                }
            });
        },

        saveInlineAddChecklist(invId) {
            var cleanId = parseInt(invId, 10);
            if (isNaN(cleanId)) {
                this.showToast('❌ ID thiết bị không hợp lệ');
                return;
            }
            var form = this.inlineAddForm[invId];
            if (!form || !form.CheckName || !form.CheckName.trim()) {
                this.showToast('⚠️ Vui lòng nhập tên checklist');
                return;
            }
            if (this.savingChecklist['add_' + invId]) return;

            this.$set(this.savingChecklist, 'add_' + invId, true);

            var groupId = this.detailItem ? (this.detailItem.GroupId || 0) : (this.activeGroup ? this.activeGroup.Id : 0);
            var itemId = this.detailItem ? (this.detailItem.Id || null) : null;
            var existingCount = (this.inventoryChecklistCache[cleanId] || []).length;

            var payload = {
                Id: 0,
                GroupId: groupId,
                Scope: 'inventory',
                ItemId: itemId,
                InventoryId: cleanId,
                CycleType: form.CycleType || null,
                CheckName: form.CheckName.trim(),
                Description: '',
                IsRequired: !!form.IsRequired,
                SortOrder: existingCount + 1,
                IsActive: true
            };

            $.ajax({
                url: '/api/category/checklist-definition/save',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify(payload),
                success: (res) => {
                    if (res && res.success && res.data) {
                        var existing = this.inventoryChecklistCache[cleanId] || [];
                        var updated = existing.concat([res.data]);
                        this.$set(this.inventoryChecklistCache, cleanId, updated);
                        if (invId !== cleanId) {
                            var existing2 = this.inventoryChecklistCache[invId] || [];
                            this.$set(this.inventoryChecklistCache, invId, existing2.concat([res.data]));
                        }
                        this.$set(this.inlineAddForm, invId, {
                            CheckName: '',
                            CycleType: '',
                            IsRequired: true,
                            show: false
                        });
                        this.showToast('✅ Thêm checklist thành công!');
                    } else {
                        var errMsg = (res && res.message) ? res.message : 'Lỗi khi thêm checklist!';
                        this.showToast('❌ ' + errMsg);
                    }
                },
                error: (err) => {
                    var msg = (err.responseJSON && err.responseJSON.message)
                        ? err.responseJSON.message
                        : (err.responseText || '❌ Lỗi kết nối!');
                    this.showToast(msg);
                },
                complete: () => {
                    this.$set(this.savingChecklist, 'add_' + invId, false);
                }
            });
        },

        toggleChecklistActive(def, event) {
            if (this.savingChecklist[def.Id]) {
                if (event) event.preventDefault();
                return;
            }

            var oldState = def.IsActive;
            var newState = !oldState;
            var invId = def.InventoryId;

            def.IsActive = newState;
            this.$set(this.savingChecklist, def.Id, true);

            $.ajax({
                url: '/api/category/checklist-definition/toggle/' + def.Id,
                type: 'PUT',
                success: (res) => {
                    if (!res || !res.success) {
                        def.IsActive = oldState;
                        this.showToast('❌ Cập nhật trạng thái thất bại!');
                    } else {
                        this.showToast(newState ? '✅ Đã kích hoạt!' : '⏸️ Đã tắt!');
                    }
                },
                error: () => {
                    def.IsActive = oldState;
                    this.showToast('❌ Cập nhật trạng thái thất bại!');
                },
                complete: () => {
                    this.$set(this.savingChecklist, def.Id, false);
                }
            });
        },

        deleteChecklist(def) {
            if (this.deletingChecklist[def.Id]) return;
            if (!confirm('Xóa checklist "' + def.CheckName + '"?\nThao tác này không thể hoàn tác.')) return;

            var invId = def.InventoryId;
            var cleanId = parseInt(invId, 10);
            
            var cache = this.inventoryChecklistCache[invId] || [];
            var oldIndex = -1;
            for (var i = 0; i < cache.length; i++) {
                if (cache[i].Id === def.Id) { oldIndex = i; break; }
            }
            if (oldIndex === -1) return;

            var deletedItem = cache[oldIndex];
            var newCache = cache.filter(function(c) { return c.Id !== def.Id; });
            this.$set(this.inventoryChecklistCache, invId, newCache);
            if (invId !== cleanId) {
                var newCacheClean = (this.inventoryChecklistCache[cleanId] || []).filter(function(c) { return c.Id !== def.Id; });
                this.$set(this.inventoryChecklistCache, cleanId, newCacheClean);
            }
            
            this.$set(this.deletingChecklist, def.Id, true);

            $.ajax({
                url: '/api/category/checklist-definition/' + def.Id,
                type: 'DELETE',
                success: (res) => {
                    if (res && res.success) {
                        this.showToast('🗑️ Đã xóa checklist!');
                    } else {
                        var rb = (this.inventoryChecklistCache[invId] || []).slice();
                        rb.splice(oldIndex, 0, deletedItem);
                        this.$set(this.inventoryChecklistCache, invId, rb);
                        if (invId !== cleanId) {
                            var rbClean = (this.inventoryChecklistCache[cleanId] || []).slice();
                            rbClean.splice(oldIndex, 0, deletedItem);
                            this.$set(this.inventoryChecklistCache, cleanId, rbClean);
                        }
                        this.showToast('❌ ' + ((res && res.message) || 'Không thể xóa!'));
                    }
                },
                error: (xhr) => {
                    var rb = (this.inventoryChecklistCache[invId] || []).slice();
                    rb.splice(oldIndex, 0, deletedItem);
                    this.$set(this.inventoryChecklistCache, invId, rb);
                    if (invId !== cleanId) {
                        var rbClean = (this.inventoryChecklistCache[cleanId] || []).slice();
                        rbClean.splice(oldIndex, 0, deletedItem);
                        this.$set(this.inventoryChecklistCache, cleanId, rbClean);
                    }
                    this.showToast('❌ ' + ((xhr.responseJSON && xhr.responseJSON.message) || 'Không thể xóa!'));
                },
                complete: () => {
                    this.$set(this.deletingChecklist, def.Id, false);
                }
            });
        },

        addFromDefaultChecklist(inv) {
            var cleanId = parseInt(inv.Id, 10);
            $.ajax({
                url: '/api/category/checklist-definition/copy-default/' + cleanId,
                type: 'POST',
                beforeSend: () => {
                    this.$set(this.loadingChecklist, inv.Id, true);
                },
                success: (res) => {
                    if (res && res.success) {
                        if (res.count > 0) {
                            this.showToast('✅ Đã thêm ' + res.count + ' checklist từ mặc định!');
                            this.loadInventoryChecklist(inv.Id, true);
                        } else {
                            this.$set(this.loadingChecklist, inv.Id, false);
                            this.showToast('ℹ️ Tất cả checklist mặc định đã được thêm trước đó.');
                        }
                    } else {
                        this.$set(this.loadingChecklist, inv.Id, false);
                        this.showToast('❌ ' + ((res && res.message) || 'Lỗi khi sao chép!'));
                    }
                },
                error: (err) => {
                    this.$set(this.loadingChecklist, inv.Id, false);
                    this.showToast((err.responseJSON && err.responseJSON.message) || '❌ Lỗi khi sao chép checklist mặc định!');
                }
            });
        },

        refreshInventoryChecklist(invId) {
            this.loadInventoryChecklist(invId, true);
        },

        showInlineAddForm(invId) {
            this.$set(this.inlineAddForm, invId, {
                CheckName: '',
                CycleType: '',
                IsRequired: true,
                show: true
            });
            this.$nextTick(() => {
                var ref = this.$refs['addInput_' + invId];
                var el = Array.isArray(ref) ? ref[0] : ref;
                if (el) el.focus();
            });
        },

        cancelInlineAdd(invId) {
            this.$set(this.inlineAddForm, invId, {
                CheckName: '',
                CycleType: '',
                IsRequired: true,
                show: false
            });
        },

        startInlineEdit(def) {
            if (this.savingChecklist[def.Id]) return;
            def._snapshot = {
                CheckName: def.CheckName,
                CycleType: def.CycleType,
                IsRequired: def.IsRequired
            };
            this.editingChecklistId = def.Id;
            this.$nextTick(function() {
                var vm = appCatalog;
                var ref = vm.$refs['editInput_' + def.Id];
                var el = Array.isArray(ref) ? ref[0] : ref;
                if (el) { el.focus(); el.select(); }
            });
        },

        cancelInlineEdit(def) {
            if (this.editingChecklistId !== def.Id) return;
            if (def._snapshot) {
                def.CheckName = def._snapshot.CheckName;
                def.CycleType = def._snapshot.CycleType;
                def.IsRequired = def._snapshot.IsRequired;
            }
            this.editingChecklistId = null;
        }
    },

    // watch: {

    //     inventorySearch() {
    //         this.inventoryCurrentPage = 1
    //     },
    //     itemSearch() { this.currentPage = 1 },
    //     itemFilterStatus() { this.currentPage = 1 },
    //         currentTab(val) {

    //         if (val === 'inventory') {

    //             this.loadInventories()
    //         }
    //     }

    // },
    watch: {

        inventorySearch() {
            this.inventoryCurrentPage = 1
        },
        itemSearch() { this.currentPage = 1 },
        itemFilterStatus() { this.currentPage = 1 },
        locAssetSearch() { this.locCurrentPage = 1 },

        currentTab(val) {

            if (val === 'inventory') {
                this.loadInventories()
            }

            if (val === 'location' && this.locDepartments.length === 0) {
                this.loadLocDepartments()
            }
        }

    },

    mounted() {
        this.loadGroups()
        this.loadInventories();
        
        document.addEventListener('click', (e) => {
            if (!e.target.closest('.searchable-select-items-container')) {
                this.showItemDropdown = false;
            }
            if (!e.target.closest('.searchable-select-inventory-container')) {
                this.showInventoryDropdown = false;
            }
            if (!e.target.closest('.searchable-select-toolbar-items-container')) {
                this.showToolbarItemDropdown = false;
            }
        });
    }
})
