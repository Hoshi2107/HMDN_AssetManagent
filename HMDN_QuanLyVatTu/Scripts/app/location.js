var appLoc = new Vue({

    el: '#app-loc',

    delimiters: ['${', '}'],

    data: {

        //Detail Modal
        showDetailModal: false,

        locationDetail: null,

        locationInventories: [],

        showStatusModal: false,

        statusTarget: null,

        searchQuery: '',

        filterBuilding: '',

        filterFloor: '',

        locations: [],

        sort: {
            key: '',
            dir: 1
        },

        currentPage: 1,

        pageSize: 10,

        showModal: false,

        isEdit: false,

        form: {
            Id: null,
            Name: '',
            Building: '',
            Floor: '',
        },

        toast: {
            show: false,
            msg: '',
            type: 'success'
        }
    },

    computed: {

        filteredLocations() {

            let list = [...this.locations]

            // SEARCH
            if (this.searchQuery) {

                const q = this.searchQuery
                    .toLowerCase()
                    .trim()

                list = list.filter(x =>

                    (
                        (x.Name || '') + ' ' +
                        (x.Building || '') + ' ' +
                        (x.Floor || '') + ' ' +
                        (x.Description || '')
                    )

                        .toLowerCase()
                        .includes(q)
                )
            }

            // FILTER
            if (this.filterBuilding) {

                list = list.filter(x =>
                    x.Building == this.filterBuilding
                )
            }

            if (this.filterFloor) {

                list = list.filter(x =>
                    x.Floor == this.filterFloor
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

        paginatedLocations() {

            const start =
                (this.currentPage - 1) *
                this.pageSize

            return this.filteredLocations.slice(
                start,
                start + this.pageSize
            )
        },

        totalPages() {

            return Math.max(
                1,
                Math.ceil(
                    this.filteredLocations.length /
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

        buildings() {

            return [...new Set(

                this.locations
                    .map(x => x.Building)
                    .filter(x => x)

            )]
        },

        floors() {

            return [...new Set(

                this.locations
                    .map(x => x.Floor)
                    .filter(x => x)

            )]
        },

        uniqueBuildings() {

            return this.buildings.length
        },

        uniqueFloors() {

            return this.floors.length
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

                url: '/api/locationapi/inventory?id=' + item.Id,

                type: 'GET',

                success: (res) => {

                    this.locationDetail = {

                        Code: res[0].LocationCode,
                        Name: res[0].LocationName,
                        Floor: res[0].Floor,
                        Building: res[0].Building,
                        DepartmentName: res[0].DepartmentName
                    }

                    this.locationInventories =
                        res.filter(x => x.InventoryId)

                    this.showDetailModal = true
                },

                error: () => {

                    this.showToast(
                        'Không tải được chi tiết vị trí',
                        'error'
                    )
                }
            })
        },

        openAdd() {

            this.isEdit = false

            //this.form = {
            //    Id: null,
            //    Name: '',
            //    Building: '',
            //    Floor: '',
            //    Description: ''
            //}
            this.form = {
                Id: null,
                Name: '',
                Building: '',
                Floor: '',
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
                    '/api/locationapi/togglestatus?id='
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

        toggleStatus(item) {

            const msg = item.IsActive
                ? 'Ngưng sử dụng vị trí này?'
                : 'Kích hoạt lại vị trí này?'

            if (!confirm(msg))
                return

            $.ajax({

                url:
                    '/api/locationapi/togglestatus?id='
                    + item.Id,

                type: 'POST',

                success: () => {

                    item.IsActive =
                        !item.IsActive

                    this.showToast(
                        'Cập nhật trạng thái thành công'
                    )
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
        saveLocation() {

            if (!this.form.Name.trim()) {

                this.showToast(
                    'Vui lòng nhập tên vị trí!',
                    'error'
                )

                return
            }

            const url = this.isEdit
                ? '/api/location/update'
                : '/api/location/create'
            //const url = this.isEdit
            //    ? '/api/locationapi/update'
            //    : '/api/locationapi/create'

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

                    this.loadLocations()

                    this.showToast(

                        this.isEdit
                            ? 'Đã cập nhật vị trí!'
                            : 'Đã thêm vị trí!'
                    )
                },

                error: () => {

                    this.showToast(
                        'Có lỗi xảy ra!',
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
        loadLocations() {

            $.ajax({

                url: '/api/location/list',
                //url: '/api/locationapi/list',

                type: 'GET',

                success: (res) => {

                    this.locations = res
                },

                error: () => {

                    this.showToast(
                        'Load dữ liệu thất bại!',
                        'error'
                    )
                }
            })
        }
    },

    watch: {

        searchQuery() {

            this.currentPage = 1
        },

        filterBuilding() {

            this.currentPage = 1
        },

        filterFloor() {

            this.currentPage = 1
        }
    },

    mounted() {

        this.loadLocations()
    }
})