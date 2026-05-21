var appCatalog = new Vue({
    el: '#app-catalog',
    delimiters: ['${', '}'],

    data: {
        groups: [],
        allItems: [],

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
        showItemForm: false,
        showDeleteModal: false,

        isEditGroup: false,
        isEditItem: false,

        groupForm: { Id: null, Code: '', Name: '', Icon: '', Description: '', SortOrder: 0, IsActive: true },
        itemForm: { Id: null, GroupId: null, Code: '', Name: '', Brand: '', Model: '', Unit: '', Description: '', IsActive: true },

        deleteTarget: {},
        deleteType: '',

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

        groupItems() {
            if (!this.activeGroup) return []
            return this.allItems.filter(x => x.GroupId === this.activeGroup.Id)
        },

        filteredItems() {
            let list = [...this.groupItems]

            if (this.itemSearch) {
                const q = this.itemSearch.toLowerCase().trim()
                list = list.filter(x =>
                    ((x.Name || '') + ' ' + (x.Code || '') + ' ' + (x.Brand || '') + ' ' + (x.Model || ''))
                        .toLowerCase().includes(q)
                )
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

        itemCountOf(groupId) {
            return this.allItems.filter(x => x.GroupId === groupId).length
        },

        showToast(msg) {
            this.toast = { show: true, msg }
            setTimeout(() => { this.toast.show = false }, 2800)
        },

        // ── LOAD ──
        loadGroups() {
            $.ajax({
                url: '/api/categoryapi/groups', type: 'GET',
                success: res => {
                    this.groups = res.sort((a, b) => a.SortOrder - b.SortOrder)
                    // Auto select first group
                    if (this.groups.length && !this.activeGroup)
                        this.selectGroup(this.groups[0])
                },
                error: () => this.showToast('❌ Load nhóm thất bại')
            })
        },

        loadAllItems() {
            $.ajax({
                url: '/api/categoryapi/items', type: 'GET',
                success: res => { this.allItems = res },
                error: () => { }
            })
        },

        selectGroup(g) {
            this.activeGroup = g
            this.itemSearch = ''
            this.itemFilterStatus = ''
            this.currentPage = 1
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
                url: '/api/categoryapi/item/toggle?id=' + item.Id,
                type: 'PUT',
                success: () => {
                    item.IsActive = !item.IsActive
                    this.showToast(item.IsActive ? '✅ Đã bật!' : '⏸️ Đã tắt!')
                },
                error: () => this.showToast('❌ Không cập nhật được!')
            })
        },

        openDetailItem(item) {
            // mở detail page hoặc modal chi tiết tuỳ mày
        },

        // ── GROUP CRUD ──
        openAddGroup() {
            this.isEditGroup = false
            this.groupForm = { Id: null, Code: '', Name: '', Icon: '', Description: '', SortOrder: this.groups.length + 1, IsActive: true }
            this.showGroupForm = true
        },

        openEditGroup(g) {
            this.isEditGroup = true
            this.groupForm = { ...g }
            this.showGroupForm = true
        },

        saveGroup() {
            if (!this.groupForm.Code.trim() || !this.groupForm.Name.trim()) {
                this.showToast('⚠️ Mã và tên nhóm không được trống!')
                return
            }
            const url = this.isEditGroup ? '/api/categoryapi/group/update' : '/api/categoryapi/group/create'
            const type = this.isEditGroup ? 'PUT' : 'POST'
            $.ajax({
                url, type,
                contentType: 'application/json',
                data: JSON.stringify(this.groupForm),
                success: () => {
                    this.showGroupForm = false
                    this.loadGroups()
                    this.showToast(this.isEditGroup ? '✅ Đã cập nhật nhóm!' : '✅ Đã thêm nhóm mới!')
                },
                error: () => this.showToast('❌ Có lỗi xảy ra!')
            })
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
            if (!this.itemForm.Code.trim() || !this.itemForm.Name.trim() || !this.itemForm.Unit.trim()) {
                this.showToast('⚠️ Mã, tên và đơn vị không được trống!')
                return
            }
            const url = this.isEditItem ? '/api/category/item/update' : '/api/category/item/create'
            const type = this.isEditItem ? 'PUT' : 'POST'
            $.ajax({
                url, type,
                contentType: 'application/json',
                data: JSON.stringify(this.itemForm),
                success: () => {
                    this.showItemForm = false
                    this.loadAllItems()
                    this.showToast(this.isEditItem ? '✅ Đã cập nhật!' : '✅ Đã thêm mẫu mới!')
                },
                error: () => this.showToast('❌ Có lỗi xảy ra!')
            })
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

        // Load inventory
        loadInventories() {

            $.ajax({

                url: '/api/inventory/list',

                type: 'GET',

                success: (res) => {

                    this.inventories = res
                },

                error: () => {

                    this.showToast(
                        'Không tải được tài sản',
                        'error'
                    )
                }
            })
        },
    },

    watch: {
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
        this.loadAllItems()
    }
})
