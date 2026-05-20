const STATUS = {
    PENDING: {
        label: 'PENDING',
        class: 'badge-pending'
    },
    APPROVED: {
        label: 'APPROVED',
        class: 'badge-active'
    },
    REJECTED: {
        label: 'REJECTED',
        class: 'badge-danger'
    }
}

var app = new Vue({
    el: '#app',
    delimiters: ['${', '}'],
    data: {
        STATUS: STATUS,
        searchQuery: '',
        filterType: '',
        filterStatus: '',
        currentPage: 1,
        pageSize: 10,
        tickets: [],
        showModal: false,
        showConfirmModal: false,
        confirmAction: '',
        selectedTicket: null,
        ticketDetails: [],
        approvalNote: '',
        sort: {
            key: '',
            dir: 1
        }
    },
    computed: {
        filteredTickets() {
            let list = [...this.tickets]

            // SEARCH
            if (this.searchQuery) {
                const q = this.searchQuery.toLowerCase()
                list = list.filter(x =>
                    (
                        (x.TicketCode || '') +
                        (x.TicketType || '') +
                        (x.CreatedBy || '') +
                        (x.CreatedByName || '') +
                        (x.CreatedByUsername || '')
                    )
                        .toLowerCase()
                        .includes(q)
                )
            }

            // FILTER
            if (this.filterType && this.filterType !== 'Tất cả') {
                list = list.filter(x => x.TicketType === this.filterType)
            }

            if (this.filterStatus && this.filterStatus !== 'Tất cả') {
                list = list.filter(x => x.Status === this.filterStatus)
            }

            // SORT
            if (this.sort.key) {
                list.sort((a, b) => {
                    const A = a[this.sort.key]
                    const B = b[this.sort.key]
                    if (A < B) return -1 * this.sort.dir
                    if (A > B) return 1 * this.sort.dir
                    return 0
                })
            }

            return list
        },
        paginatedTickets() {
            const start = (this.currentPage - 1) * this.pageSize
            return this.filteredTickets.slice(start, start + this.pageSize)
        },
        totalPages() {
            return Math.ceil(this.filteredTickets.length / this.pageSize) || 1
        },
        pages() {
            return Array.from({ length: this.totalPages }, (_, i) => i + 1)
        }
    },
    methods: {
        formatDate(dateStr) {
            if (!dateStr) return '';
            var s = dateStr.toString();
            if (s.indexOf('/Date') !== -1) {
                var date = new Date(parseInt(s.substr(6), 10));
                var day = ('0' + date.getDate()).slice(-2);
                var month = ('0' + (date.getMonth() + 1)).slice(-2);
                return day + '/' + month + '/' + date.getFullYear();
            }
            var iso = s.match(/^(\d{4})-(\d{2})-(\d{2})/);
            if (iso) {
                return iso[3] + '/' + iso[2] + '/' + iso[1];
            }
            var d = new Date(s);
            if (isNaN(d.getTime())) return s;
            var day = ('0' + d.getDate()).slice(-2);
            var month = ('0' + (d.getMonth() + 1)).slice(-2);
            return day + '/' + month + '/' + d.getFullYear();
        },
        creatorLabel(item) {
            if (!item) return '';
            if (item.CreatedByName) return item.CreatedByName;
            if (item.CreatedByUsername) return item.CreatedByUsername;
            return 'User ' + (item.CreatedBy || '');
        },
        creatorInitials(item) {
            var name = this.creatorLabel(item);
            var parts = name.trim().split(/\s+/);
            if (parts.length >= 2) {
                return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
            }
            return (name[0] || 'U').toUpperCase();
        },
        statusClass(status) {
            return (STATUS[status] && STATUS[status].class) || 'badge-pending'
        },
        statusLabel(status) {
            return (STATUS[status] && STATUS[status].label) || status || 'Pending'
        },
        changePage(page) {
            if (page < 1 || page > this.totalPages) return
            this.currentPage = page
        },
        nextPage() {
            if (this.currentPage < this.totalPages) this.currentPage++
        },
        prevPage() {
            if (this.currentPage > 1) this.currentPage--
        },
        viewDetail(item) {
            this.selectedTicket = item;
            this.ticketDetails = [];
            this.approvalNote = '';
            this.showModal = true;

            $.ajax({
                url: '/api/approvals/GetTicketDetails?ticketId=' + item.Id,
                type: 'GET',
                success: (res) => {
                    this.ticketDetails = res;
                },
                error: () => {
                    console.log('Lỗi lấy chi tiết!');
                }
            });
        },
        updateTicketStatus(status) {
            if (!this.selectedTicket) return;
            this.confirmAction = status;
            this.showConfirmModal = true;
        },
        // Execute status update when user confirms in modal
        executeAction(status) {
            $.ajax({
                url: '/api/approvals/UpdateStatus',
                type: 'POST',
                data: {
                    ticketId: this.selectedTicket.Id,
                    status: status,
                    note: this.approvalNote
                },
                success: (res) => {
                    if (res.success) {
                        this.showConfirmModal = false;
                        this.showModal = false;
                        this.loadTickets();
                    } else {
                        alert(res.message);
                    }
                },
                error: () => {
                    alert('Lỗi hệ thống khi cập nhật trạng thái!');
                }
            });
        },
        loadTickets() {
            $.ajax({
                url: '/api/approvals/GetTickets',
                type: 'GET',
                dataType: 'json',
                cache: false,
                success: (res) => {
                    if (Array.isArray(res)) {
                        this.tickets = res
                        return
                    }
                    if (res && res.error) {
                        console.error('GetTickets:', res.error)
                        alert('Không tải được danh sách phê duyệt: ' + res.error)
                    }
                    this.tickets = []
                },
                error: (xhr) => {
                    console.error('Load dữ liệu thất bại', xhr.status, xhr.responseText)
                    alert('Không kết nối được API danh sách phê duyệt. Vui lòng build lại project và thử lại.')
                }
            })
        }
    },
    mounted() {
        this.loadTickets()
    }
})
