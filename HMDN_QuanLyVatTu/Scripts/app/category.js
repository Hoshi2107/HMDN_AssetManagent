    var appCatalog = new Vue({
        el: '#app-catalog',
    delimiters: ['${', '}'],

    data: {
        // Groups
        groups: [],
    searchQuery: '',
    filterStatus: '',

    // Items
    allItems: [],          // toàn bộ items đã load
    groupItems: [],        // items của group đang mở
    itemSearch: '',
    itemFilterStatus: '',

    // Modal flags
    showItemsModal: false,
    showGroupForm: false,
    showItemForm: false,
    showDeleteModal: false,

    activeGroup: { },

    // Forms
    isEditGroup: false,
    groupForm: {Id: null, Code: '', Name: '', Icon: '', Description: '', SortOrder: 0, IsActive: true },

    isEditItem: false,
    itemForm: {Id: null, GroupId: null, Code: '', Name: '', Brand: '', Model: '', Unit: '', Description: '', IsActive: true },

    // Delete
    deleteTarget: { },
    deleteType: '',   // 'group' | 'item'

    toast: {show: false, msg: '' }
        },

    computed: {

        filteredGroups() {
        let list = [...this.groups]

    if (this.searchQuery) {
                    const q = this.searchQuery.toLowerCase().trim()
                    list = list.filter(g =>
    ((g.Name || '') + ' ' + (g.Code || '') + ' ' + (g.Description || ''))
    .toLowerCase().includes(q)
    )
                }

    if (this.filterStatus !== '') {
                    const active = this.filterStatus === 'true'
                    list = list.filter(g => g.IsActive === active)
                }

                return list.sort((a, b) => a.SortOrder - b.SortOrder)
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
                    const active = this.itemFilterStatus === 'true'
                    list = list.filter(x => x.IsActive === active)
                }

    return list
            }
        },

    methods: {

        // ── HELPERS ──
        itemCountOf(groupId) {
                return this.allItems.filter(x => x.GroupId === groupId).length
            },

    showToast(msg) {
        this.toast = { show: true, msg }
                setTimeout(() => {this.toast.show = false}, 2800)
            },

    // ── LOAD ──
    loadGroups() {
        $.ajax({
            url: '/api/catalog/groups',
            type: 'GET',
            success: (res) => { this.groups = res },
            error: () => this.showToast('❌ Load nhóm thất bại')
        })
    },

    loadAllItems() {
        $.ajax({
            url: '/api/catalog/items',
            type: 'GET',
            success: (res) => { this.allItems = res },
            error: () => { }
        })
    },

    // ── OPEN ITEMS MODAL ──
    openGroupItems(g) {
        this.activeGroup = g
                this.groupItems = this.allItems.filter(x => x.GroupId === g.Id)
    this.itemSearch = ''
    this.itemFilterStatus = ''
    this.showItemsModal = true
            },

    // ── GROUP CRUD ──
    openAddGroup() {
        this.isEditGroup = false
                this.groupForm = {Id: null, Code: '', Name: '', Icon: '', Description: '', SortOrder: this.groups.length + 1, IsActive: true }
    this.showGroupForm = true
            },

    openEditGroup(g) {
        this.isEditGroup = true
                this.groupForm = {...g}
    this.showGroupForm = true
            },

    saveGroup() {
                if (!this.groupForm.Code.trim() || !this.groupForm.Name.trim()) {
        this.showToast('⚠️ Mã và tên nhóm không được trống!')
                    return
                }

    const url = this.isEditGroup ? '/api/catalog/group/update' : '/api/catalog/group/create'
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
                this.itemForm = {Id: null, GroupId: this.activeGroup.Id, Code: '', Name: '', Brand: '', Model: '', Unit: 'Cái', Description: '', IsActive: true }
    this.showItemForm = true
            },

    openEditItem(item) {
        this.isEditItem = true
                this.itemForm = {...item}
    this.showItemForm = true
            },

    saveItem() {
                if (!this.itemForm.Code.trim() || !this.itemForm.Name.trim() || !this.itemForm.Unit.trim()) {
        this.showToast('⚠️ Mã, tên và đơn vị không được trống!')
                    return
                }

    const url = this.isEditItem ? '/api/catalog/item/update' : '/api/catalog/item/create'
    const type = this.isEditItem ? 'PUT' : 'POST'

    $.ajax({
        url, type,
        contentType: 'application/json',
    data: JSON.stringify(this.itemForm),
                    success: () => {
        this.showItemForm = false
                        this.loadAllItems()
                        // refresh groupItems luôn
                        setTimeout(() => {
        this.groupItems = this.allItems.filter(x => x.GroupId === this.activeGroup.Id)
    }, 300)
    this.showToast(this.isEditItem ? '✅ Đã cập nhật!' : '✅ Đã thêm loại thiết bị!')
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
    ? '/api/catalog/group/delete?id=' + this.deleteTarget.Id
    : '/api/catalog/item/delete?id=' + this.deleteTarget.Id

    $.ajax({
        url,
        type: 'DELETE',
                    success: () => {
        this.showDeleteModal = false
                        if (isGroup) {
        this.loadGroups()
                            this.loadAllItems()
                        } else {
        this.loadAllItems()
                            this.groupItems = this.groupItems.filter(x => x.Id !== this.deleteTarget.Id)
                        }
    this.showToast('🗑️ Đã xoá!')
                    },
                    error: () => this.showToast('❌ Không thể xoá!')
                })
            }
        },

    mounted() {
        this.loadGroups()
            this.loadAllItems()
        }
    })

