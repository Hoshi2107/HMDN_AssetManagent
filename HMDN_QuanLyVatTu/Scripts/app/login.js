new Vue({
    el: '#app',
    data: {
        // 🌟 FIX BUG 1: Sử dụng 'email' thay vì 'username' để đồng bộ 100% với LoginViewModel bên C#
        email: '',
        password: '',
        rememberMe: false,
        showPassword: false,
        errorMessage: '',
        isLoading: false,
        hasError: false
    },
    mounted() {
        // Tự động tải lại email đã lưu nếu người dùng chọn Ghi nhớ trước đó
        const savedEmail = localStorage.getItem('saved_email');
        if (savedEmail) {
            this.email = savedEmail;
            this.rememberMe = true;
        }
    },
    methods: {
        handleLogin() {
            // Reset trạng thái thông báo lỗi trước khi bấm
            this.errorMessage = '';
            this.isLoading = true;
            this.hasError = false;

            // ── TẦNG 1: GỌI XÁC THỰC TÀI KHOẢN TỪ API ĐÃ VIẾT BẰNG STORED PROCEDURE ──
            $.ajax({
                url: '/api/authapi/login',
                type: 'POST',
                contentType: 'application/json',
                data: JSON.stringify({
                    Email: this.email,       // 🌟 FIX BUG 2: Map chính xác vào thuộc tính public string Email {get;set;}
                    Password: this.password  // Map chính xác vào thuộc tính Password
                }),
                success: (res) => {
                    if (res.success) {
                        // Xử lý lưu trữ thông tin ở LocalStorage của trình duyệt (Client Side)
                        if (this.rememberMe) {
                            localStorage.setItem('saved_email', this.email);
                        } else {
                            localStorage.removeItem('saved_email');
                        }
                        localStorage.setItem('current_user', JSON.stringify(res.user));

                        // ── TẦNG 2: BẮN AJAX ĐỒNG BỘ MẢNG QUYỀN VÀO SESSION C# (SERVER SIDE) ──
                        // Bước này cực kỳ quan trọng, giúp bộ lọc CustomAuthorize và Layout chung không đá bạn ra ngoài
                        $.ajax({
                            url: '/Account/SetLoginSession',
                            type: 'POST',
                            contentType: 'application/json',
                            data: JSON.stringify({
                                modules: res.modules, // Gửi mảng chứa code, name, url, permissions
                                fullName: res.user.FullName
                            }),
                            success: (sessionRes) => {
                                this.isLoading = false;

                                // Sau khi C# đã cất quyền vào Session an toàn -> Tiến hành chuyển hướng trang
                                if (res.modules && res.modules.length > 0) {
                                    // Tự động đẩy thẳng vào URL của phân hệ đầu tiên mà tài khoản này được gán quyền
                                    window.location.href = res.modules[0].url;
                                } else {
                                    // Phòng hờ tài khoản không có quyền nào thì đẩy về kho tài sản mặc định
                                    window.location.href = '/Inventory/Index';
                                }
                            },
                            error: () => {
                                this.isLoading = false;
                                this.triggerError('Lỗi đồng bộ phiên làm việc máy chủ (Session Error).');
                            }
                        });

                    } else {
                        this.isLoading = false;
                        this.triggerError(res.message || 'Tài khoản hoặc mật khẩu không chính xác.');
                    }
                },
                error: (xhr) => {
                    this.isLoading = false;
                    let msg = 'Không thể kết nối đến máy chủ xác thực API.';
                    try {
                        if (xhr.responseJSON && xhr.responseJSON.message) {
                            msg = xhr.responseJSON.message;
                        }
                    } catch (e) { }
                    this.triggerError(msg);
                }
            });
        },
        triggerError(msg) {
            this.errorMessage = msg;
            this.hasError = true;
            // Hiệu ứng giật khung (shake) khi có lỗi trong 400ms
            setTimeout(() => {
                this.hasError = false;
            }, 400);
        },
        forgotPassword() {
            alert('Tính năng khôi phục mật khẩu đang được bảo trì. Vui lòng liên hệ phòng Kỹ thuật / IT của bệnh viện để được cấp lại trực tiếp.');
        }
    }
});