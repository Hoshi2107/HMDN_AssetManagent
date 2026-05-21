const STATUS = {
    active: {
        label: 'Đang sử dụng',
        class: 's-active'
    },

    suspended: {
        label: 'Tạm ngưng',
        class: 's-suspended'
    }
}

var app = new Vue({
    el: '#app',

    delimiters: ['${', '}'],

    data: {
        STATUS: STATUS,

        searchQuery: '',

        filterLoca: '',

        filterDept: '',

        filterStatus: '',

        filterGroup: '',

        showModal: false,

        selectedDevice: null,

        currentPage: 1,

        pageSize: 15,

        alertCount: 3,

        devices: [],

        sort: {
            key: '',
            dir: 1
        }
    },
    
    computed: {

        filteredDevices() {

            let list = [...this.devices]

            // SEARCH
            if (this.searchQuery) {

                const q = this.searchQuery
                    .toLowerCase()
                    .trim()

                list = list.filter(x =>

                    (
                        (x.Id || '') +
                        ' ' +

                        (x.AssetCode || '') +
                        ' ' +

                        (x.ItemName || '') +
                        ' ' +
                            
                        (x.SerialNumber || '') +
                        ' ' +

                        (x.DepartmentName || '') +
                        ' ' +

                        (x.LocationName || '') +
                        ' ' +

                        (x.LifeStatus || '')

                    )
                        .toLowerCase()
                        .includes(q)
                )
            }
            //if (this.searchQuery) {

            //    const q = this.searchQuery.toLowerCase()

            //    list = list.filter(x =>
            //        (
            //            (x.id || '')+
            //            (x.name || '') +
            //            (x.serial || '') +
            //            (x.dept || '')
            //        )
            //            .toLowerCase()
            //            .includes(q)
            //    )
            //}

            // FILTER
            if (this.filterDept) {
                list = list.filter(x =>
                    x.dept == this.filterDept
                )
            }

            if (this.filterLoca) {
                list = list.filter(x =>
                    x.LocationName == this.filterLoca
                )
            }

            if (this.filterStatus) {
                list = list.filter(x =>
                    x.LifeStatus == this.filterStatus
                )
            }

            if (this.filterGroup) {

                list = list.filter(x =>
                    x.GroupName == this.filterGroup
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

        // Get unique groups for filter dropdown
        groups() {

            return [...new Set(

                this.devices
                    .map(x => x.GroupName)
                    .filter(x => x)

            )]
        },

        //Paginate
        paginatedDevices() {

            const start =
                (this.currentPage - 1) *
                this.pageSize

            return this.filteredDevices.slice(
                start,
                start + this.pageSize
            )
        },

        totalPages() {

            return Math.ceil(
                this.filteredDevices.length / this.pageSize
            )
        },

        pages() {

            return Array.from(
                { length: this.totalPages },
                (_, i) => i + 1
            )
        },

        // Get unique locations for filter dropdown
        locations() {

            return [...new Set(
                this.devices
                    .map(x => x.LocationName)
                    .filter(x => x)
            )]
        },

        status() {

            return [...new Set(
                this.devices
                    .map(x => x.LifeStatus)
                    .filter(x => x)
                    
            )]

        },

        

    },

    methods: {

        statusLabel(status) {
            return STATUS[status]?.label || status
        },

        statusClass(status) {
            return STATUS[status]?.class || ''
        },

        sortBy(key) {

            if (this.sort.key == key) {
                this.sort.dir *= -1
            }
            else {

                this.sort.key = key
                this.sort.dir = 1
            }
        },

        goDetail() {

            window.location.href =
                '/Inventory/Detail'
        },

        //Pagination
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


        loadDevices() {

            $.ajax({

                url: '/api/device/list',

                type: 'GET',

                success: (res) => {

                    this.devices = res
                },

                error: () => {

                    alert('Load dữ liệu thất bại')
                }
            })
        },

        //openDetail(id) {

        //    $.ajax({

        //        url: '/api/device/detail?id=' + id,

        //        type: 'GET',

        //        success: (res) => {

        //            this.selectedDevice = res

        //            this.showModal = true
        //        },

        //        error: () => {

        //            alert('Không load được chi tiết')
        //        }
        //    })
        //}
        openDetail(id) {
            console.log('Calling detail for id:', id) // check xem id đúng chưa

            $.ajax({
                url: '/api/device/detail?id=' + id,
                type: 'GET',
                success: (res) => {
                    console.log('Response:', res) // res là object hay array?
                    this.selectedDevice = res
                    this.showModal = true
                },
                error: (xhr) => {
                    console.log('Error:', xhr.status, xhr.responseText)
                    alert('Không load được chi tiết')
                }
            })
        }
    },

    watch: {

        searchQuery() {
            this.currentPage = 1
        },

        filterLoca() {
            this.currentPage = 1
        },

        filterGroup() {
            this.currentPage = 1
        },

        filterStatus() {
            this.currentPage = 1
        }
    },

    mounted() {

        this.loadDevices()
    }
})