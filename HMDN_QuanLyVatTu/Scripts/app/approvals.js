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
                        (x.CreatedBy || '')
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
            // Handle C# Json format /Date(123123123)/ 
            if (dateStr.toString().indexOf('/Date') !== -1) {
                var date = new Date(parseInt(dateStr.substr(6)));
                var day = ("0" + date.getDate()).slice(-2);
                var month = ("0" + (date.getMonth() + 1)).slice(-2);
                return day + '/' + month + '/' + date.getFullYear();
            }
            var d = new Date(dateStr);
            var day = ("0" + d.getDate()).slice(-2);
            var month = ("0" + (d.getMonth() + 1)).slice(-2);
            return day + '/' + month + '/' + d.getFullYear();
        },
        statusClass(status) {
            return STATUS[status]?.class || 'badge-pending'
        },
        statusLabel(status) {
            return STATUS[status]?.label || status || 'Pending'
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
                url: '/Approvals/GetTicketDetails?ticketId=' + item.Id,
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
                url: '/Approvals/UpdateStatus',
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
                url: '/Approvals/GetTickets',
                type: 'GET',
                success: (res) => {
                    this.tickets = res
                },
                error: () => {
                    console.log('Load dữ liệu thất bại')
                }
            })
        }
    },
    mounted() {
        this.loadTickets()
    }
})
