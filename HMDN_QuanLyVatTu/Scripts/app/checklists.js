new Vue({
    el: '#app',
    delimiters: ['${', '}'],
    data: {
        activeTab: 'schedules',
        currentUser: (function() {
            try {
                var localUser = {};
                var u = localStorage.getItem('current_user');
                if (u) {
                    localUser = JSON.parse(u);
                }
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
        })(),

        // Lists
        schedules: [],
        logs: [],
        checklistItems: [], // Hạng mục của thiết bị đang làm checklist

        // KPIs
        kpi: {
            pending: 0,
            overdue: 0,
            completed: 0,
            passRate: 100
        },

        allGroups: [],

        // Filters
        schedulesFilter: {
            query: '',
            status: 'pending',
            cycleType: '',
            onlyOverdue: false,
            fromDate: '',
            toDate: '',
            groupId: 0,
            strictDate: false,
            kpiFilter: ''
        },
        logsFilter: {
            query: '',
            result: '',
            kpiFilter: ''
        },

        // Pagination
        schedulesPage: 1,
        schedulesPerPage: 10,
        logsPage: 1,
        logsPerPage: 10,

        // Modals visibility
        showGenerateModal: false,
        showPerformModal: false,
        showDetailsModal: false,

        // Modals loading states
        performLoading: false,
        isGenerating: false,
        isSubmitting: false,

        // Modal states
        generateForm: {
            fromDate: '',
            toDate: ''
        },
        activeSchedule: {},
        performForm: {
            qrScanned: false,
            note: ''
        },
        showQrScannerArea: false,
        scannedQrInput: '',
        logDetails: null,

        // Offline mode & Sync
        isOnline: navigator.onLine,
        offlineQueueLength: 0,
        isSyncing: false,

        // Group Batch Inspection and Asset History Enhancements
        selectedBatchScheduleIds: [],
        recentInspectionHistory: [],
        activePerformModalTab: 'checklist', // 'checklist' | 'history'
        qrFirstScannerActive: false,
        qrFirstScannedInput: '',
        selectedGroupId: 0,
        selectedGroupCycle: '',

        // Manager Approval Views
        complianceFilter: {
            fromDate: '',
            toDate: ''
        },
        departmentCompliance: [],
        pendingApprovals: [],
        selectedLogIds: [],
        isApproving: false,
        operationalKpis: {
            TotalScheduled: 0,
            TotalCompleted: 0,
            TotalPending: 0,
            Overdue: 0,
            CompletionRate: 0,
            PassRate: 0,
            FailRate: 0,
            ComplianceRate: 0,
            FailedChecklists: 0,
            RepairTicketsCreated: 0,
            RepairTicketsCompleted: 0,
            SuspendedAssets: 0
        },

        // Batch Check
        showBatchCheckModal: false,
        batchCheckGroup: null, 
        batchCheckMode: '', 
        batchCheckItems: [], 
        batchCheckNote: '',
        isBatchSubmitting: false,
        showGroupBatchSelectModal: false,
        groupBatchOptions: [],

        // Ticket tracking
        lastSubmitLogId: null,
        lastSubmitResult: '',
    },

    watch: {
        'schedulesFilter.query': function () { this.schedulesPage = 1; },
        'schedulesFilter.status': function () { this.schedulesPage = 1; this.schedulesFilter.onlyOverdue = false; },
        'schedulesFilter.cycleType': function () { this.schedulesPage = 1; },
        'schedulesFilter.fromDate': function (val) { 
            this.schedulesPage = 1; 
            this.schedulesFilter.strictDate = !!(val || this.schedulesFilter.toDate);
        },
        'schedulesFilter.toDate': function (val) { 
            this.schedulesPage = 1; 
            this.schedulesFilter.strictDate = !!(this.schedulesFilter.fromDate || val);
        },
        'schedulesFilter.groupId': function () { this.schedulesPage = 1; },
        'logsFilter.query': function () { this.logsPage = 1; },
        'logsFilter.result': function () { this.logsPage = 1; }
    },

    computed: {
        hasFailedItem() {
            var vm = this;
            if (!vm.checklistItems || vm.checklistItems.length === 0) return false;
            return vm.checklistItems.some(function (i) { return i.isPassed === false; });
        },
        checklistProgress() {
            var vm = this;
            if (!vm.checklistItems || vm.checklistItems.length === 0) return { completed: 0, total: 0, percentage: 0 };
            var completed = vm.checklistItems.filter(i => i.isPassed !== null).length;
            var total = vm.checklistItems.length;
            var percentage = Math.round((completed / total) * 100);
            return { completed, total, percentage };
        },
        checklistPassRate() {
            var vm = this;
            if (!vm.checklistItems || vm.checklistItems.length === 0) return { passed: 0, total: 0, percentage: 0 };
            var passed = vm.checklistItems.filter(i => i.isPassed === true).length;
            var total = vm.checklistItems.length;
            var percentage = Math.round((passed / total) * 100);
            return { passed, total: total, percentage };
        },
        detailLogProgress() {
            var vm = this;
            if (!vm.logDetails || !vm.logDetails.Items || vm.logDetails.Items.length === 0) return { passed: 0, total: 0, percentage: 0 };
            var passed = vm.logDetails.Items.filter(i => i.IsPassed === true).length;
            var total = vm.logDetails.Items.length;
            var percentage = total > 0 ? Math.round((passed / total) * 100) : 0;
            return { passed, total, percentage };
        },
        filteredSchedules() {
            var vm = this;
            var q = (vm.schedulesFilter.query || '').trim().toLowerCase();
            return vm.schedules.filter(function (s) {
                var matchQuery = !q || 
                    (s.AssetCode && s.AssetCode.toLowerCase().indexOf(q) > -1) ||
                    (s.ItemName && s.ItemName.toLowerCase().indexOf(q) > -1) ||
                    (s.SerialNumber && s.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                    (s.DepartmentName && s.DepartmentName.toLowerCase().indexOf(q) > -1);
                
                var matchStatus = true;
                if (vm.schedulesFilter.status) {
                    if (vm.schedulesFilter.status === 'pending') {
                        matchStatus = s.Status === 'pending';
                    } else if (vm.schedulesFilter.status === 'completed') {
                        matchStatus = s.Status === 'completed' || s.Status === 'done';
                    } else {
                        matchStatus = s.Status === vm.schedulesFilter.status;
                    }
                }
                var matchCycle = !vm.schedulesFilter.cycleType || s.CycleType === vm.schedulesFilter.cycleType;
                
                var matchOverdue = true;
                if (vm.schedulesFilter.onlyOverdue) {
                    matchOverdue = s.Status === 'overdue';
                }

                var matchFrom = true;
                if (vm.schedulesFilter.fromDate) {
                    if (vm.schedulesFilter.strictDate) {
                        matchFrom = s.ScheduledDate >= vm.schedulesFilter.fromDate;
                    } else {
                        matchFrom = s.Status === 'pending' || s.Status === 'overdue' || s.ScheduledDate >= vm.schedulesFilter.fromDate;
                    }
                }
                var matchTo = !vm.schedulesFilter.toDate || s.ScheduledDate <= vm.schedulesFilter.toDate;
                var matchGroup = !vm.schedulesFilter.groupId || s.GroupId === vm.schedulesFilter.groupId;
                
                return matchQuery && matchStatus && matchCycle && matchOverdue && matchFrom && matchTo && matchGroup;
            });
        },

        groupCardsData() {
            var vm = this;
            var counts = {};
            vm.schedules.forEach(function (s) {
                if (s.Status === 'pending' || s.Status === 'overdue' || s.Status === 'NeedsReinspection') {
                    var q = (vm.schedulesFilter.query || '').trim().toLowerCase();
                    var matchQuery = !q || 
                        (s.AssetCode && s.AssetCode.toLowerCase().indexOf(q) > -1) ||
                        (s.ItemName && s.ItemName.toLowerCase().indexOf(q) > -1) ||
                        (s.SerialNumber && s.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                        (s.DepartmentName && s.DepartmentName.toLowerCase().indexOf(q) > -1);
                    
                    var matchCycle = !vm.schedulesFilter.cycleType || s.CycleType === vm.schedulesFilter.cycleType;
                    
                    var matchOverdue = true;
                    if (vm.schedulesFilter.onlyOverdue) {
                        matchOverdue = s.Status === 'overdue';
                    }

                    var matchFrom = true;
                    if (vm.schedulesFilter.fromDate) {
                        if (vm.schedulesFilter.strictDate) {
                            matchFrom = s.ScheduledDate >= vm.schedulesFilter.fromDate;
                        } else {
                            matchFrom = s.ScheduledDate >= vm.schedulesFilter.fromDate;
                        }
                    }
                    var matchTo = !vm.schedulesFilter.toDate || s.ScheduledDate <= vm.schedulesFilter.toDate;

                    if (matchQuery && matchCycle && matchOverdue && matchFrom && matchTo) {
                        var gId = s.GroupId || 0;
                        counts[gId] = (counts[gId] || 0) + 1;
                    }
                }
            });

            var totalPending = 0;
            Object.keys(counts).forEach(function (k) {
                totalPending += counts[k];
            });

            var cards = vm.allGroups.map(function (g) {
                return {
                    Id: g.Id,
                    Name: g.Name,
                    Icon: g.Icon || '📦',
                    PendingCount: counts[g.Id] || 0
                };
            });

            cards.unshift({
                Id: 0,
                Name: 'Tất cả',
                Icon: '📦',
                PendingCount: totalPending
            });

            return cards;
        },

        paginatedSchedules() {
            var start = (this.schedulesPage - 1) * this.schedulesPerPage;
            var end = start + this.schedulesPerPage;
            return this.filteredSchedules.slice(start, end);
        },

        schedulesTotalPages() {
            var total = Math.ceil(this.filteredSchedules.length / this.schedulesPerPage);
            return total > 0 ? total : 1;
        },

        filteredLogs() {
            var vm = this;
            var q = (vm.logsFilter.query || '').trim().toLowerCase();
            return vm.logs.filter(function (l) {
                var matchQuery = !q || 
                    (l.AssetCode && l.AssetCode.toLowerCase().indexOf(q) > -1) ||
                    (l.ItemName && l.ItemName.toLowerCase().indexOf(q) > -1) ||
                    (l.SerialNumber && l.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                    (l.DepartmentName && l.DepartmentName.toLowerCase().indexOf(q) > -1);
                
                var matchResult = !vm.logsFilter.result || l.OverallResult === vm.logsFilter.result;
                
                var matchKpi = true;
                if (vm.logsFilter.kpiFilter === 'today') {
                    var todayStr = vm.getLocalTodayStr();
                    matchKpi = (l.CheckedAt || '').substring(0, 10) === todayStr;
                } else if (vm.logsFilter.kpiFilter === 'week') {
                    var oneWeekAgo = new Date();
                    oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
                    var oneWeekAgoStr = oneWeekAgo.toISOString().substring(0, 10);
                    matchKpi = (l.CheckedAt || '').substring(0, 10) >= oneWeekAgoStr;
                }

                return matchQuery && matchResult && matchKpi;
            });
        },

        paginatedLogs() {
            var start = (this.logsPage - 1) * this.logsPerPage;
            var end = start + this.logsPerPage;
            return this.filteredLogs.slice(start, end);
        },

        logsTotalPages() {
            var total = Math.ceil(this.filteredLogs.length / this.logsPerPage);
            return total > 0 ? total : 1;
        },

        batchableGroups() {
            var vm = this;
            var q = (vm.schedulesFilter.query || '').trim().toLowerCase();
            var pending = vm.schedules.filter(function (s) {
                var matchStatus = s.Status === 'pending' || s.Status === 'overdue' || s.Status === 'NeedsReinspection';
                var matchCycle = !vm.schedulesFilter.cycleType || s.CycleType === vm.schedulesFilter.cycleType;
                var matchQuery = !q || 
                    (s.AssetCode && s.AssetCode.toLowerCase().indexOf(q) > -1) ||
                    (s.ItemName && s.ItemName.toLowerCase().indexOf(q) > -1) ||
                    (s.SerialNumber && s.SerialNumber.toLowerCase().indexOf(q) > -1) ||
                    (s.DepartmentName && s.DepartmentName.toLowerCase().indexOf(q) > -1);
                
                var matchFrom = true;
                if (vm.schedulesFilter.fromDate) {
                    if (vm.schedulesFilter.strictDate) {
                        matchFrom = s.ScheduledDate >= vm.schedulesFilter.fromDate;
                    } else {
                        matchFrom = s.ScheduledDate >= vm.schedulesFilter.fromDate;
                    }
                }
                var matchTo = !vm.schedulesFilter.toDate || s.ScheduledDate <= vm.schedulesFilter.toDate;

                return matchStatus && matchCycle && matchQuery && matchFrom && matchTo;
            });

            var groups = {};
            pending.forEach(function (s) {
                var gId = s.GroupId || 0;
                if (gId === 0) return;
                
                var cycle = s.CycleType || 'adhoc';
                var key = gId + '-' + cycle;
                
                if (!groups[key]) {
                    groups[key] = {
                        GroupId: gId,
                        GroupName: s.GroupName || 'Chưa phân nhóm',
                        GroupIcon: s.GroupIcon || '📦',
                        GroupCode: s.GroupCode || '',
                        CycleType: s.CycleType,
                        DepartmentName: 'Tất cả phòng ban',
                        Schedules: []
                    };
                }
                groups[key].Schedules.push(s);
            });

            var result = [];
            Object.keys(groups).forEach(function (k) {
                result.push(groups[k]);
            });
            return result;
        },

        selectedBatchGroup() {
            var vm = this;
            if (!vm.selectedGroupId) return null;
            return vm.batchableGroups.find(function (bg) {
                return bg.GroupId === vm.selectedGroupId && bg.CycleType === vm.selectedGroupCycle;
            }) || null;
        },

        estimatedBatchSavings() {
            var vm = this;
            if (!vm.selectedBatchScheduleIds || vm.selectedBatchScheduleIds.length <= 1) return 0;
            return Math.round((vm.selectedBatchScheduleIds.length - 1) * 1.5);
        },

        technicianKpis() {
            var vm = this;
            var todayStr = vm.getLocalTodayStr();
            
            // Filters logs completed today
            var completedToday = vm.logs.filter(function(l) {
                return (l.CheckedAt || '').substring(0, 10) === todayStr;
            }).length;

            // Filters logs failed today
            var failedToday = vm.logs.filter(function(l) {
                return (l.CheckedAt || '').substring(0, 10) === todayStr && l.OverallResult === 'fail';
            }).length;

            // Filters pending items scheduled for today
            var pendingToday = vm.schedules.filter(function(s) {
                return s.ScheduledDate === todayStr && 
                       s.Status !== 'done' && 
                       s.Status !== 'completed' && 
                       s.Status !== 'skipped' && 
                       s.Status !== 'cancelled';
            }).length;

            var totalToday = completedToday + pendingToday;

            // Checked this week (last 7 days)
            var oneWeekAgo = new Date();
            oneWeekAgo.setDate(oneWeekAgo.getDate() - 7);
            var oneWeekAgoStr = oneWeekAgo.toISOString().substring(0, 10);
            var checkedThisWeek = vm.logs.filter(function(l) {
                return (l.CheckedAt || '').substring(0, 10) >= oneWeekAgoStr;
            }).length;

            var avgTime = completedToday > 0 ? "2.8" : "—";

            return {
                totalToday: totalToday,
                completedToday: completedToday,
                pendingToday: pendingToday,
                failedToday: failedToday,
                avgTime: avgTime,
                checkedThisWeek: checkedThisWeek
            };
        },

        nextPendingAsset() {
            var vm = this;
            if (!vm.activeSchedule || !vm.activeSchedule.Id) return null;
            return vm.schedules.find(function (s) {
                return s.Id !== vm.activeSchedule.Id 
                    && s.GroupId === vm.activeSchedule.GroupId 
                    && s.CycleType === vm.activeSchedule.CycleType
                    && s.DepartmentName === vm.activeSchedule.DepartmentName
                    && (s.Status === 'pending' || s.Status === 'overdue' || s.Status === 'NeedsReinspection');
            }) || null;
        },

        historyWarnings() {
            var vm = this;
            var warnings = [];
            if (!vm.activeSchedule || !vm.activeSchedule.Id) return warnings;
            
            if (vm.activeSchedule.LifeStatus === 'suspended') {
                warnings.push("Thiết bị hiện đang bị TẠM NGƯNG hoạt động.");
            }
            if (vm.activeSchedule.HasOpenRepair) {
                warnings.push("Thiết bị đang có yêu cầu sửa chữa chưa hoàn thành (Open Repair Ticket).");
            }
            
            // Check failures in recent history
            if (vm.recentInspectionHistory && vm.recentInspectionHistory.length > 0) {
                var now = new Date();
                var thirtyDaysAgo = new Date();
                thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30);
                
                var failures30Days = vm.recentInspectionHistory.filter(function (log) {
                    var checkedDate = new Date(log.CheckedAt);
                    return log.OverallResult === 'fail' && checkedDate >= thirtyDaysAgo;
                });
                
                if (failures30Days.length >= 2) {
                    warnings.push("Thiết bị đã thất bại kiểm tra " + failures30Days.length + " lần trong 30 ngày qua.");
                }
                
                // Consecutive failures
                var consecutiveFailures = 0;
                for (var i = 0; i < vm.recentInspectionHistory.length; i++) {
                    if (vm.recentInspectionHistory[i].OverallResult === 'fail') {
                        consecutiveFailures++;
                    } else {
                        break; // stop counting if we see a pass
                    }
                }
                if (consecutiveFailures >= 2) {
                    warnings.push("Cảnh báo hỏng lặp lại: Thiết bị đã thất bại liên tiếp " + consecutiveFailures + " lần gần nhất.");
                }
            }
            return warnings;
        }
    },

    methods: {
        selectGroupCard(gCard) {
            var vm = this;
            if (gCard) {
                vm.selectedGroupId = gCard.GroupId;
                vm.selectedGroupCycle = gCard.CycleType;
                vm.schedulesFilter.groupId = gCard.GroupId;
                vm.schedulesFilter.cycleType = gCard.CycleType;
            } else {
                vm.selectedGroupId = 0;
                vm.selectedGroupCycle = '';
                vm.schedulesFilter.groupId = 0;
            }
            vm.schedulesFilter.strictDate = false;
            vm.schedulesFilter.fromDate = '';
            vm.schedulesFilter.toDate = '';
        },

        // ── LOAD DATA ──
        loadActiveGroups() {
            var vm = this;
            fetch('/api/checklists/active-groups')
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.allGroups = res.data || [];
                    }
                })
                .catch(function (err) {
                    console.error('Lỗi tải danh sách nhóm:', err);
                });
        },

        // ── BATCH CHECK METHODS ──
        openBatchCheckFromCard(group) {
            var vm = this;
            var options = vm.batchableGroups.filter(function (bg) {
                return bg.GroupId === group.Id;
            });
            
            if (options.length === 0) {
                vm.toast('Thông báo', 'Không có lịch kiểm tra chờ thực hiện cho nhóm này.', 'info');
                return;
            }
            
            if (options.length === 1) {
                vm.openBatchCheckModal(options[0]);
            } else {
                vm.groupBatchOptions = options;
                vm.showGroupBatchSelectModal = true;
            }
        },

        selectGroupBatchOption(option) {
            this.showGroupBatchSelectModal = false;
            this.openBatchCheckModal(option);
        },

        isQuickPassAvailable(group) {
            if (!group || !group.Schedules) return false;
            // Block quick pass for MEDICAL group
            if (group.GroupCode === 'MEDICAL') return false;
            var size = group.Schedules.length;
            if (size < 1) return false;
            
            var hasSuspended = group.Schedules.some(function (s) { return s.LifeStatus === 'suspended'; });
            var hasOpenRepair = group.Schedules.some(function (s) { return s.HasOpenRepair === true; });
            var hasNeedsReinspection = group.Schedules.some(function (s) { return s.Status === 'NeedsReinspection' || s.OriginalStatus === 'NeedsReinspection'; });
            var hasRestricted = group.Schedules.some(function (s) { return s.Criticality === 'High' || s.Criticality === 'Critical'; });
            
            return !hasSuspended && !hasOpenRepair && !hasNeedsReinspection && !hasRestricted;
        },

        isBatchCheckEligible(group) {
            if (!group || !group.Schedules || group.Schedules.length === 0) return { eligible: false, reason: "Không có thiết bị." };
            
            // Block batch check for MEDICAL group
            if (group.GroupCode === 'MEDICAL') {
                return { eligible: false, reason: "Thiết bị y tế có các hạng mục checklist riêng biệt cho từng loại máy, yêu cầu kiểm tra và đánh giá riêng lẻ từng thiết bị để đảm bảo an toàn." };
            }

            var hasRestricted = group.Schedules.some(function (s) {
                return s.Criticality === 'High' || s.Criticality === 'Critical';
            });
            if (hasRestricted) {
                return { eligible: false, reason: "Nhóm chứa thiết bị có độ quan trọng cao (High/Critical) yêu cầu kiểm tra riêng lẻ." };
            }
            
            var hasInactive = group.Schedules.some(function (s) {
                return s.LifeStatus !== 'active';
            });
            if (hasInactive) {
                return { eligible: false, reason: "Nhóm chứa thiết bị đang bị tạm ngưng hoặc không hoạt động." };
            }
            
            return { eligible: true, reason: "" };
        },

        quickPassGroup(group) {
            var vm = this;
            if (!vm.isQuickPassAvailable(group)) {
                vm.toast('Cảnh báo', 'Không đủ điều kiện để thực hiện Đạt Nhanh nhóm này.', 'warning');
                return;
            }
            
            var count = group.Schedules.length;
            if (!confirm("You are about to mark " + count + " assets as passed. Continue?\n(Bạn chuẩn bị đánh dấu ĐẠT cho toàn bộ " + count + " thiết bị của nhóm này. Tiếp tục?)")) {
                return;
            }
            
            vm.isBatchSubmitting = true;
            var scheduleIds = group.Schedules.map(function (s) { return s.Id; });
            
            fetch('/api/checklists/group-definitions?groupId=' + group.GroupId + '&cycleType=' + (group.CycleType || ''))
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success && res.data) {
                        var itemsPayload = res.data.map(function (item) {
                            return {
                                DefinitionId: item.Id,
                                IsPassed: true,
                                Note: null
                            };
                        });
                        
                        var payload = {
                            ScheduleIds: scheduleIds,
                            Mode: 'quick',
                            OverallResult: 'pass',
                            Items: itemsPayload,
                            Note: 'Hệ thống tự động Đạt Nhanh hàng loạt.',
                            CheckedBy: vm.currentUser.Id
                        };
                        
                        fetch('/api/checklists/batch-check-group', {
                            method: 'POST',
                            headers: { 'Content-Type': 'application/json' },
                            body: JSON.stringify(payload)
                        })
                        .then(function (r) { return r.json(); })
                        .then(function (saveRes) {
                            vm.isBatchSubmitting = false;
                            if (saveRes.success) {
                                vm.toast('Thành công', 'Đã lưu đạt hàng loạt cho ' + count + ' thiết bị.', 'success');
                                vm.loadSchedules();
                                vm.loadLogs();
                            } else {
                                vm.toast('Lỗi', saveRes.message, 'danger');
                            }
                        })
                        .catch(function (err) {
                            vm.isBatchSubmitting = false;
                            console.error(err);
                            vm.toast('Lỗi kết nối', 'Không thể gửi kết quả lên máy chủ.', 'danger');
                        });
                    } else {
                        vm.isBatchSubmitting = false;
                        vm.toast('Lỗi', 'Không thể tải cấu trúc biểu mẫu.', 'danger');
                    }
                })
                .catch(function (err) {
                    vm.isBatchSubmitting = false;
                    console.error(err);
                });
        },

        isQuickPassAvailableForSch(sch) {
            if (!sch) return false;
            if (sch.LifeStatus === 'suspended') return false;
            if (sch.HasOpenRepair === true) return false;
            if (sch.Status === 'NeedsReinspection' || sch.OriginalStatus === 'NeedsReinspection') return false;
            if (sch.Criticality === 'High' || sch.Criticality === 'Critical') return false;
            return true;
        },

        quickPassSingle(sch) {
            var vm = this;
            if (!vm.isQuickPassAvailableForSch(sch)) {
                vm.toast('Cảnh báo', 'Thiết bị này không đủ điều kiện (High/Critical hoặc đang tạm ngưng) để Đạt Nhanh.', 'warning');
                return;
            }
            if (!confirm("Xác nhận đánh giá ĐẠT cho thiết bị " + sch.ItemName + " (" + sch.AssetCode + ")?")) {
                return;
            }
            
            vm.$set(sch, '_isQuickPassing', true);

            var isAsset = !!sch.InventoryId;
            var scope = isAsset ? 3 : 4;
            var targetId = isAsset ? sch.InventoryId : sch.LocationId;
            var url = '/api/checklists/template?scope=' + scope + '&targetId=' + targetId + '&cycleType=' + (sch.CycleType || '') + '&scheduledDate=' + (sch.ScheduledDate || '');

            fetch(url)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success && res.data) {
                        var itemsPayload = res.data.map(function (item) {
                            var stringVal = null;
                            if (item.ValueType === 'select') {
                                var defaultOpt = (item.Options || []).find(function (o) { return o.IsDefault || o.isDefault; });
                                stringVal = defaultOpt ? defaultOpt.Value : 'normal';
                            }
                            return {
                                DefinitionId: item.Id,
                                IsPassed: true,
                                NumericValue: null,
                                StringValue: stringVal,
                                Note: null
                            };
                        });
                        
                        var payload = {
                            ScheduleId: sch.Id,
                            InventoryId: sch.InventoryId || null,
                            LocationId: sch.LocationId || null,
                            TemplateVersionId: res.templateVersionId || null,
                            CheckedBy: vm.currentUser.Id,
                            CycleType: sch.CycleType,
                            OverallResult: 'pass',
                            Note: 'Hệ thống tự động Đạt Nhanh (Duyệt nhanh).',
                            QrScanned: false,
                            QrLocation: sch.LocationName || sch.DepartmentName || '',
                            ImageUrls: '',
                            Items: itemsPayload
                        };

                        vm.postSaveChecklist(payload, true, sch);
                    } else {
                        vm.toast('Lỗi', res.message || 'Không thể tải cấu hình mẫu checklist.', 'danger');
                        vm.$set(sch, '_isQuickPassing', false);
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi', 'Không thể kết nối đến máy chủ.', 'danger');
                    vm.$set(sch, '_isQuickPassing', false);
                });
        },

        openBatchCheckModal(batchGroup) {
            var vm = this;
            vm.batchCheckGroup = batchGroup;
            
            var eligibility = vm.isBatchCheckEligible(batchGroup);
            if (!eligibility.eligible) {
                vm.toast('Cảnh báo hạn chế', 'Không cho phép kiểm tra hàng loạt: ' + eligibility.reason, 'danger');
                return;
            }

            vm.selectedBatchScheduleIds = batchGroup.Schedules.map(function (s) { return s.Id; });
            vm.batchCheckMode = 'template'; 
            vm.batchCheckItems = [];
            vm.batchCheckNote = '';
            vm.showBatchCheckModal = true;
            if (vm.performForm) {
                vm.performForm.submitted = false;
            }
            vm.lastSubmitLogId = null;
            vm.lastSubmitResult = '';
            
            vm.loadBatchCheckDefinitions(batchGroup.GroupId, batchGroup.CycleType);
        },

        loadBatchCheckDefinitions(groupId, cycleType) {
            var vm = this;
            fetch('/api/checklists/group-definitions?groupId=' + groupId + '&cycleType=' + (cycleType || ''))
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.batchCheckItems = (res.data || []).map(function (item) {
                            return {
                                DefinitionId: item.Id,
                                CheckName: item.CheckName,
                                Scope: item.Scope,
                                IsPassed: true,
                                Note: ''
                            };
                        });
                    } else {
                        vm.toast('Lỗi', 'Không thể tải hạng mục kiểm tra.', 'danger');
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi', 'Không thể kết nối máy chủ.', 'danger');
                });
        },

        submitBatchCheck() {
            var vm = this;
            if (vm.selectedBatchScheduleIds.length === 0) {
                vm.toast('Cảnh báo', 'Vui lòng chọn ít nhất một thiết bị để thực hiện kiểm tra.', 'warning');
                return;
            }
            if (!vm.batchCheckMode) {
                vm.toast('Cảnh báo', 'Vui lòng chọn chế độ kiểm tra.', 'warning');
                return;
            }

            var count = vm.selectedBatchScheduleIds.length;
            if (!confirm("You are about to apply this checklist result to " + count + " assets. Continue?\n(Bạn chuẩn bị áp dụng kết quả checklist này cho " + count + " thiết bị. Tiếp tục?)")) {
                return;
            }
            
            vm.isBatchSubmitting = true;
            var scheduleIds = vm.selectedBatchScheduleIds;
            
            var overallResult = 'pass';
            if (vm.batchCheckMode === 'template') {
                var anyFailed = vm.batchCheckItems.some(function (item) { return !item.IsPassed; });
                var allFailed = vm.batchCheckItems.every(function (item) { return !item.IsPassed; });
                if (allFailed) {
                    overallResult = 'fail';
                } else if (anyFailed) {
                    overallResult = 'partial';
                }
            }
            
            var payload = {
                ScheduleIds: scheduleIds,
                Mode: vm.batchCheckMode,
                OverallResult: overallResult,
                Items: vm.batchCheckItems.map(function (item) {
                    return {
                        DefinitionId: item.DefinitionId,
                        IsPassed: item.IsPassed,
                        Note: item.Note
                    };
                }),
                Note: vm.batchCheckNote,
                CheckedBy: vm.currentUser.Id
            };
            
            fetch('/api/checklists/batch-check-group', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isBatchSubmitting = false;
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    
                    // Save last submit info for ticket creation redirect
                    vm.lastSubmitLogId = res.failedLogId || res.firstLogId;
                    vm.lastSubmitResult = overallResult;

                    if (vm.performForm) {
                        vm.performForm.submitted = true;
                    }

                    if (overallResult === 'pass') {
                        vm.showBatchCheckModal = false;
                    }
                    vm.loadSchedules();
                    vm.loadLogs();
                } else {
                    vm.toast('Thất bại', res.message, 'danger');
                }
            })
            .catch(function (err) {
                vm.isBatchSubmitting = false;
                console.error(err);
                vm.toast('Lỗi', 'Không thể kết nối máy chủ.', 'danger');
            });
        },

        loadSchedules() {
            var vm = this;
            var now = new Date();
            var start = new Date(now.getFullYear() - 1, now.getMonth(), now.getDate());
            var end = new Date(now.getFullYear(), now.getMonth() + 3, now.getDate());
            
            var startStr = start.toISOString().substring(0, 10);
            var endStr = end.toISOString().substring(0, 10);

            function useCachedSchedules() {
                var cached = localStorage.getItem('checklist_schedules_cache');
                if (cached) {
                    var parsed = JSON.parse(cached);
                    var todayStr = vm.getLocalTodayStr();
                    parsed.forEach(function (s) {
                        if (s.Status === 'pending' && s.DueDate < todayStr) {
                            s.Status = 'overdue';
                        }
                    });
                    vm.schedules = parsed;
                    vm.calculateKPIs();
                    vm.checkUrlParams();
                    vm.toast('Ngoại tuyến', 'Đang sử dụng dữ liệu lịch trình đã lưu trong bộ nhớ cache.', 'warning');
                } else {
                    vm.toast('Lỗi', 'Không thể tải lịch trình và không có dữ liệu cache.', 'danger');
                }
            }

            if (!vm.isOnline) {
                useCachedSchedules();
                return;
            }

            fetch('/api/checklists/schedules?fromDate=' + startStr + '&toDate=' + endStr)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        console.log("=== DEBUG SCHEDULES ===", res.data);
                        var todayStr = vm.getLocalTodayStr();
                        res.data.forEach(function (s) {
                            if (s.Status === 'pending' && s.DueDate < todayStr) {
                                s.Status = 'overdue';
                            }
                        });
                        vm.schedules = res.data;
                        localStorage.setItem('checklist_schedules_cache', JSON.stringify(res.data));
                        vm.calculateKPIs();
                        vm.checkUrlParams();
                    } else {
                        console.log("=== DEBUG SCHEDULES FAILED ===", res);
                        useCachedSchedules();
                    }
                })
                .catch(function (err) {
                    console.error("=== DEBUG FETCH ERROR ===", err);
                    useCachedSchedules();
                });
        },

        loadLogs() {
            var vm = this;

            function useCachedLogs() {
                var cached = localStorage.getItem('checklist_logs_cache');
                if (cached) {
                    vm.logs = JSON.parse(cached);
                    vm.calculateKPIs();
                    vm.toast('Ngoại tuyến', 'Đang sử dụng nhật ký đã lưu trong bộ nhớ cache.', 'warning');
                } else {
                    vm.toast('Lỗi', 'Không thể tải nhật ký và không có dữ liệu cache.', 'danger');
                }
            }

            if (!vm.isOnline) {
                useCachedLogs();
                return;
            }

            fetch('/api/checklists/logs')
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.logs = res.data;
                        localStorage.setItem('checklist_logs_cache', JSON.stringify(res.data));
                        vm.calculateKPIs();
                    } else {
                        useCachedLogs();
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    useCachedLogs();
                });
        },

        // ── KPIS CALCULATION ──
        calculateKPIs() {
            var vm = this;
            var todayStr = vm.getLocalTodayStr();

            // 1. Pending & Overdue
            var pending = 0;
            var overdue = 0;
            vm.schedules.forEach(function (s) {
                if (s.Status === 'pending' || s.Status === 'NeedsReinspection') {
                    pending++;
                } else if (s.Status === 'overdue') {
                    pending++;
                    overdue++;
                }
            });

            // 2. Completed (in current month)
            var completed = 0;
            var currentMonth = new Date().toISOString().substring(0, 7); // yyyy-MM
            vm.logs.forEach(function (l) {
                if (l.CheckedAt.substring(0, 7) === currentMonth) {
                    completed++;
                }
            });

            // 3. Pass Rate
            var totalLogs = vm.logs.length;
            var passLogs = vm.logs.filter(function (l) { return l.OverallResult === 'pass'; }).length;
            var passRate = totalLogs > 0 ? Math.round((passLogs / totalLogs) * 100) : 100;

            vm.kpi = {
                pending: pending,
                overdue: overdue,
                completed: completed,
                passRate: passRate
            };
        },

        // ── PAGINATION CONTROLS ──
        setSchedulesPage(p) {
            if (p >= 1 && p <= this.schedulesTotalPages) {
                this.schedulesPage = p;
            }
        },

        setLogsPage(p) {
            if (p >= 1 && p <= this.logsTotalPages) {
                this.logsPage = p;
            }
        },

        // ── KPI CARD FILTERING ──
        filterByKpi(type) {
            var vm = this;
            if (type === 'pending') {
                vm.activeTab = 'schedules';
                vm.schedulesFilter.status = 'pending';
                vm.schedulesFilter.onlyOverdue = false;
                vm.schedulesFilter.query = '';
                vm.schedulesFilter.cycleType = '';
                vm.schedulesFilter.fromDate = '';
                vm.schedulesFilter.toDate = '';
            } else if (type === 'overdue') {
                vm.activeTab = 'schedules';
                vm.schedulesFilter.status = 'overdue';
                vm.schedulesFilter.onlyOverdue = false;
                vm.schedulesFilter.query = '';
                vm.schedulesFilter.cycleType = '';
                vm.schedulesFilter.fromDate = '';
                vm.schedulesFilter.toDate = '';
            } else if (type === 'completed') {
                vm.activeTab = 'logs';
                vm.logsFilter.result = '';
                vm.logsFilter.query = '';
            }
        },

        filterByTechnicianKpi(type) {
            var vm = this;
            var todayStr = vm.getLocalTodayStr();
            vm.schedulesFilter.strictDate = false;
            vm.schedulesFilter.kpiFilter = '';
            vm.logsFilter.kpiFilter = '';

            if (type === 'totalToday') {
                vm.activeTab = 'schedules';
                vm.schedulesFilter.status = '';
                vm.schedulesFilter.fromDate = todayStr;
                vm.schedulesFilter.toDate = todayStr;
                vm.schedulesFilter.strictDate = true;
                vm.schedulesFilter.query = '';
                vm.schedulesFilter.cycleType = '';
            } else if (type === 'completedToday') {
                vm.activeTab = 'logs';
                vm.logsFilter.kpiFilter = 'today';
                vm.logsFilter.result = '';
                vm.logsFilter.query = '';
            } else if (type === 'pendingToday') {
                vm.activeTab = 'schedules';
                vm.schedulesFilter.status = 'pending';
                vm.schedulesFilter.fromDate = todayStr;
                vm.schedulesFilter.toDate = todayStr;
                vm.schedulesFilter.strictDate = true;
                vm.schedulesFilter.query = '';
                vm.schedulesFilter.cycleType = '';
            } else if (type === 'failedToday') {
                vm.activeTab = 'logs';
                vm.logsFilter.kpiFilter = 'today';
                vm.logsFilter.result = 'fail';
                vm.logsFilter.query = '';
            } else if (type === 'checkedThisWeek') {
                vm.activeTab = 'logs';
                vm.logsFilter.kpiFilter = 'week';
                vm.logsFilter.result = '';
                vm.logsFilter.query = '';
            }
        },

        resetFilters() {
            this.schedulesFilter = {
                query: '',
                status: 'pending',
                cycleType: '',
                onlyOverdue: false,
                fromDate: '',
                toDate: '',
                strictDate: false,
                kpiFilter: ''
            };
        },

        // ── URL QUERY ROUTING PARSING ──
        checkUrlParams() {
            var vm = this;
            var params = new URLSearchParams(window.location.search);
            
            var tab = params.get('tab');
            if (tab) {
                vm.activeTab = tab;
            }

            var status = params.get('status');
            if (status !== null) {
                vm.schedulesFilter.status = status;
            }

            var fromDate = params.get('fromDate');
            if (fromDate) {
                vm.schedulesFilter.fromDate = fromDate;
            }
            var toDate = params.get('toDate');
            if (toDate) {
                vm.schedulesFilter.toDate = toDate;
            }

            var inventoryIdStr = params.get('inventoryId');
            if (inventoryIdStr) {
                var inventoryId = parseInt(inventoryIdStr);
                if (!isNaN(inventoryId)) {
                    var sch = vm.schedules.find(function (s) {
                        return s.InventoryId === inventoryId && (s.Status === 'pending' || s.Status === 'overdue');
                    });
                    
                    if (sch) {
                        vm.openPerformModal(sch);
                        var newUrl = window.location.protocol + "//" + window.location.host + window.location.pathname;
                        window.history.replaceState({ path: newUrl }, '', newUrl);
                    } else {
                        var anySch = vm.schedules.find(function (s) {
                            return s.InventoryId === inventoryId;
                        });
                        if (anySch && anySch.AssetCode) {
                            vm.schedulesFilter.query = anySch.AssetCode;
                            vm.schedulesFilter.status = '';
                            vm.toast('Kiểm tra hoàn thành', 'Không có lịch kiểm tra chờ xử lý cho thiết bị này. Lần kiểm tra này có thể đã được hoàn thành trước đó.', 'warning');
                        } else {
                            vm.toast('Không tìm thấy lịch trình', 'Thiết bị được yêu cầu hiện tại không có lịch trình checklist nào trong hệ thống.', 'danger');
                        }
                        var newUrl = window.location.protocol + "//" + window.location.host + window.location.pathname;
                        window.history.replaceState({ path: newUrl }, '', newUrl);
                    }
                }
            }
        },

        // ── SIMULATED QR CODE SCAN ──
        openQrScanner() {
            this.showQrScannerArea = true;
            this.scannedQrInput = '';
        },

        autoFillQrCode() {
            this.scannedQrInput = this.activeSchedule.QrCode || ('QR-' + this.activeSchedule.AssetCode);
        },

        verifyQrCode() {
            var vm = this;
            var expected = (vm.activeSchedule.QrCode || ('QR-' + vm.activeSchedule.AssetCode)).trim().toLowerCase();
            var input = (vm.scannedQrInput || '').trim().toLowerCase();
            
            if (!input) {
                vm.toast('Cảnh báo', 'Vui lòng nhập hoặc quét mã QR thiết bị.', 'warning');
                return;
            }

            if (input === expected) {
                vm.performForm.qrScanned = true;
                vm.showQrScannerArea = false;
                vm.toast('Thành công', 'Xác thực QR Code trùng khớp thiết bị: ' + vm.activeSchedule.ItemName, 'success');
            } else {
                vm.performForm.qrScanned = false;
                var foundOther = vm.schedules.find(s => (s.QrCode || ('QR-' + s.AssetCode)).trim().toLowerCase() === input);
                if (foundOther) {
                    vm.toast('Lỗi xác thực', 'Mã QR này thuộc về thiết bị khác (' + foundOther.ItemName + ') thuộc nhóm ' + foundOther.GroupName + '. (Sai thiết bị / Wrong Asset)', 'danger');
                } else {
                    vm.toast('Lỗi xác thực', 'Mã QR không hợp lệ hoặc không tìm thấy thiết bị (Asset Not Found / QR Mismatch). Vui lòng quét lại.', 'danger');
                }
            }
        },

        openQrFirstScanner() {
            this.qrFirstScannerActive = true;
            this.qrFirstScannedInput = '';
        },

        verifyQrFirst() {
            var vm = this;
            var input = (vm.qrFirstScannedInput || '').trim().toLowerCase();
            if (!input) {
                vm.toast('Cảnh báo', 'Vui lòng nhập hoặc quét mã QR thiết bị.', 'warning');
                return;
            }

            var match = vm.schedules.find(function (s) {
                var expected = (s.QrCode || ('QR-' + s.AssetCode)).trim().toLowerCase();
                return expected === input && (s.Status === 'pending' || s.Status === 'overdue' || s.Status === 'NeedsReinspection');
            });

            if (match) {
                vm.qrFirstScannerActive = false;
                vm.openPerformModal(match);
                vm.performForm.qrScanned = true; // Auto-validated QR location
                vm.toast('Thành công', 'Nhận diện thiết bị thành công: ' + match.ItemName, 'success');
            } else {
                var anyMatch = vm.schedules.find(function (s) {
                    var expected = (s.QrCode || ('QR-' + s.AssetCode)).trim().toLowerCase();
                    return expected === input;
                });
                if (anyMatch) {
                    vm.toast('Thông báo', 'Thiết bị này (' + anyMatch.ItemName + ') đã hoàn thành checklist hoặc không có lịch trình chờ xử lý.', 'info');
                } else {
                    vm.toast('Lỗi', 'Không tìm thấy thiết bị nào có mã QR tương ứng trong lịch trình: ' + input, 'danger');
                }
            }
        },

        // ── ACTIONS ──
        generateSchedules() {
            var vm = this;
            if (!vm.generateForm.fromDate || !vm.generateForm.toDate) {
                vm.toast('Cảnh báo', 'Vui lòng chọn đầy đủ thời gian.', 'warning');
                return;
            }
            if (vm.generateForm.fromDate > vm.generateForm.toDate) {
                vm.toast('Cảnh báo', 'Ngày bắt đầu không được lớn hơn ngày kết thúc.', 'warning');
                return;
            }

            vm.isGenerating = true;
            fetch('/api/checklists/generate', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    FromDate: vm.generateForm.fromDate,
                    ToDate: vm.generateForm.toDate
                })
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isGenerating = false;
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    vm.showGenerateModal = false;
                    vm.loadSchedules();
                } else {
                    vm.toast('Lỗi', res.message, 'danger');
                }
            })
            .catch(function (err) {
                vm.isGenerating = false;
                console.error(err);
                vm.toast('Lỗi', 'Không thể kết nối máy chủ.', 'danger');
            });
        },

        loadRecentHistory(inventoryId) {
            var vm = this;
            if (!vm.isOnline) return;
            fetch('/api/checklists/logs?inventoryId=' + inventoryId)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.recentInspectionHistory = (res.data || []).slice(0, 5);
                    }
                })
                .catch(function (err) { return console.error(err); });
        },

        goToNextAsset() {
            var vm = this;
            var next = vm.nextPendingAsset;
            if (next) {
                vm.openPerformModal(next);
            }
        },

        openPerformModal(schedule) {
            var vm = this;
            vm.activePerformModalTab = 'checklist';
            vm.recentInspectionHistory = [];
            if (schedule.InventoryId) {
                vm.loadRecentHistory(schedule.InventoryId);
            }

            vm.activeSchedule = schedule;
            vm.performForm = {
                qrScanned: false,
                note: '',
                submitted: false
            };
            vm.checklistItems = [];
            vm.showPerformModal = true;
            vm.performLoading = true;
            vm.lastSubmitLogId = null;
            vm.lastSubmitResult = '';

            var isAsset = !!schedule.InventoryId;
            var scope = isAsset ? 3 : 4;
            var targetId = isAsset ? schedule.InventoryId : schedule.LocationId;
            var key = (isAsset ? 'asset_' + targetId : 'loc_' + targetId) + '_' + schedule.CycleType;

            function useCachedDefs() {
                var defsCache = {};
                try {
                    var existing = localStorage.getItem('checklist_defs_cache');
                    if (existing) defsCache = JSON.parse(existing);
                } catch(e) {}
                var cachedData = defsCache[key];
                if (cachedData) {
                    vm.checklistItems = cachedData.map(function (item) {
                        return {
                            DefinitionId: item.Id,
                            CheckName: item.CheckName,
                            Description: item.Description,
                            IsRequired: item.IsRequired,
                            ValueType: item.ValueType || 'checkbox',
                            Unit: item.Unit || '',
                            ValidationRules: item.ValidationRules ? JSON.parse(item.ValidationRules) : null,
                            Options: item.Options || [],
                            isPassed: null,
                            numericValue: null,
                            stringValue: item.ValueType === 'select' ? ((item.Options && item.Options.find(function(o) { return o.IsDefault; })) ? item.Options.find(function(o) { return o.IsDefault; }).Value : '') : '',
                            note: ''
                        };
                    });
                    vm.performLoading = false;
                    vm.toast('Ngoại tuyến', 'Sử dụng biểu mẫu checklist đã lưu trong bộ nhớ cache.', 'warning');
                } else {
                    vm.performLoading = false;
                    vm.toast('Lỗi', 'Không thể tải biểu mẫu checklist và không có dữ liệu cache.', 'danger');
                    vm.showPerformModal = false;
                }
            }

            if (!vm.isOnline) {
                useCachedDefs();
                return;
            }

            fetch('/api/checklists/template?scope=' + scope + '&targetId=' + targetId + '&cycleType=' + schedule.CycleType + '&scheduledDate=' + (schedule.ScheduledDate || ''))
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    vm.performLoading = false;
                    if (res.success) {
                        vm.activeScheduleTemplateVersionId = res.templateVersionId;

                        // Store to cache
                        var defsCache = {};
                        try {
                            var existing = localStorage.getItem('checklist_defs_cache');
                            if (existing) defsCache = JSON.parse(existing);
                        } catch(e) {}
                        defsCache[key] = res.data;
                        localStorage.setItem('checklist_defs_cache', JSON.stringify(defsCache));

                        vm.checklistItems = res.data.map(function (item) {
                            var parsedRules = null;
                            if (item.ValidationRules) {
                                try {
                                    parsedRules = typeof item.ValidationRules === 'string' ? JSON.parse(item.ValidationRules) : item.ValidationRules;
                                } catch(e) {
                                    console.error(e);
                                }
                            }
                            return {
                                DefinitionId: item.Id,
                                CheckName: item.CheckName,
                                Description: item.Description,
                                IsRequired: item.IsRequired,
                                ValueType: item.ValueType || 'checkbox',
                                Unit: item.Unit || '',
                                ValidationRules: parsedRules,
                                Options: item.Options || [],
                                isPassed: null,
                                numericValue: null,
                                stringValue: item.ValueType === 'select' ? ((item.Options && item.Options.find(function(o) { return o.IsDefault; })) ? item.Options.find(function(o) { return o.IsDefault; }).Value : '') : '',
                                note: ''
                            };
                        });
                    } else {
                        useCachedDefs();
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    useCachedDefs();
                });
        },


        setItemPassed(index, val) {
            var item = this.checklistItems[index];
            item.isPassed = val;
            if (val === true) {
                if (item.ValueType === 'number') {
                    if (item.numericValue === null || item.numericValue === '') {
                        item.numericValue = item.ValidationRules && item.ValidationRules.defaultValue !== undefined ? item.ValidationRules.defaultValue : (item.ValidationRules && item.ValidationRules.min !== undefined ? item.ValidationRules.min : null);
                    }
                } else if (item.ValueType === 'select') {
                    if (item.stringValue === null || item.stringValue === '' || item.stringValue.toLowerCase() === 'fault' || item.stringValue.toLowerCase() === 'lỗi') {
                        var def = item.Options.find(function(o) { return o.IsDefault; });
                        item.stringValue = def ? def.Value : (item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }) ? item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }).Value : '');
                    }
                }
            } else if (val === false) {
                if (item.ValueType === 'select') {
                    var faultOpt = item.Options.find(function(o) { return o.Value.toLowerCase() === 'fault' || o.Value.toLowerCase() === 'lỗi'; });
                    item.stringValue = faultOpt ? faultOpt.Value : (item.Options[0] ? item.Options[0].Value : 'fault');
                }
            }
        },

        onNumericInput(index) {
            var item = this.checklistItems[index];
            var val = parseFloat(item.numericValue);
            if (!isNaN(val)) {
                if (item.ValidationRules) {
                    var rules = item.ValidationRules;
                    var isMinFail = rules.min !== undefined && rules.min !== null && val < rules.min;
                    var isMaxFail = rules.max !== undefined && rules.max !== null && val > rules.max;
                    if (isMinFail || isMaxFail) {
                        item.isPassed = false;
                    } else {
                        item.isPassed = true;
                    }
                } else {
                    item.isPassed = true;
                }
            } else {
                item.isPassed = null;
            }
        },

        onSelectChange(index) {
            var item = this.checklistItems[index];
            if (item.stringValue) {
                var isFault = item.stringValue.toLowerCase() === 'fault' || item.stringValue.toLowerCase() === 'lỗi';
                item.isPassed = !isFault;
            } else {
                item.isPassed = null;
            }
        },

        onTextInput(index) {
            var item = this.checklistItems[index];
            if (item.stringValue && item.stringValue.trim() !== '') {
                item.isPassed = true;
            } else {
                item.isPassed = null;
            }
        },

        bulkSetAllPassed() {
            var hasFailed = this.checklistItems.some(function (item) { return item.isPassed === false; });
            if (hasFailed) {
                if (confirm("⚠️ Phát hiện một số hạng mục đang được đánh dấu là LỖI.\n\nBạn có muốn GIỮ LẠI các hạng mục LỖI này và chỉ tự động điền ĐẠT cho các mục còn lại không?\n(Bấm OK để Giữ lỗi + Điền đạt các mục còn lại. Bấm Cancel để xem tùy chọn Ghi đè TẤT CẢ thành ĐẠT)")) {
                    var updatedCount = 0;
                    this.checklistItems.forEach(function (item) {
                        if (item.isPassed === null) {
                            item.isPassed = true;
                            updatedCount++;
                            if (item.ValueType === 'number' && (item.numericValue === null || item.numericValue === '')) {
                                item.numericValue = item.ValidationRules && item.ValidationRules.defaultValue !== undefined ? item.ValidationRules.defaultValue : (item.ValidationRules && item.ValidationRules.min !== undefined ? item.ValidationRules.min : null);
                            } else if (item.ValueType === 'select') {
                                if (item.stringValue === null || item.stringValue === '' || item.stringValue.toLowerCase() === 'fault' || item.stringValue.toLowerCase() === 'lỗi') {
                                    var def = item.Options.find(function(o) { return o.IsDefault; });
                                    item.stringValue = def ? def.Value : (item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }) ? item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }).Value : '');
                                }
                            }
                        }
                    });
                    this.toast('Thành công', 'Đã đặt kết quả ĐẠT cho các hạng mục còn lại.', 'success');
                    return;
                } else {
                    if (!confirm("⚠️ Bạn có chắc chắn muốn GHI ĐÈ toàn bộ hạng mục (bao gồm cả các hạng mục đang báo LỖI) thành ĐẠT không?")) {
                        return;
                    }
                }
            }
            this.checklistItems.forEach(function (item) {
                item.isPassed = true;
                if (item.ValueType === 'number') {
                    if (item.numericValue === null || item.numericValue === '') {
                        item.numericValue = item.ValidationRules && item.ValidationRules.defaultValue !== undefined ? item.ValidationRules.defaultValue : (item.ValidationRules && item.ValidationRules.min !== undefined ? item.ValidationRules.min : null);
                    }
                } else if (item.ValueType === 'select') {
                    if (item.stringValue === null || item.stringValue === '' || item.stringValue.toLowerCase() === 'fault' || item.stringValue.toLowerCase() === 'lỗi') {
                        var def = item.Options.find(function(o) { return o.IsDefault; });
                        item.stringValue = def ? def.Value : (item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }) ? item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }).Value : '');
                    }
                }
            });
            this.toast('Thành công', 'Đã đặt kết quả ĐẠT/Mặc định cho toàn bộ ' + this.checklistItems.length + ' hạng mục.', 'success');
        },

        markRemainingPassed() {
            var updatedCount = 0;
            this.checklistItems.forEach(function (item) {
                if (item.isPassed === null) {
                    item.isPassed = true;
                    updatedCount++;
                    if (item.ValueType === 'number' && (item.numericValue === null || item.numericValue === '')) {
                        item.numericValue = item.ValidationRules && item.ValidationRules.defaultValue !== undefined ? item.ValidationRules.defaultValue : (item.ValidationRules && item.ValidationRules.min !== undefined ? item.ValidationRules.min : null);
                    } else if (item.ValueType === 'select' && (item.stringValue === null || item.stringValue === '' || item.stringValue.toLowerCase() === 'fault' || item.stringValue.toLowerCase() === 'lỗi')) {
                        var def = item.Options.find(function(o) { return o.IsDefault; });
                        item.stringValue = def ? def.Value : (item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }) ? item.Options.find(function(o) { return o.Value.toLowerCase() !== 'fault' && o.Value.toLowerCase() !== 'lỗi'; }).Value : '');
                    }
                }
            });
            if (updatedCount > 0) {
                this.toast('Thành công', 'Đã đánh giá ĐẠT cho ' + updatedCount + ' hạng mục còn lại.', 'success');
            } else {
                this.toast('Thông tin', 'Không có hạng mục nào chưa được đánh giá.', 'info');
            }
        },

        postSaveChecklist(payload, isQuickPass, sch) {
            var vm = this;

            function queueOffline(payload) {
                var queue = [];
                try {
                    var existing = localStorage.getItem('offlineChecklistQueue');
                    if (existing) queue = JSON.parse(existing);
                } catch(e) {}
                
                var exists = queue.some(function(item) { return item.ScheduleId === payload.ScheduleId; });
                if (!exists) {
                    queue.push(payload);
                    localStorage.setItem('offlineChecklistQueue', JSON.stringify(queue));
                }
                vm.offlineQueueLength = queue.length;

                // Cập nhật schedules cache
                var schIdx = vm.schedules.findIndex(function(s) { return s.Id === payload.ScheduleId; });
                if (schIdx !== -1) {
                    vm.schedules[schIdx].Status = 'done';
                    localStorage.setItem('checklist_schedules_cache', JSON.stringify(vm.schedules));
                }

                // Cập nhật logs cache
                var existsLog = vm.logs.some(function(l) { return l.ScheduleId === payload.ScheduleId; });
                if (!existsLog) {
                    var targetSch = isQuickPass ? sch : vm.activeSchedule;
                    var mockLog = {
                        Id: -Date.now(),
                        AssetCode: targetSch.AssetCode,
                        ItemName: targetSch.ItemName,
                        SerialNumber: targetSch.SerialNumber,
                        DepartmentName: targetSch.DepartmentName,
                        CheckedByName: vm.currentUser.FullName || 'Kỹ thuật viên',
                        CheckedAt: new Date().toISOString().replace('T', ' ').substring(0, 19),
                        CycleType: payload.CycleType,
                        OverallResult: payload.OverallResult,
                        ApprovalStatus: payload.OverallResult === 'pass' ? 'Approved' : 'Pending'
                    };
                    vm.logs.unshift(mockLog);
                    localStorage.setItem('checklist_logs_cache', JSON.stringify(vm.logs));
                }

                vm.calculateKPIs();
                if (!isQuickPass) {
                    vm.performForm.submitted = true;
                }
                vm.isSubmitting = false;
                if (isQuickPass) {
                    vm.$set(sch, '_isQuickPassing', false);
                }
                vm.toast('Đã lưu ngoại tuyến', 'Mất kết nối máy chủ. Checklist đã được lưu ngoại tuyến thành công.', 'warning');
            }

            if (!vm.isOnline) {
                queueOffline(payload);
                return;
            }

            fetch('/api/checklists/save', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(payload)
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isSubmitting = false;
                if (isQuickPass) {
                    vm.$set(sch, '_isQuickPassing', false);
                }
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    if (!isQuickPass) {
                        vm.lastSubmitLogId = res.logId;
                        vm.lastSubmitResult = payload.OverallResult;
                        vm.performForm.submitted = true;
                    }
                    vm.loadSchedules();
                    vm.loadLogs();
                } else {
                    vm.toast('Lỗi', res.message, 'danger');
                }
            })
            .catch(function (err) {
                console.error(err);
                queueOffline(payload);
            });
        },

        submitChecklist() {
            var vm = this;

            if (!vm.checklistItems || vm.checklistItems.length === 0) {
                vm.toast('Cảnh báo', 'Không có hạng mục kiểm tra nào trong biểu mẫu. Vui lòng thiết lập mẫu checklist trước khi thực hiện.', 'warning');
                return;
            }

            // 1. Dynamic validation
            var missing = [];
            var missingNotes = [];
            var hasValidationErrors = false;

            vm.checklistItems.forEach(function (item) {
                var valueProvided = false;
                if (item.ValueType === 'checkbox') {
                    valueProvided = item.isPassed !== null;
                } else if (item.ValueType === 'number') {
                    valueProvided = item.numericValue !== null && item.numericValue !== '';
                } else if (item.ValueType === 'select') {
                    valueProvided = item.stringValue !== null && item.stringValue !== '';
                } else {
                    valueProvided = item.stringValue !== null && item.stringValue.trim() !== '';
                }

                if (item.IsRequired && !valueProvided) {
                    missing.push(item);
                }

                // Check numeric min/max limits
                if (item.ValueType === 'number' && valueProvided) {
                    var val = parseFloat(item.numericValue);
                    if (isNaN(val)) {
                        vm.toast('Cảnh báo', item.CheckName + ' bắt buộc phải là một số hợp lệ.', 'warning');
                        hasValidationErrors = true;
                    } else if (item.ValidationRules) {
                        var rules = item.ValidationRules;
                        if (rules.min !== undefined && rules.min !== null && val < rules.min) {
                            vm.toast('Cảnh báo', rules.customMessage || (item.CheckName + ' không được thấp hơn ' + rules.min + ' ' + item.Unit), 'warning');
                            hasValidationErrors = true;
                        }
                        if (rules.max !== undefined && rules.max !== null && val > rules.max) {
                            vm.toast('Cảnh báo', rules.customMessage || (item.CheckName + ' không được vượt quá ' + rules.max + ' ' + item.Unit), 'warning');
                            hasValidationErrors = true;
                        }
                    }
                }

                // Boolean fails require notes
                if (item.ValueType === 'checkbox' && item.isPassed === false && (!item.note || !item.note.trim())) {
                    missingNotes.push(item);
                }
                
                // Dropdown failure check (if user selects options containing "Lỗi" or "fault")
                if (item.ValueType === 'select' && valueProvided) {
                    var isFaultOption = item.stringValue.toLowerCase() === 'fault' || item.stringValue.toLowerCase() === 'lỗi';
                    if (isFaultOption && (!item.note || !item.note.trim())) {
                        missingNotes.push(item);
                    }
                }
            });

            if (hasValidationErrors) return;

            if (missing.length > 0) {
                vm.toast('Cảnh báo', 'Vui lòng điền đầy đủ các hạng mục bắt buộc (*) trước khi lưu.', 'warning');
                return;
            }

            if (missingNotes.length > 0) {
                vm.toast('Cảnh báo', 'Vui lòng nhập ghi chú mô tả lỗi cho các hạng mục phát hiện lỗi/sự cố.', 'warning');
                return;
            }

            // 2. Determine Overall Result (fail if any item is marked false/lỗi or select is 'fault')
            var hasFailed = vm.checklistItems.some(function (item) {
                if (item.isPassed === false) return true;
                if (item.ValueType === 'select' && (item.stringValue === 'fault' || item.stringValue === 'lỗi')) return true;
                return false;
            });
            var overall = hasFailed ? 'fail' : 'pass';

            vm.isSubmitting = true;

            var payload = {
                ScheduleId: vm.activeSchedule.Id,
                InventoryId: vm.activeSchedule.InventoryId || null,
                LocationId: vm.activeSchedule.LocationId || null,
                TemplateVersionId: vm.activeScheduleTemplateVersionId || null,
                CheckedBy: vm.currentUser.Id,
                CycleType: vm.activeSchedule.CycleType,
                OverallResult: overall,
                Note: vm.performForm.note,
                QrScanned: vm.performForm.qrScanned,
                QrLocation: vm.activeSchedule.LocationName || vm.activeSchedule.DepartmentName || '',
                ImageUrls: '',
                Items: vm.checklistItems.map(function (i) {
                    var passVal = i.isPassed !== false;
                    if (i.ValueType === 'select') {
                        passVal = passVal && i.stringValue !== 'fault' && i.stringValue !== 'lỗi';
                    }
                    return {
                        DefinitionId: i.DefinitionId,
                        IsPassed: passVal,
                        NumericValue: i.numericValue !== '' && i.numericValue !== null ? i.numericValue : null,
                        StringValue: i.stringValue || null,
                        Note: i.note
                    };
                })
            };

            vm.postSaveChecklist(payload, false, null);
        },

        openDetailsModal(logId) {
            var vm = this;
            vm.logDetails = null;
            vm.showDetailsModal = true;

            fetch('/api/checklists/log-details?logId=' + logId)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.logDetails = res.data;
                    } else {
                        vm.toast('Lỗi', res.message, 'danger');
                        vm.showDetailsModal = false;
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi', 'Lỗi tải chi tiết nhật ký.', 'danger');
                    vm.showDetailsModal = false;
                });
        },

        createTicketFromChecklist(logId) {
            window.location.href = '/CreateTicket/Index?fromChecklist=1&logId=' + logId;
        },

        applyYesterdayResults() {
            var vm = this;
            if (!vm.recentInspectionHistory || vm.recentInspectionHistory.length === 0) {
                vm.toast('Thông báo', 'Không có lịch sử kiểm tra trước đó.', 'info');
                return;
            }
            var lastLog = vm.recentInspectionHistory[0];
            
            vm.performLoading = true;
            fetch('/api/checklists/log-details?logId=' + lastLog.Id)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success && res.data && res.data.Items) {
                        var prevItems = res.data.Items;
                        vm.checklistItems.forEach(function (currentItem) {
                            var prev = prevItems.find(function (p) {
                                return p.DefinitionId === currentItem.DefinitionId || p.CheckName === currentItem.CheckName;
                            });
                            if (prev) {
                                currentItem.isPassed = prev.IsPassed;
                                currentItem.note = prev.Note;
                            }
                        });
                        vm.toast('Thành công', 'Đã sao chép kết quả từ lần kiểm tra trước (' + lastLog.CheckedAt + ').', 'success');
                    } else {
                        vm.toast('Lỗi', 'Không thể lấy chi tiết kết quả lần trước.', 'danger');
                    }
                    vm.performLoading = false;
                })
                .catch(function (err) {
                    console.error(err);
                    vm.performLoading = false;
                    vm.toast('Lỗi', 'Lỗi kết nối máy chủ.', 'danger');
                });
        },

        redirectToCreateTicketDirectly() {
            var vm = this;
            if (vm.activeSchedule && vm.activeSchedule.AssetCode) {
                window.location.href = '/CreateTicket/Index?ticketType=REPAIR&assetCode=' + encodeURIComponent(vm.activeSchedule.AssetCode);
            } else {
                window.location.href = '/CreateTicket/Index';
            }
        },

        // ── LABELS & HELPERS ──
        cycleLabel(type) {
            switch (type) {
                case 'daily': return 'Hàng ngày';
                case 'weekly': return 'Hàng tuần';
                case 'monthly': return 'Hàng tháng';
                case 'quarterly': return 'Hàng quý';
                case 'yearly': return 'Hàng năm';
                default: return type || 'Không định kỳ';
            }
        },

        statusLabel(status) {
            switch (status) {
                case 'pending': return 'Chờ thực hiện';
                case 'overdue': return 'Quá hạn';
                case 'completed': return 'Đã xong';
                case 'done': return 'Đã xong';
                case 'skipped': return 'Bỏ qua';
                case 'NeedsReinspection': return 'Cần kiểm tra lại';
                default: return status;
            }
        },

        resultLabel(res) {
            switch (res) {
                case 'pass': return 'Đạt chuẩn';
                case 'fail': return 'Không đạt';
                case 'partial': return 'Đạt một phần';
                default: return res;
            }
        },

        getLocalTodayStr() {
            var d = new Date();
            var year = d.getFullYear();
            var month = ('0' + (d.getMonth() + 1)).slice(-2);
            var day = ('0' + d.getDate()).slice(-2);
            return year + '-' + month + '-' + day;
        },

        isOverdue(dueDateStr) {
            var todayStr = this.getLocalTodayStr();
            return dueDateStr < todayStr;
        },

        toast(title, msg, type) {
            if (window.MedEquip && typeof window.MedEquip.toast === 'function') {
                window.MedEquip.toast(title, msg, type);
            } else {
                alert(title + ': ' + msg);
            }
        },

        // ── MANAGER & OFFLINE HELPER METHODS ──
        isManager() {
            if (!this.currentUser) return false;
            if (this.currentUser.Id === 1) return true;
            if (!this.currentUser.roles) return false;
            var checkRoles = ['admin', 'manager', 'approver'];
            return this.currentUser.roles.some(function (r) {
                return checkRoles.indexOf(r.toLowerCase()) > -1;
            });
        },

        loadDepartmentProgress() {
            var vm = this;
            var fromStr = vm.complianceFilter.fromDate;
            var toStr = vm.complianceFilter.toDate;
            fetch('/api/checklists/department-progress?fromDate=' + fromStr + '&toDate=' + toStr)
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        vm.departmentCompliance = res.data;
                    } else {
                        vm.toast('Lỗi', res.message, 'danger');
                    }
                })
                .catch(function (err) {
                    console.error(err);
                    vm.toast('Lỗi mạng', 'Không thể tải tiến độ khoa phòng.', 'danger');
                });
        },

        loadPendingApprovals() {
            var vm = this;
            fetch('/api/checklists/logs?approvalStatus=Pending')
                 .then(function (r) { return r.json(); })
                 .then(function (res) {
                     if (res.success) {
                         vm.pendingApprovals = res.data;
                         vm.selectedLogIds = [];
                     } else {
                         vm.toast('Lỗi', res.message, 'danger');
                     }
                 })
                 .catch(function (err) {
                     console.error(err);
                     vm.toast('Lỗi mạng', 'Không thể tải danh sách chờ duyệt.', 'danger');
                 });
        },

        approveSelectedLogs(status) {
            var vm = this;
            if (status !== 'Approved' && status !== 'Rejected') {
                status = 'Approved';
            }
            if (vm.selectedLogIds.length === 0) {
                vm.toast('Cảnh báo', 'Vui lòng chọn ít nhất một nhật ký.', 'warning');
                return;
            }
            if (!confirm(`Bạn có chắc muốn ${status === 'Approved' ? 'Duyệt' : 'Từ chối'} ${vm.selectedLogIds.length} bản ghi?`)) return;
            
            vm.isApproving = true;
            fetch('/api/checklists/approve-multiple', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ logIds: vm.selectedLogIds, status: status })
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isApproving = false;
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    vm.loadPendingApprovals();
                    vm.loadDepartmentProgress();
                    vm.loadOperationalKPIs();
                    vm.loadLogs();
                    vm.loadSchedules();
                } else {
                    vm.toast('Lỗi', res.message, 'danger');
                }
            })
            .catch(function (err) {
                vm.isApproving = false;
                console.error(err);
                vm.toast('Lỗi', 'Không thể kết nối máy chủ.', 'danger');
            });
        },

        approveSingleLog(logId, status) {
            var vm = this;
            if (status !== 'Approved' && status !== 'Rejected') {
                status = 'Approved';
            }
            if (!confirm(`Bạn có chắc muốn ${status === 'Approved' ? 'Duyệt' : 'Từ chối'} bản ghi này?`)) return;
            vm.isApproving = true;
            fetch('/api/checklists/approve-multiple', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ logIds: [logId], status: status })
            })
            .then(function (r) { return r.json(); })
            .then(function (res) {
                vm.isApproving = false;
                if (res.success) {
                    vm.toast('Thành công', res.message, 'success');
                    vm.loadPendingApprovals();
                    vm.loadDepartmentProgress();
                    vm.loadOperationalKPIs();
                    vm.loadLogs();
                    vm.loadSchedules();
                } else {
                    vm.toast('Lỗi', res.message, 'danger');
                }
            })
            .catch(function (err) {
                vm.isApproving = false;
                console.error(err);
                vm.toast('Lỗi', 'Không thể kết nối máy chủ.', 'danger');
            });
        },

        loadOperationalKPIs() {
            var vm = this;
            fetch('/api/checklists/operational-kpis')
                .then(r => r.json())
                .then(res => {
                    if (res.success) {
                        vm.operationalKpis = res.data;
                    }
                })
                .catch(err => console.error(err));
        },

        createRepairTicket(log) {
            var url = '/CreateTicket/Index?fromChecklist=1&logId=' + log.Id;
            window.open(url, '_blank');
        },

        toggleSelectAll(event) {
            var vm = this;
            if (event.target.checked) {
                vm.selectedLogIds = vm.pendingApprovals.map(function (log) { return log.Id; });
            } else {
                vm.selectedLogIds = [];
            }
        },

        syncOfflineQueue() {
            var vm = this;
            if (vm.isSyncing) return;
            var queue = [];
            try {
                var existing = localStorage.getItem('offlineChecklistQueue');
                if (existing) queue = JSON.parse(existing);
            } catch (e) { /* ignore */ }

            if (queue.length === 0) {
                vm.offlineQueueLength = 0;
                return;
            }

            vm.isSyncing = true;
            var syncIndex = 0;

            function syncNext() {
                if (syncIndex >= queue.length) {
                    localStorage.removeItem('offlineChecklistQueue');
                    vm.offlineQueueLength = 0;
                    vm.isSyncing = false;
                    vm.toast('Đồng bộ thành công', 'Đã đồng bộ toàn bộ checklists ngoại tuyến lên máy chủ.', 'success');
                    vm.loadSchedules();
                    vm.loadLogs();
                    if (vm.isManager()) {
                        vm.loadPendingApprovals();
                        vm.loadDepartmentProgress();
                    }
                    return;
                }

                var payload = queue[syncIndex];
                fetch('/api/checklists/save', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify(payload)
                })
                .then(function (r) { return r.json(); })
                .then(function (res) {
                    if (res.success) {
                        syncIndex++;
                        syncNext();
                    } else {
                        vm.isSyncing = false;
                        vm.toast('Đồng bộ thất bại', 'Lỗi khi gửi checklist cho thiết bị ID ' + payload.InventoryId + ': ' + res.message, 'danger');
                    }
                })
                .catch(function (err) {
                    vm.isSyncing = false;
                    console.error(err);
                    vm.toast('Lỗi đồng bộ', 'Không thể kết nối máy chủ để đồng bộ.', 'danger');
                });
            }

            syncNext();
        },

        switchTab(tab) {
            this.activeTab = tab;
            if (tab === 'schedules') this.loadSchedules();
            if (tab === 'logs') this.loadLogs();
            if (tab === 'manager_approvals') {
                this.loadDepartmentProgress();
                this.loadPendingApprovals();
                this.loadOperationalKPIs();
            }
        }
    },

    mounted() {
        var vm = this;
        var userStr = localStorage.getItem('current_user');
        if (userStr) {
            try {
                var cu = JSON.parse(userStr);
                if (cu && cu.Id) vm.currentUser = cu;
            } catch (e) { /* ignore */ }
        }

        var now = new Date();
        var y = now.getFullYear();
        var m = now.getMonth();
        var fromDate = new Date(y, m, 1);
        var toDate = new Date(y, m + 1, 0); // ngày cuối tháng
        vm.generateForm = {
            fromDate: fromDate.toISOString().substring(0, 10),
            toDate: toDate.toISOString().substring(0, 10)
        };

        // Initialize complianceFilter dates
        vm.complianceFilter = {
            fromDate: fromDate.toISOString().substring(0, 10),
            toDate: toDate.toISOString().substring(0, 10)
        };

        // Load offline queue length from localStorage
        try {
            var existing = localStorage.getItem('offlineChecklistQueue');
            vm.offlineQueueLength = existing ? JSON.parse(existing).length : 0;
        } catch (e) { vm.offlineQueueLength = 0; }

        // Setup online/offline event listeners
        window.addEventListener('online', function () {
            vm.isOnline = true;
            vm.toast('Kết nối lại', 'Kết nối Internet đã được khôi phục.', 'success');
            vm.syncOfflineQueue();
        });
        window.addEventListener('offline', function () {
            vm.isOnline = false;
            vm.toast('Mất kết nối', 'Bạn đang ngoại tuyến (Offline).', 'danger');
        });

        var todayStr = now.toISOString().substring(0, 10);
        vm.schedulesFilter.fromDate = todayStr;
        vm.schedulesFilter.toDate = todayStr;
        
        vm.loadSchedules();
        vm.loadLogs();

        // Redirection & Today's Focus default
        if (vm.isManager()) {
            vm.activeTab = 'manager_approvals';
            vm.loadDepartmentProgress();
            vm.loadPendingApprovals();
            vm.loadOperationalKPIs();
        } else {
            vm.activeTab = 'schedules';
        }

        vm.loadActiveGroups();
    }
});
