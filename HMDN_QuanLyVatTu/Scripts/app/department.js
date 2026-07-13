var appDept = new Vue({

    el: '#app-dept',

    delimiters: ['${', '}'],

    data: {

        //Detail Modal
        showDetailModal: false,

        departmentDetail: null,

        departmentInventories: [],

        showStatusModal: false,

        statusTarget: null,

        searchQuery: '',

        departments: [],

        sort: {
            key: '',
            dir: 1
        },

        // PAGINATION cho inventory trong detail modal
        invPage: 1,
        invPageSize: 10,

        currentPage: 1,

        pageSize: 10,

        showModal: false,

        isEdit: false,

        form: {
            Id: null,
            Code: '',
            Name: '',
            Description: ''
        },

        toast: {
            show: false,
            msg: '',
            type: 'success'
        }
    },

    computed: {

        paginatedInventories() {

            const start =
                (this.invPage - 1) *
                this.invPageSize

            return this.departmentInventories.slice(
                start,
                start + this.invPageSize
            )
        },

        invTotalPages() {

            return Math.max(
                1,
                Math.ceil(
                    this.departmentInventories.length /
                    this.invPageSize
                )
            )
        },

        invPages() {

            return Array.from(
                { length: this.invTotalPages },
                (_, i) => i + 1
            )
        },

        filteredDepartments() {

            let list = [...this.departments]

            // SEARCH
            if (this.searchQuery) {

                const q = this.searchQuery
                    .toLowerCase()
                    .trim()

                list = list.filter(x =>

                    (
                        (x.Code || '') + ' ' +
                        (x.Name || '') + ' ' +
                        (x.Description || '')
                    )

                        .toLowerCase()
                        .includes(q)
                )
            }

            // SORT
            if (this.sort.key) {

                list.sort((a, b) => {

                    const A = a[this.sort.key]
                    const B = b[this.sort.key]

                    if (A < B)
                        return -1 * this.sort.dir

                    if (A > B)
                        return 1 * this.sort.dir

                    return 0
                })
            }

            return list
        },

        paginatedDepartments() {

            const start =
                (this.currentPage - 1) *
                this.pageSize

            return this.filteredDepartments.slice(
                start,
                start + this.pageSize
            )
        },

        totalPages() {

            return Math.max(
                1,
                Math.ceil(
                    this.filteredDepartments.length /
                    this.pageSize
                )
            )
        },

        pages() {

            return Array.from(
                { length: this.totalPages },
                (_, i) => i + 1
            )
        },

        totalAssets() {

            return this.departments
                .reduce((sum, x) => sum + (x.AssetCount || 0), 0)
        },

        activeDepartments() {

            return this.departments
                .filter(x => x.IsActive)
                .length
        }
    },

    methods: {

        // SORT
        sortBy(key) {

            if (this.sort.key == key) {

                this.sort.dir *= -1
            }
            else {

                this.sort.key = key
                this.sort.dir = 1
            }
        },

        sortIcon(key) {

            if (this.sort.key !== key)
                return '↕'

            return this.sort.dir === 1
                ? '↑'
                : '↓'
        },

        // PAGINATION
        changePage(page) {

            if (page < 1 || page > this.totalPages)
                return

            this.currentPage = page
        },

        nextPage() {

            if (this.currentPage < this.totalPages) {

                this.currentPage++
            }
        },

        prevPage() {

            if (this.currentPage > 1) {

                this.currentPage--
            }
        },

        // MODAL
        openToggleStatus(item) {

            this.statusTarget = item

            this.showStatusModal = true
        },

        openDetail(item) {

            $.ajax({

                url: '/api/department/inventory?id=' + item.Id,

                type: 'GET',

                success: (res) => {

                    this.departmentDetail = {

                        Code: res[0].DepartmentCode,
                        Name: res[0].DepartmentName
                    }

                    this.departmentInventories =
                        res.filter(x => x.InventoryId)

                    this.showDetailModal = true
                },

                error: () => {

                    this.showToast(
                        'Không tải được chi tiết khoa phòng',
                        'error'
                    )
                }
            })
        },

        openAdd() {

            this.isEdit = false

            this.form = {
                Id: null,
                Code: '',
                Name: '',
                Description: ''
            }

            this.showModal = true
        },

        openEdit(item) {

            this.isEdit = true

            this.form = { ...item }

            this.showModal = true
        },

        // STATUS
        confirmToggleStatus() {

            if (!this.statusTarget)
                return

            $.ajax({

                url:
                    '/api/department/togglestatus?id='
                    + this.statusTarget.Id,

                type: 'POST',

                success: () => {

                    this.statusTarget.IsActive =
                        !this.statusTarget.IsActive

                    this.showToast(
                        'Cập nhật trạng thái thành công'
                    )

                    this.showStatusModal = false

                    this.statusTarget = null
                },

                error: () => {

                    this.showToast(
                        'Cập nhật thất bại',
                        'error'
                    )
                }
            })
        },

        // SAVE
        saveDepartment() {

            if (!this.form.Code.trim()) {

                this.showToast(
                    'Vui lòng nhập mã khoa phòng!',
                    'error'
                )

                return
            }

            if (!this.form.Name.trim()) {

                this.showToast(
                    'Vui lòng nhập tên khoa phòng!',
                    'error'
                )

                return
            }

            const url = this.isEdit
                ? '/api/department/update'
                : '/api/department/create'

            const type = this.isEdit
                ? 'PUT'
                : 'POST'

            $.ajax({

                url: url,

                type: type,

                contentType: 'application/json',

                data: JSON.stringify(this.form),

                success: () => {

                    this.showModal = false

                    this.loadDepartments()

                    this.showToast(

                        this.isEdit
                            ? 'Đã cập nhật khoa phòng!'
                            : 'Đã thêm khoa phòng!'
                    )
                },

                error: (xhr) => {

                    this.showToast(
                        xhr.responseText || 'Có lỗi xảy ra!',
                        'error'
                    )
                }
            })
        },

        // TOAST
        showToast(msg, type = 'success') {

            this.toast = {
                show: true,
                msg,
                type
            }

            setTimeout(() => {

                this.toast.show = false

            }, 3000)
        },

        // LOAD
        loadDepartments() {

            $.ajax({

                url: '/api/department/list',

                type: 'GET',

                success: (res) => {

                    this.departments = res
                },

                error: () => {

                    this.showToast(
                        'Load dữ liệu thất bại!',
                        'error'
                    )
                }
            })
        },

        invChangePage(page) {

            if (page < 1 || page > this.invTotalPages)
                return

            this.invPage = page
        },

        invNextPage() {

            if (this.invPage < this.invTotalPages) {

                this.invPage++
            }
        },

        invPrevPage() {

            if (this.invPage > 1) {

                this.invPage--
            }
        },

        // SỬA lại openDetail() để reset invPage về 1 mỗi lần mở
        openDetail(item) {

            $.ajax({

                url: '/api/department/inventory?id=' + item.Id,

                type: 'GET',

                success: (res) => {

                    this.departmentDetail = {

                        Code: res[0].DepartmentCode,
                        Name: res[0].DepartmentName
                    }

                    this.departmentInventories =
                        res.filter(x => x.InventoryId)

                    this.invPage = 1 // ← thêm dòng này

                    this.showDetailModal = true
                },

                error: () => {

                    this.showToast(
                        'Không tải được chi tiết khoa phòng',
                        'error'
                    )
                }
            })
        },

    },

    watch: {

        searchQuery() {

            this.currentPage = 1
        }
    },

    mounted() {

        this.loadDepartments()
    }
})