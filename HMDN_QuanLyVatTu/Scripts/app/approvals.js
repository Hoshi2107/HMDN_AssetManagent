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
        filterDepartment: '',
        departmentsList: [],
        currentPage: 1,
        pageSize: 10,
        tickets: [],
        showModal: false,
        showConfirmModal: false,
        confirmAction: '',
        selectedTicket: null,
        ticketDetails: [],
        approvalNote: '',
        chatMessage: '',
        chatMessagesList: [],
        isUploadingChatFile: false,
        activeMediaTab: 'media',
        lightbox: {
            show: false,
            items: [],
            currentIndex: 0
        },
        sort: {
            key: '',
            dir: 1
        },
        // Thông tin user đang đăng nhập (đọc từ localStorage do login.js đã lưu)
        currentUser: (function() {
            try {
                var localUser = {};
                var u = localStorage.getItem('current_user');
                if (u) {
                    localUser = JSON.parse(u);
                }
                
                // Ưu tiên đọc Id và FullName từ SERVER_SESSION nếu có thông tin hợp lệ
                if (window.SERVER_SESSION && window.SERVER_SESSION.userId > 0) {
                    return {
                        Id: window.SERVER_SESSION.userId,
                        FullName: window.SERVER_SESSION.fullName || localUser.FullName || 'Người dùng',
                        roles: localUser.roles || []
                    };
                }
                return localUser.Id ? localUser : { Id: 0, FullName: 'Người dùng', roles: [] };
            } catch(e) {
                return { Id: 0, FullName: 'Người dùng', roles: [] };
            }
        })()
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
        },
        mediaFiles() {
            return this.chatMessagesList.filter(msg => !msg.isRevoked && (this.isImageMsg(msg.content) || this.isVideoMsg(msg.content))).map(msg => {
                if (this.isImageMsg(msg.content)) {
                    return {
                        id: msg.id,
                        url: this.getImageUrl(msg.content),
                        type: 'IMAGE',
                        name: msg.rawFileName || 'Ảnh',
                        sender: msg.sender,
                        time: msg.time
                    };
                } else {
                    return {
                        id: msg.id,
                        url: this.getVideoUrl(msg.content),
                        type: 'VIDEO',
                        name: msg.rawFileName || 'Video',
                        sender: msg.sender,
                        time: msg.time
                    };
                }
            });
        },
        documentFiles() {
            return this.chatMessagesList.filter(msg => !msg.isRevoked && this.isFileMsg(msg.content)).map(msg => {
                let fileData = this.getFileData(msg.content);
                return {
                    id: msg.id,
                    url: fileData.url,
                    name: fileData.name,
                    sender: msg.sender,
                    time: msg.time
                };
            });
        },
        linkFiles() {
            let links = [];
            const urlRegex = /(https?:\/\/[^\s]+)/gi;
            this.chatMessagesList.forEach(msg => {
                if (msg.isRevoked || msg.isSystem) return;
                if (this.isImageMsg(msg.content) || this.isFileMsg(msg.content) || this.isVideoMsg(msg.content)) return;

                let matches = msg.content.match(urlRegex);
                if (matches) {
                    matches.forEach(url => {
                        links.push({
                            id: msg.id,
                            url: url,
                            sender: msg.sender,
                            time: msg.time
                        });
                    });
                }
            });
            return links;
        },
        isChatLocked() {
            if (!this.selectedTicket) return false;
            // KHÓA chat khi Status là APPROVED hoặc REJECTED
            // MỞ chat khi Status là PENDING
            return this.selectedTicket.Status !== 'PENDING';
        }
    },
    methods: {
        ticketTypeLabel(type) {
            if (type === 'IMPORT') return 'Nhập kho';
            if (type === 'EXPORT') return 'Xuất kho';
            if (type === 'TRANSFER') return 'Điều chuyển';
            if (type === 'SUPPORT') return 'Hỗ trợ';
            if (type === 'REPAIR') return 'Sửa chữa';
            return type || 'Nhập kho';
        },
        getUnitFromNote(note) {
            if (!note) return 'Cái';
            var parts = note.split(' | ');
            return parts[0] || 'Cái';
        },
        getItemNoteFromNote(note) {
            if (!note) return '';
            var parts = note.split(' | ');
            if (parts.length <= 1) return '';
            return parts.slice(1).join(' | ');
        },
        isDeviceFormType(type) {
            return type === 'SUPPORT' || type === 'REPAIR';
        },
        canApprove() {
            if (!this.currentUser) return false;
            if (this.currentUser.Id === 1) return true;
            if (!this.currentUser.roles) return false;
            const checkRoles = ['admin', 'manager', 'approver'];
            return this.currentUser.roles.some(r => checkRoles.includes(r.toLowerCase()));
        },
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
            this.chatMessage = '';
            this.chatMessagesList = [];
            this.loadDiscussions(item.Id);

            $.ajax({
                url: '/api/approvals/GetTicketDetails?ticketId=' + item.Id,
                type: 'GET',
                success: (res) => {
                    if (Array.isArray(res)) {
                        this.ticketDetails = res.map(dt => {
                            return {
                                ...dt,
                                isApproved: dt.ApprovalStatus === 'rejected' ? false : true,
                                approvedQuantity: (dt.ApprovedQuantity !== null && dt.ApprovedQuantity !== undefined) ? dt.ApprovedQuantity : dt.Quantity,
                                approvalNote: dt.ApprovalNote || '',
                                note: dt.Note || ''
                            };
                        });
                    } else {
                        this.ticketDetails = [];
                    }
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
            const itemsPayload = this.ticketDetails.map(dt => {
                return {
                    Id: dt.Id,
                    ApprovedQuantity: dt.isApproved ? dt.approvedQuantity : 0,
                    IsApproved: dt.isApproved,
                    ApprovalNote: dt.approvalNote
                };
            });

            $.ajax({
                url: '/api/approvals/UpdateStatus',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    TicketId: this.selectedTicket.Id,
                    Status: status,
                    Note: this.approvalNote,
                    Items: itemsPayload,
                    UserId: this.currentUser.Id
                }),
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
            // Truyền userId để backend lọc phân quyền
            var userId = (this.currentUser && this.currentUser.Id) ? this.currentUser.Id : 0;
            var url = '/api/approvals/GetTickets?userId=' + userId;
            if (this.filterDepartment) {
                url += '&departmentId=' + this.filterDepartment;
            }
            $.ajax({
                url: url,
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
        },
        // ==== CHAT: Load từ bảng TicketDiscussions ====
        loadDiscussions(ticketId) {
            $.ajax({
                url: '/api/approvals/GetDiscussions?ticketId=' + ticketId,
                type: 'GET',
                success: (res) => {
                    let messages = [];

                    // 1. Nối tiếp các tin nhắn thảo luận từ bảng TicketDiscussions
                    if (Array.isArray(res)) {
                        const discussionMsgs = res.map(d => this.mapDiscussionToClient(d));
                        messages = messages.concat(discussionMsgs);
                    }

                    this.chatMessagesList = messages;
                    this.scrollToBottom();
                },
                error: () => {
                    console.error('Lỗi tải thảo luận!');
                }
            });
        },
        scrollToBottom() {
            this.$nextTick(() => {
                const container = this.$el.querySelector('.chat-messages');
                if (container) {
                    container.scrollTop = container.scrollHeight;
                }
            });
        },
        // Ánh xạ dữ liệu từ DB sang Model Client của chat để tương thích Index.cshtml
        mapDiscussionToClient(d) {
            let content = d.Message;
            let fileType = d.FileType ? d.FileType.trim().toUpperCase() : '';
            if (fileType === 'IMAGE') {
                content = '[IMAGE: ' + (d.FilePath || '') + ']';
            } else if (fileType === 'FILE') {
                content = '[FILE: ' + (d.FilePath || '') + '|' + (d.FileName || '') + ']';
            } else if (fileType === 'VIDEO') {
                content = '[VIDEO: ' + (d.FilePath || '') + '|' + (d.FileName || '') + ']';
            }

            // Xác định tin nhắn này có phải do user hiện tại gửi không
            // Bằng cách so sánh tên người gửi với FullName của current user
            var myName = this.currentUser && this.currentUser.FullName ? this.currentUser.FullName.trim() : '';
            var senderName = d.SenderName ? d.SenderName.trim() : '';
            var isOwnerMsg = myName !== '' && senderName === myName;

            return {
                id: d.Id,
                sender: d.SenderName,
                content: content,
                time: this.formatChatTime(d.CreatedAt),
                isSystem: false,
                isRevoked: d.IsRevoked || false,
                rawFileType: fileType,
                rawFilePath: d.FilePath,
                rawFileName: d.FileName,
                isOwnerMsg: isOwnerMsg  // true = tin nhắn của user đang đăng nhập
            };
        },
        // Lấy tên người gửi hiện tại từ localStorage
        getCurrentSenderName() {
            if (this.currentUser && this.currentUser.FullName && this.currentUser.FullName.trim() !== '') {
                return this.currentUser.FullName.trim();
            }
            return 'Người dùng';
        },
        // Kiểm tra user hiện tại có phải người tạo phiếu đang xem không
        isTicketCreator() {
            if (!this.selectedTicket || !this.currentUser) return false;
            return this.currentUser.Id === this.selectedTicket.CreatedBy;
        },
        // Gửi tin nhắn text → lưu vào TicketDiscussions
        sendChatMessage() {
            if (this.isChatLocked) return;
            if (!this.chatMessage.trim() || !this.selectedTicket) return;
            const msgText = this.chatMessage.trim();
            this.chatMessage = '';

            // Lấy tên thực của user đang đăng nhập thay vì hardcode 'Người duyệt'
            const senderName = this.getCurrentSenderName();

            $.ajax({
                url: '/api/approvals/SendChatMessage',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    TicketId: this.selectedTicket.Id,
                    SenderName: senderName,
                    Message: msgText
                }),
                success: (res) => {
                    if (res.success) {
                        // Ánh xạ và push vào danh sách chat
                        this.chatMessagesList.push(this.mapDiscussionToClient(res.item));
                        this.scrollToBottom();
                    } else {
                        alert('Không gửi được tin nhắn: ' + res.message);
                    }
                },
                error: () => {
                    alert('Lỗi hệ thống khi gửi tin nhắn!');
                }
            });
        },
        triggerAttachFile() {
            if (this.$refs.chatFileInput) {
                this.$refs.chatFileInput.click();
            }
        },
        // Upload file → lưu vào ~/Uploads + tạo dòng trong TicketDiscussions
        handleChatFileUpload(event) {
            if (this.isChatLocked) return;
            const file = event.target.files[0];
            if (!file) return;

            const allowedExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.pdf', '.doc', '.docx', '.xls', '.xlsx', '.mp4', '.mov', '.avi'];
            const fileExtension = '.' + file.name.split('.').pop().toLowerCase();
            if (!allowedExtensions.includes(fileExtension)) {
                alert('Định dạng file không được phép! Chỉ nhận ảnh, video, PDF, Word, Excel.');
                return;
            }

            var formData = new FormData();
            formData.append('fileUpload', file);
            formData.append('ticketId', this.selectedTicket.Id);              // Gửi kèm ticketId
            formData.append('senderName', this.getCurrentSenderName());       // Tên thực của user đăng nhập

            this.isUploadingChatFile = true;

            $.ajax({
                url: '/api/approvals/UploadChatFile',
                type: 'POST',
                data: formData,
                contentType: false,
                processData: false,
                success: (res) => {
                    this.isUploadingChatFile = false;
                    if (this.$refs.chatFileInput) this.$refs.chatFileInput.value = '';

                    if (res.success) {
                        // Ánh xạ và push thẳng vào danh sách
                        this.chatMessagesList.push(this.mapDiscussionToClient(res.item));
                        this.scrollToBottom();
                    } else {
                        alert('Upload file thất bại: ' + res.message);
                    }
                },
                error: () => {
                    this.isUploadingChatFile = false;
                    if (this.$refs.chatFileInput) this.$refs.chatFileInput.value = '';
                    alert('Lỗi khi tải file lên server!');
                }
            });
        },
        // Helper: kiểm tra loại message theo dạng chuỗi [IMAGE: ...] hoặc [FILE: ...] để đồng bộ Index.cshtml
        isImageMsg(content) {
            return content && content.startsWith('[IMAGE: ') && content.endsWith(']');
        },
        getImageUrl(content) {
            if (!content) return '';
            return content.substring(8, content.length - 1);
        },
        isFileMsg(content) {
            return content && content.startsWith('[FILE: ') && content.endsWith(']');
        },
        getFileData(content) {
            if (!content) return { url: '', name: '' };
            let parts = content.substring(7, content.length - 1).split('|');
            return { url: parts[0], name: parts[1] || 'Tài liệu' };
        },
        getFileIcon(fileName) {
            if (!fileName) return '📁';
            let ext = fileName.split('.').pop().toLowerCase();
            if (['jpg', 'jpeg', 'png', 'gif'].includes(ext)) return '🖼️';
            if (ext === 'pdf') return '📕';
            if (['doc', 'docx'].includes(ext)) return '📘';
            if (['xls', 'xlsx'].includes(ext)) return '📗';
            return '📁';
        },
        isVideoMsg(content) {
            return content && content.startsWith('[VIDEO: ') && content.endsWith(']');
        },
        getVideoUrl(content) {
            if (!content) return '';
            let parts = content.substring(8, content.length - 1).split('|');
            return parts[0];
        },
        getVideoName(content) {
            if (!content) return '';
            let parts = content.substring(8, content.length - 1).split('|');
            return parts[1] || 'Video';
        },
        parseMessageContent(content) {
            if (!content) return '';
            // Escape HTML to prevent XSS
            let temp = document.createElement('div');
            temp.innerText = content;
            let escaped = temp.innerHTML;

            const urlRegex = /(https?:\/\/[^\s]+)/gi;
            return escaped.replace(urlRegex, function (url) {
                return '<a href="' + url + '" target="_blank" class="chat-text-link">' + url + '</a>';
            });
        },
        replyMessage(msg) {
            if (this.isChatLocked) return;
            if (!msg) return;
            let preview = '';
            if (this.isImageMsg(msg.content)) {
                preview = '[Hình ảnh]';
            } else if (this.isVideoMsg(msg.content)) {
                preview = '[Video] ' + this.getVideoName(msg.content);
            } else if (this.isFileMsg(msg.content)) {
                preview = '[File] ' + this.getFileData(msg.content).name;
            } else {
                const plainText = msg.content || '';
                preview = plainText.length > 50 ? plainText.substring(0, 50) + '...' : plainText;
            }

            const prefix = `@Phản hồi ${msg.sender}: "${preview}" -> `;
            this.chatMessage = prefix + (this.chatMessage || '');
            this.$nextTick(() => {
                if (this.$refs.chatInput) {
                    this.$refs.chatInput.focus();
                }
            });
        },
        forwardMessage(msg) {
            if (this.isChatLocked) return;
            if (!msg) return;
            let contentToCopy = '';
            if (this.isImageMsg(msg.content)) {
                let url = this.getImageUrl(msg.content);
                contentToCopy = url.startsWith('/') ? window.location.origin + url : url;
            } else if (this.isVideoMsg(msg.content)) {
                let url = this.getVideoUrl(msg.content);
                contentToCopy = url.startsWith('/') ? window.location.origin + url : url;
            } else if (this.isFileMsg(msg.content)) {
                let url = this.getFileData(msg.content).url;
                contentToCopy = url.startsWith('/') ? window.location.origin + url : url;
            } else {
                contentToCopy = msg.content || '';
            }

            navigator.clipboard.writeText(contentToCopy).then(() => {
                alert('Đã sao chép nội dung tin nhắn để chuyển tiếp!');
            }).catch(err => {
                console.error('Không thể sao chép tin nhắn: ', err);
            });

            if (this.isChatLocked) return;
            this.chatMessage = '[Chuyển tiếp]: ' + contentToCopy;
            this.$nextTick(() => {
                if (this.$refs.chatInput) {
                    this.$refs.chatInput.focus();
                }
            });
        },
        revokeMessage(msg) {
            if (this.isChatLocked) return;
            if (!msg || !msg.id) return;
            if (!confirm('Bạn có chắc chắn muốn thu hồi tin nhắn này?')) return;

            $.ajax({
                url: '/api/approvals/RevokeChatMessage',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    MessageId: msg.id
                }),
                success: (res) => {
                    if (res.success) {
                        msg.isRevoked = true;
                        const index = this.chatMessagesList.findIndex(m => m.id === msg.id);
                        if (index !== -1) {
                            this.$set(this.chatMessagesList, index, {
                                ...this.chatMessagesList[index],
                                isRevoked: true
                            });
                        }
                    } else {
                        alert('Không thể thu hồi tin nhắn: ' + res.message);
                    }
                },
                error: () => {
                    alert('Lỗi hệ thống khi thu hồi tin nhắn!');
                }
            });
        },
        // Lightbox
        openLightbox(url, type) {
            const mediaItems = this.mediaFiles;
            const items = mediaItems.map(m => ({ url: m.url, type: m.type }));
            const idx = items.findIndex(item => item.url === url);
            this.lightbox.items = items;
            this.lightbox.currentIndex = idx >= 0 ? idx : 0;
            this.lightbox.show = true;
        },
        closeLightbox() {
            this.lightbox.show = false;
            this.lightbox.items = [];
        },
        prevLightbox() {
            if (this.lightbox.items.length === 0) return;
            this.lightbox.currentIndex = (this.lightbox.currentIndex - 1 + this.lightbox.items.length) % this.lightbox.items.length;
        },
        nextLightbox() {
            if (this.lightbox.items.length === 0) return;
            this.lightbox.currentIndex = (this.lightbox.currentIndex + 1) % this.lightbox.items.length;
        },
        formatChatTime(dateStr) {
            if (!dateStr) return '';
            var d = new Date(dateStr);
            if (isNaN(d.getTime())) return dateStr;
            var hh = ('0' + d.getHours()).slice(-2);
            var mm = ('0' + d.getMinutes()).slice(-2);
            var dd = ('0' + d.getDate()).slice(-2);
            var mo = ('0' + (d.getMonth() + 1)).slice(-2);
            return hh + ':' + mm + ' ' + dd + '/' + mo + '/' + d.getFullYear();
        },
        clearHideTimeout(msg) {
            if (!msg) return;
            if (msg.hideTimeout) {
                clearTimeout(msg.hideTimeout);
                msg.hideTimeout = null;
            }
            this.$set(msg, 'showActions', true);
            this.chatMessagesList.forEach(m => {
                if (m !== msg && m.showActions) {
                    if (m.hideTimeout) {
                        clearTimeout(m.hideTimeout);
                        m.hideTimeout = null;
                    }
                    this.$set(m, 'showActions', false);
                }
            });
        },
        startHideTimeout(msg) {
            if (!msg) return;
            if (msg.hideTimeout) {
                clearTimeout(msg.hideTimeout);
            }
            const timeoutId = setTimeout(() => {
                this.$set(msg, 'showActions', false);
                msg.hideTimeout = null;
            }, 2500);
            msg.hideTimeout = timeoutId;
        },
        getReasonDetails() {
            if (!this.chatMessagesList || this.chatMessagesList.length === 0) return '';
            // Lấy tin nhắn text đầu tiên của người tạo phiếu (không phải msg hệ thống)
            // Xác định bằng cách tìm tin nhắn mà SenderName đúng với tên người tạo phiếu
            if (!this.selectedTicket) return '';
            const creatorName = this.selectedTicket.CreatedByName || this.selectedTicket.CreatedByUsername || '';
            const firstTextMsg = this.chatMessagesList.find(m =>
                !m.isSystem && !m.isRevoked && m.rawFileType === 'TEXT' &&
                (creatorName === '' || m.sender === creatorName)
            );
            return firstTextMsg ? firstTextMsg.content : '';
        },
        loadDepartments() {
            $.ajax({
                url: '/api/approvals/GetDepartments',
                type: 'GET',
                dataType: 'json',
                success: (res) => {
                    if (Array.isArray(res)) {
                        this.departmentsList = res;
                    }
                },
                error: (xhr) => {
                    console.error('Không tải được danh sách phòng ban:', xhr.responseText);
                }
            });
        }
    },
    watch: {
        filterDepartment: function (newVal) {
            this.currentPage = 1;
            this.loadTickets();
        }
    },
    mounted() {
        this.loadDepartments();
        this.loadTickets();
    }
})
