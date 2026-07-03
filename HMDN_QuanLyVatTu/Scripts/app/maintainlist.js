var app = new Vue({
    el: '#app',
    delimiters: ['${', '}'],

    data: {
        list: [],
        loading: false,

        searchQuery: '',
        filterStatus: '',
        filterType: '',
        filterUrgency: '',
        fromDate: '',
        toDate: '',

        currentPage: 1,
        pageSize: 15,

        showDetail: false,
        selected: null
    },

    computed: {
        filteredList() {
            let data = [...this.list]

            if (this.searchQuery.trim()) {
                const q = this.searchQuery.toLowerCase()
                data = data.filter(x =>
                    (x.AssetCode || '').toLowerCase().includes(q) ||
                    (x.ItemName || '').toLowerCase().includes(q) ||
                    (x.ScheduleName || '').toLowerCase().includes(q) ||
                    (x.DepartmentName || '').toLowerCase().includes(q)
                )
            }

            if (this.filterUrgency) {
                data = data.filter(x => {
                    const d = x.DaysUntilDue
                    if (this.filterUrgency === 'overdue') return d < 0
                    if (this.filterUrgency === 'critical') return d >= 0 && d <= 3
                    if (this.filterUrgency === 'warning') return d > 3 && d <= 7
                    if (this.filterUrgency === 'ok') return d > 7
                    return true
                })
            }

            return data
        },

        kpi() {
            const data = this.filteredList
            return {
                overdue: data.filter(x => x.DaysUntilDue < 0).length,
                critical: data.filter(x => x.DaysUntilDue >= 0 && x.DaysUntilDue <= 3).length,
                warning: data.filter(x => x.DaysUntilDue > 3 && x.DaysUntilDue <= 7).length,
                ok: data.filter(x => x.DaysUntilDue > 7).length
            }
        },

        paginatedList() {
            const start = (this.currentPage - 1) * this.pageSize
            return this.filteredList.slice(start, start + this.pageSize)
        },

        totalPages() {
            return Math.max(1, Math.ceil(this.filteredList.length / this.pageSize))
        },

        pages() {
            return Array.from({ length: this.totalPages }, (_, i) => i + 1)
        }
    },

    watch: {
        searchQuery() { this.currentPage = 1 },
        filterUrgency() { this.currentPage = 1 },
        pageSize() { this.currentPage = 1 }
    },

    methods: {
        onTableScroll(e) {
            const el = e.target.closest('.table-scroll') || e.target;
            const card = el.closest('.table-card');
            if (!card) return;

            const atStart = el.scrollLeft <= 4;
            const atEnd = el.scrollLeft >= (el.scrollWidth - el.clientWidth - 4);

            card.classList.toggle('scrolled-left', !atStart);
            card.classList.toggle('scrolled-right', !atEnd);
        },

        checkTableScrollState() {
            // Gọi sau khi data load xong / resize, để set lại fade ban đầu
            this.$nextTick(() => {
                const el = this.$el.querySelector('.table-scroll');
                if (!el) return;
                const card = el.closest('.table-card');
                const needsScroll = el.scrollWidth > el.clientWidth;
                card.classList.toggle('scrolled-right', needsScroll);
                card.classList.toggle('scrolled-left', false);
            });
        },

        loadList() {
            this.loading = true
            this.currentPage = 1

            const params = new URLSearchParams()
            if (this.filterStatus) params.append('status', this.filterStatus)
            if (this.filterType) params.append('type', this.filterType)
            if (this.fromDate) params.append('fromDate', this.fromDate)
            if (this.toDate) params.append('toDate', this.toDate)
            if (this.searchQuery) params.append('search', this.searchQuery)

            $.get('/api/maintain-list/list?' + params.toString(), (res) => {
                this.list = res
                this.loading = false
                this.checkTableScrollState() 
            }).fail(() => {
                alert('Không tải được dữ liệu')
                this.loading = false
            })
        },

        resetFilter() {
            this.filterStatus = ''
            this.filterType = ''
            this.filterUrgency = ''
            this.fromDate = ''
            this.toDate = ''
            this.searchQuery = ''
            this.loadList()
        },

        openDetail(item) {
            this.selected = item
            this.showDetail = true
        },

        prevPage() { if (this.currentPage > 1) this.currentPage-- },
        nextPage() { if (this.currentPage < this.totalPages) this.currentPage++ },

        formatDate(val) {
            if (!val) return null
            return val.toString().substring(0, 10)
        },

        typeLabel(type) {
            const map = {
                preventive: 'Định kỳ',
                corrective: 'Sửa chữa',
                calibration: 'Hiệu chuẩn',
                inspection: 'Kiểm tra'
            }
            return map[type] || type
        },

        statusLabel(status) {
            const map = {
                active: 'Đang hoạt động',
                completed: 'Đã hoàn thành',
                cancelled: 'Đã hủy'
            }
            return map[status] || status
        },

        urgencyMeta(days) {
            if (days < 0) return { text: 'Quá hạn', bg: '#fee2e2', color: '#dc2626' }
            if (days <= 3) return { text: days + ' ngày', bg: '#fee2e2', color: '#dc2626' }
            if (days <= 7) return { text: days + ' ngày', bg: '#ffedd5', color: '#ea580c' }
            if (days <= 15) return { text: days + ' ngày', bg: '#fef9c3', color: '#ca8a04' }
            return { text: days + ' ngày', bg: '#dcfce7', color: '#16a34a' }
        },

        urgencyClass(days) {
            if (days < 0) return 'row-overdue'
            if (days <= 3) return 'row-critical'
            if (days <= 7) return 'row-warning'
            return ''
        }
    },

    mounted() {
        this.loadList();
        window.addEventListener('resize', this.checkTableScrollState);

    }
})