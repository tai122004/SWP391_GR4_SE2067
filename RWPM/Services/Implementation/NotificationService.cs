using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Common.Models;
using RWPM.Hubs;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Notification;
using RWPM.Services.Abstraction;
using NR = RWPM.Resources.Shared.NotificationResource;

namespace RWPM.Services.Implementation
{
    /// <summary>
    /// Thông báo trong ứng dụng.
    /// - Nội dung lưu dạng Type + ArgsJson, dịch sang ngôn ngữ người xem lúc hiển thị (Render).
    /// - Ghi dữ liệu bằng DbContext riêng (scope mới) để không dính vào change tracker/transaction của nghiệp vụ chính.
    /// - Mọi lỗi khi gửi thông báo chỉ ghi log, không làm hỏng thao tác chính.
    /// </summary>
    public class NotificationService : INotificationService
    {
        public const string HubEventNew = "Notification_New";
        public const string HubEventCount = "Notification_Count";

        private const int MaxArgsJsonLength = 1000;
        private const int MaxUrlLength = 300;
        private const int MaxUsernameLength = 30;

        private static readonly JsonSerializerOptions ArgsJsonOptions = new()
        {
            // Giữ nguyên ký tự tiếng Việt (không escape \uXXXX) để không vượt giới hạn 1000 ký tự
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly DefaultDatabaseContext _context;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IHubContext<ApplicationHub> _hubContext;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            DefaultDatabaseContext context,
            IServiceScopeFactory scopeFactory,
            IHubContext<ApplicationHub> hubContext,
            ILogger<NotificationService> logger)
        {
            _context = context;
            _scopeFactory = scopeFactory;
            _hubContext = hubContext;
            _logger = logger;
        }

        #region Gửi thông báo

        public Task NotifyAsync(string username, string type, IEnumerable<string?>? args = null, string? url = null)
            => NotifyAsync(new[] { username }, type, args, url);

        public async Task NotifyAsync(IEnumerable<string> usernames, string type, IEnumerable<string?>? args = null, string? url = null)
        {
            try
            {
                var names = (usernames ?? Enumerable.Empty<string>())
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Select(u => u.Trim())
                    .Where(u => u.Length <= MaxUsernameLength)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (names.Count == 0 || string.IsNullOrWhiteSpace(type) || type.Length > 50) return;

                var argsJson = SerializeArgs(args);
                var safeUrl = SanitizeUrl(url);

                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<DefaultDatabaseContext>();

                // Chỉ gửi cho tài khoản còn tồn tại và đang hoạt động (tránh lỗi khóa ngoại)
                var recipients = await db.Acc.AsNoTracking()
                    .Where(a => a.IsActive && names.Contains(a.Username))
                    .Select(a => a.Username)
                    .ToListAsync();
                if (recipients.Count == 0) return;

                var now = DateTime.Now;
                var rows = recipients.Select(u => new Notification
                {
                    Username = u,
                    Type = type,
                    ArgsJson = argsJson,
                    Url = safeUrl,
                    IsRead = false,
                    CreatedDate = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, now.Second)
                }).ToList();

                db.Notification.AddRange(rows);
                await db.SaveChangesAsync();

                await PushNewAsync(db, rows);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create notification {Type}", type);
            }
        }

        private async Task PushNewAsync(DefaultDatabaseContext db, List<Notification> rows)
        {
            try
            {
                var names = rows.Select(r => r.Username).Distinct().ToList();
                var counts = await db.Notification.AsNoTracking()
                    .Where(n => !n.IsRead && names.Contains(n.Username))
                    .GroupBy(n => n.Username)
                    .Select(g => new { Username = g.Key, Count = g.Count() })
                    .ToListAsync();
                var countMap = counts.ToDictionary(x => x.Username, x => x.Count, StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    // CustomUserIdProvider dùng Identity.Name (= Acc.Username) làm user id của SignalR.
                    // Chỉ gửi id + số chưa đọc: client tự tải lại danh sách để hiển thị đúng ngôn ngữ của người xem.
                    await _hubContext.Clients.User(row.Username).SendAsync(HubEventNew, new
                    {
                        id = row.NotificationId,
                        type = row.Type,
                        unreadCount = countMap.TryGetValue(row.Username, out var c) ? c : 1
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push realtime notification");
            }
        }

        private async Task PushCountAsync(string username, int unreadCount)
        {
            try
            {
                // Đồng bộ badge giữa các tab đang mở của cùng người dùng
                await _hubContext.Clients.User(username).SendAsync(HubEventCount, new { unreadCount });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to push notification count");
            }
        }

        private static string? SerializeArgs(IEnumerable<string?>? args)
        {
            if (args == null) return null;
            var list = args.Select(a => (a ?? string.Empty).Trim()).ToList();
            if (list.Count == 0) return null;

            int limit = 400;
            string json = JsonSerializer.Serialize(Truncate(list, limit), ArgsJsonOptions);
            while (json.Length > MaxArgsJsonLength && limit > 20)
            {
                limit -= 40;
                json = JsonSerializer.Serialize(Truncate(list, limit), ArgsJsonOptions);
            }
            return json.Length > MaxArgsJsonLength ? null : json;
        }

        private static List<string> Truncate(List<string> list, int limit)
            => list.Select(a => a.Length > limit ? a.Substring(0, limit - 1) + "…" : a).ToList();

        private static string? SanitizeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            url = url.Trim();
            // Chỉ cho phép đường dẫn nội bộ ("/..."), chặn "//host" và "/\host"
            if (!url.StartsWith('/') || url.StartsWith("//") || url.StartsWith("/\\")) return null;
            return url.Length > MaxUrlLength ? null : url;
        }

        #endregion

        #region Đọc / đánh dấu

        public async Task<List<NotificationItemVM>> GetLatestAsync(string username, int take = 10)
        {
            take = Math.Clamp(take, 1, 50);
            var rows = await _context.Notification.AsNoTracking()
                .Where(n => n.Username == username)
                .OrderByDescending(n => n.CreatedDate)
                .ThenByDescending(n => n.NotificationId)
                .Take(take)
                .ToListAsync();
            return rows.Select(Render).ToList();
        }

        public Task<int> GetUnreadCountAsync(string username)
            => _context.Notification.AsNoTracking().CountAsync(n => n.Username == username && !n.IsRead);

        public async Task<NotificationItemVM?> MarkReadAsync(long id, string username)
        {
            var entity = await _context.Notification
                .FirstOrDefaultAsync(n => n.NotificationId == id && n.Username == username);
            if (entity == null) return null;

            if (!entity.IsRead)
            {
                entity.IsRead = true;
                entity.ReadAt = DateTime.Now;
                await _context.SaveChangesAsync();
                await PushCountAsync(username, await GetUnreadCountAsync(username));
            }
            return Render(entity);
        }

        public async Task<int> MarkAllReadAsync(string username)
        {
            var unread = await _context.Notification
                .Where(n => n.Username == username && !n.IsRead)
                .ToListAsync();
            if (unread.Count == 0) return 0;

            var now = DateTime.Now;
            foreach (var n in unread)
            {
                n.IsRead = true;
                n.ReadAt = now;
            }
            await _context.SaveChangesAsync();
            await PushCountAsync(username, 0);
            return unread.Count;
        }

        public async Task<NotificationListVM> GetPagedAsync(string username, bool unreadOnly, int pageNumber, int pageSize = 20)
        {
            pageSize = Math.Clamp(pageSize, 1, 50);
            var query = _context.Notification.AsNoTracking().Where(n => n.Username == username);
            if (unreadOnly) query = query.Where(n => !n.IsRead);

            var total = await query.CountAsync();
            int totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            pageNumber = Math.Clamp(pageNumber, 1, totalPages);

            var rows = await query
                .OrderByDescending(n => n.CreatedDate)
                .ThenByDescending(n => n.NotificationId)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();
            var items = rows.Select(Render).ToList();

            return new NotificationListVM
            {
                Items = items,
                UnreadOnly = unreadOnly,
                TotalCount = total,
                UnreadCount = await GetUnreadCountAsync(username),
                Pagination = new PaginationRes<NotificationItemVM>(items, pageNumber, pageSize, total)
            };
        }

        #endregion

        #region Render (dịch theo ngôn ngữ người xem)

        public static NotificationItemVM Render(Notification n)
        {
            var args = ParseArgs(n.ArgsJson);
            string A(int i) => i < args.Length ? args[i] : string.Empty;

            string title, message, icon, color;
            switch (n.Type)
            {
                case NotificationTypes.RequestApproved:
                    title = NR.Title_RequestApproved;
                    message = Format(NR.Body_RequestApproved, RequestTypeName(A(0)), A(1), A(2));
                    icon = "bi-check-circle-fill"; color = "success";
                    break;
                case NotificationTypes.RequestRejected:
                    title = NR.Title_RequestRejected;
                    var reason = A(3);
                    if (reason == AttendanceAdjustmentService.DefaultRejectionReason)
                        reason = RWPM.Resources.Shared.RequestResource.Adj_DefaultRejectReason;
                    message = string.IsNullOrWhiteSpace(reason)
                        ? Format(NR.Body_RequestRejectedNoReason, RequestTypeName(A(0)), A(1), A(2))
                        : Format(NR.Body_RequestRejected, RequestTypeName(A(0)), A(1), A(2), reason);
                    icon = "bi-x-circle-fill"; color = "danger";
                    break;
                case NotificationTypes.SwapCoworkerApproved:
                    title = NR.Title_SwapCoworkerApproved;
                    message = Format(NR.Body_SwapCoworkerApproved, A(0), A(1), A(2));
                    icon = "bi-arrow-left-right"; color = "info";
                    break;
                case NotificationTypes.RequestSubmitted:
                    title = NR.Title_RequestSubmitted;
                    message = Format(NR.Body_RequestSubmitted, RequestTypeName(A(0)), A(1), A(2), A(3));
                    icon = "bi-inbox-fill"; color = "warning";
                    break;
                case NotificationTypes.ShiftRegSubmittedBatch:
                    title = NR.Title_ShiftRegSubmittedBatch;
                    message = Format(NR.Body_ShiftRegSubmittedBatch, A(0), A(1), A(2));
                    icon = "bi-calendar-plus"; color = "warning";
                    break;
                case NotificationTypes.PayslipAvailable:
                    title = NR.Title_PayslipAvailable;
                    message = Format(NR.Body_PayslipAvailable, A(0), A(1));
                    icon = "bi-cash-stack"; color = "primary";
                    break;
                default:
                    title = NR.Title_Default;
                    message = string.Join(" ", args.Where(a => !string.IsNullOrWhiteSpace(a)));
                    icon = "bi-bell-fill"; color = "secondary";
                    break;
            }

            var age = (long)Math.Max(0, (DateTime.Now - n.CreatedDate).TotalSeconds);
            return new NotificationItemVM
            {
                Id = n.NotificationId,
                Type = n.Type,
                Title = title,
                Message = message.Replace(" ()", string.Empty),
                Url = SanitizeUrl(n.Url),
                IsRead = n.IsRead,
                CreatedDate = n.CreatedDate,
                CreatedDateStr = n.CreatedDate.ToString("dd/MM/yyyy HH:mm"),
                AgeSeconds = age,
                RelativeTime = RelativeText(age, n.CreatedDate),
                Icon = icon,
                Color = color
            };
        }

        public static string RelativeText(long ageSeconds, DateTime created)
        {
            if (ageSeconds < 60) return NR.Rel_JustNow;
            if (ageSeconds < 3600) return Format(NR.Rel_MinutesAgo, ageSeconds / 60);
            if (ageSeconds < 86400) return Format(NR.Rel_HoursAgo, ageSeconds / 3600);
            if (ageSeconds < 7 * 86400) return Format(NR.Rel_DaysAgo, ageSeconds / 86400);
            return created.ToString("dd/MM/yyyy HH:mm");
        }

        private static string RequestTypeName(string key) => key switch
        {
            NotificationRequestTypes.ShiftReg => NR.ReqType_ShiftReg,
            NotificationRequestTypes.ShiftSwap => NR.ReqType_ShiftSwap,
            NotificationRequestTypes.Leave => NR.ReqType_Leave,
            NotificationRequestTypes.Attendance => NR.ReqType_Attendance,
            _ => NR.ReqType_Default
        };

        private static string Format(string? pattern, params object?[] args)
        {
            if (string.IsNullOrEmpty(pattern)) return string.Join(" ", args);
            try { return string.Format(CultureInfo.CurrentCulture, pattern, args); }
            catch (FormatException) { return pattern; }
        }

        private static string[] ParseArgs(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try
            {
                return JsonSerializer.Deserialize<string?[]>(json)?.Select(a => a ?? string.Empty).ToArray() ?? Array.Empty<string>();
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        #endregion

        #region Người duyệt

        public Task<List<string>> GetApproverUsernamesAsync(int storeId, string? requesterUsername, bool adminOnly = false)
            => GetApproverUsernamesAsync(_context, storeId, requesterUsername, adminOnly);

        private static async Task<List<string>> GetApproverUsernamesAsync(DefaultDatabaseContext db, int storeId, string? requesterUsername, bool adminOnly)
        {
            var result = new List<string>();
            if (!adminOnly)
            {
                var storeManagers = await db.Employee.AsNoTracking()
                    .Where(e => e.StoreId == storeId && e.IsActive && e.Account.IsActive && e.Account.Role == AccountRole.StoreManager)
                    .Select(e => e.Username)
                    .ToListAsync();
                var hr = await db.Acc.AsNoTracking()
                    .Where(a => a.IsActive && a.Role == AccountRole.HR)
                    .Select(a => a.Username)
                    .ToListAsync();
                result.AddRange(storeManagers);
                result.AddRange(hr);

                // Đơn của HR: HR khác hoặc Admin duyệt
                if (!string.IsNullOrEmpty(requesterUsername)
                    && await db.Acc.AsNoTracking().AnyAsync(a => a.Username == requesterUsername && a.Role == AccountRole.HR))
                {
                    result.AddRange(await AdminsAsync(db));
                }
            }

            result = result
                .Where(u => !string.Equals(u, requesterUsername, StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (adminOnly || result.Count == 0)
            {
                result = (await AdminsAsync(db))
                    .Where(u => !string.Equals(u, requesterUsername, StringComparison.OrdinalIgnoreCase))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            return result;
        }

        private static Task<List<string>> AdminsAsync(DefaultDatabaseContext db)
            => db.Acc.AsNoTracking().Where(a => a.IsActive && a.Role == AccountRole.Admin).Select(a => a.Username).ToListAsync();

        #endregion

        #region Điểm kích hoạt theo nghiệp vụ

        private sealed record RequestInfo(
            string RequesterUsername,
            string RequesterName,
            int StoreId,
            AccountRole? RequesterRole,
            RegistrationStatus Status,
            string Code,
            string Detail,
            string? RejectionReason,
            DateTime RangeStart,
            DateTime RangeEnd,
            string? CoworkerUsername);

        public static string BuildRequestCode(string requestType, DateTime createdDate, int id)
        {
            var prefix = requestType switch
            {
                NotificationRequestTypes.ShiftReg => "YCCL",
                NotificationRequestTypes.ShiftSwap => "DCCL",
                NotificationRequestTypes.Leave => "DXN",
                NotificationRequestTypes.Attendance => "YCDC",
                _ => "REQ"
            };
            return prefix + createdDate.ToString("yyMMdd") + "-" + id.ToString("D3");
        }

        private static string RequestUrl(string requestType, string? code, DateTime start, DateTime end, bool pendingOnly = false)
        {
            var url = "/Request/Index?start=" + start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + "&end=" + end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                + "&type=" + Uri.EscapeDataString(requestType);
            if (pendingOnly) url += "&status=" + (int)RegistrationStatus.Pending;
            if (!string.IsNullOrEmpty(code)) url += "&q=" + Uri.EscapeDataString(code);
            return url;
        }

        private static string DateRange(DateTime start, DateTime end)
            => start.Date == end.Date ? start.ToString("dd/MM/yyyy") : $"{start:dd/MM/yyyy} – {end:dd/MM/yyyy}";

        private static async Task<List<(int Id, RequestInfo Info)>> LoadRequestsAsync(DefaultDatabaseContext db, string requestType, List<int> ids)
        {
            var result = new List<(int, RequestInfo)>();
            switch (requestType)
            {
                case NotificationRequestTypes.ShiftReg:
                    {
                        var rows = await db.ShiftRegistration.AsNoTracking()
                            .Include(x => x.Employee).ThenInclude(e => e.Account)
                            .Include(x => x.Shift)
                            .Where(x => ids.Contains(x.ShiftRegistrationId))
                            .ToListAsync();
                        foreach (var r in rows)
                        {
                            result.Add((r.ShiftRegistrationId, new RequestInfo(
                                r.Employee?.Username ?? string.Empty,
                                r.Employee?.Account?.FullName ?? r.Employee?.Username ?? string.Empty,
                                r.StoreId,
                                r.Employee?.Account?.Role,
                                r.Status,
                                BuildRequestCode(requestType, r.CreatedDate, r.ShiftRegistrationId),
                                $"{r.Shift?.ShiftName} {r.WorkDate:dd/MM/yyyy}".Trim(),
                                r.RejectionReason,
                                r.WorkDate.Date, r.WorkDate.Date,
                                null)));
                        }
                        break;
                    }
                case NotificationRequestTypes.ShiftSwap:
                    {
                        var rows = await db.ShiftSwapRequest.AsNoTracking()
                            .Include(x => x.RequesterEmployee).ThenInclude(e => e.Account)
                            .Include(x => x.TargetEmployee).ThenInclude(e => e!.Account)
                            .Include(x => x.TargetShift)
                            .Where(x => ids.Contains(x.ShiftSwapRequestId))
                            .ToListAsync();
                        foreach (var s in rows)
                        {
                            // Không dùng SourceRegistration vì sau khi duyệt ca nguồn đã bị đổi
                            string detail = s.TargetEmployee != null
                                ? (s.TargetEmployee.Account?.FullName ?? s.TargetEmployee.Username)
                                : $"→ {s.TargetShift?.ShiftName} {s.TargetWorkDate:dd/MM/yyyy}".Trim();
                            result.Add((s.ShiftSwapRequestId, new RequestInfo(
                                s.RequesterEmployee?.Username ?? string.Empty,
                                s.RequesterEmployee?.Account?.FullName ?? s.RequesterEmployee?.Username ?? string.Empty,
                                s.StoreId,
                                s.RequesterEmployee?.Account?.Role,
                                s.Status,
                                BuildRequestCode(requestType, s.CreatedDate, s.ShiftSwapRequestId),
                                detail,
                                s.RejectionReason,
                                // Danh sách đổi ca lọc theo ngày tạo đơn
                                s.CreatedDate.Date, s.CreatedDate.Date,
                                s.TargetEmployee?.Username)));
                        }
                        break;
                    }
                case NotificationRequestTypes.Leave:
                    {
                        var rows = await db.LeaveRequest.AsNoTracking()
                            .Include(x => x.Employee).ThenInclude(e => e.Account)
                            .Where(x => ids.Contains(x.LeaveRequestId))
                            .ToListAsync();
                        foreach (var l in rows)
                        {
                            result.Add((l.LeaveRequestId, new RequestInfo(
                                l.Employee?.Username ?? string.Empty,
                                l.Employee?.Account?.FullName ?? l.Employee?.Username ?? string.Empty,
                                l.StoreId,
                                l.Employee?.Account?.Role,
                                l.Status,
                                BuildRequestCode(requestType, l.CreatedDate, l.LeaveRequestId),
                                DateRange(l.StartDate, l.EndDate),
                                l.RejectionReason,
                                l.StartDate.Date, l.EndDate.Date,
                                null)));
                        }
                        break;
                    }
                case NotificationRequestTypes.Attendance:
                    {
                        var rows = await db.AttendanceAdjustmentRequest.AsNoTracking()
                            .Include(x => x.Employee).ThenInclude(e => e.Account)
                            .Include(x => x.Shift)
                            .Where(x => ids.Contains(x.AttendanceAdjustmentRequestId))
                            .ToListAsync();
                        foreach (var a in rows)
                        {
                            result.Add((a.AttendanceAdjustmentRequestId, new RequestInfo(
                                a.Employee?.Username ?? string.Empty,
                                a.Employee?.Account?.FullName ?? a.Employee?.Username ?? string.Empty,
                                a.StoreId,
                                a.Employee?.Account?.Role,
                                a.Status,
                                BuildRequestCode(requestType, a.CreatedDate, a.AttendanceAdjustmentRequestId),
                                $"{a.Shift?.ShiftName} {a.WorkDate:dd/MM/yyyy}".Trim(),
                                a.RejectionReason,
                                a.WorkDate.Date, a.WorkDate.Date,
                                null)));
                        }
                        break;
                    }
            }
            return result;
        }

        public async Task NotifyRequestDecisionAsync(string requestType, IEnumerable<int> ids, string? actorUsername)
        {
            try
            {
                var idList = (ids ?? Enumerable.Empty<int>()).Where(x => x > 0).Distinct().ToList();
                if (idList.Count == 0) return;

                List<(int Id, RequestInfo Info)> requests;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DefaultDatabaseContext>();
                    requests = await LoadRequestsAsync(db, requestType, idList);
                }

                foreach (var (_, info) in requests)
                {
                    if (info.Status != RegistrationStatus.Approved && info.Status != RegistrationStatus.Rejected) continue;
                    if (string.IsNullOrEmpty(info.RequesterUsername)) continue;

                    var url = RequestUrl(requestType, info.Code, info.RangeStart, info.RangeEnd);
                    bool approved = info.Status == RegistrationStatus.Approved;

                    if (!string.Equals(info.RequesterUsername, actorUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        if (approved)
                            await NotifyAsync(info.RequesterUsername, NotificationTypes.RequestApproved, new[] { requestType, info.Code, info.Detail }, url);
                        else
                            await NotifyAsync(info.RequesterUsername, NotificationTypes.RequestRejected, new[] { requestType, info.Code, info.Detail, info.RejectionReason }, url);
                    }

                    // Đổi ca với đồng nghiệp: báo cho đồng nghiệp khi đơn được duyệt
                    if (approved && requestType == NotificationRequestTypes.ShiftSwap
                        && !string.IsNullOrEmpty(info.CoworkerUsername)
                        && !string.Equals(info.CoworkerUsername, actorUsername, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(info.CoworkerUsername, info.RequesterUsername, StringComparison.OrdinalIgnoreCase))
                    {
                        await NotifyAsync(info.CoworkerUsername!, NotificationTypes.SwapCoworkerApproved,
                            new[] { info.Code, info.RequesterName, info.Detail }, "/ShiftRegistration/Index");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send decision notifications for {RequestType}", requestType);
            }
        }

        public async Task NotifyRequestSubmittedAsync(string requestType, int id, string? actorUsername)
        {
            try
            {
                if (id <= 0) return;
                RequestInfo? info;
                List<string> approvers;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DefaultDatabaseContext>();
                    info = (await LoadRequestsAsync(db, requestType, new List<int> { id })).Select(x => x.Info).FirstOrDefault();
                    if (info == null || info.Status != RegistrationStatus.Pending) return;

                    // Mirror RequestController: đơn điều chỉnh công của StoreManager/HR chỉ Admin được duyệt
                    bool adminOnly = requestType == NotificationRequestTypes.Attendance
                        && (info.RequesterRole == AccountRole.StoreManager || info.RequesterRole == AccountRole.HR);
                    approvers = await GetApproverUsernamesAsync(db, info.StoreId, info.RequesterUsername, adminOnly);
                }

                approvers = approvers.Where(u => !string.Equals(u, actorUsername, StringComparison.OrdinalIgnoreCase)).ToList();
                if (approvers.Count == 0) return;

                await NotifyAsync(approvers, NotificationTypes.RequestSubmitted,
                    new[] { requestType, info.Code, info.RequesterName, info.Detail },
                    RequestUrl(requestType, info.Code, info.RangeStart, info.RangeEnd));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send submitted notification for {RequestType} #{Id}", requestType, id);
            }
        }

        public async Task NotifyShiftRegistrationsSubmittedAsync(int employeeId, IReadOnlyCollection<int> registrationIds, string? actorUsername)
        {
            try
            {
                var ids = (registrationIds ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToList();
                if (ids.Count == 0) return;
                if (ids.Count == 1)
                {
                    await NotifyRequestSubmittedAsync(NotificationRequestTypes.ShiftReg, ids[0], actorUsername);
                    return;
                }

                List<(int Id, RequestInfo Info)> rows;
                List<string> approvers;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DefaultDatabaseContext>();
                    rows = (await LoadRequestsAsync(db, NotificationRequestTypes.ShiftReg, ids))
                        .Where(x => x.Info.Status == RegistrationStatus.Pending)
                        .ToList();
                    if (rows.Count == 0) return;
                    var first = rows[0].Info;
                    approvers = await GetApproverUsernamesAsync(db, first.StoreId, first.RequesterUsername, false);
                }

                approvers = approvers.Where(u => !string.Equals(u, actorUsername, StringComparison.OrdinalIgnoreCase)).ToList();
                if (approvers.Count == 0) return;

                var start = rows.Min(x => x.Info.RangeStart);
                var end = rows.Max(x => x.Info.RangeEnd);
                if (rows.Count == 1)
                {
                    var single = rows[0].Info;
                    await NotifyAsync(approvers, NotificationTypes.RequestSubmitted,
                        new[] { NotificationRequestTypes.ShiftReg, single.Code, single.RequesterName, single.Detail },
                        RequestUrl(NotificationRequestTypes.ShiftReg, single.Code, start, end));
                    return;
                }

                await NotifyAsync(approvers, NotificationTypes.ShiftRegSubmittedBatch,
                    new[] { rows[0].Info.RequesterName, rows.Count.ToString(CultureInfo.InvariantCulture), DateRange(start, end) },
                    RequestUrl(NotificationRequestTypes.ShiftReg, null, start, end, pendingOnly: true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send shift registration notifications for employee {EmployeeId}", employeeId);
            }
        }

        public async Task NotifyPayslipsAvailableAsync(int payrollId, string? actorUsername)
        {
            try
            {
                DateTime start, end;
                List<string> usernames;
                using (var scope = _scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<DefaultDatabaseContext>();
                    var payroll = await db.Payroll.AsNoTracking()
                        .Where(p => p.PayrollId == payrollId && p.Status == PayrollStatus.Locked)
                        .Select(p => new { p.StartDate, p.EndDate })
                        .FirstOrDefaultAsync();
                    if (payroll == null) return;
                    start = payroll.StartDate;
                    end = payroll.EndDate;

                    usernames = await db.PayrollDetail.AsNoTracking()
                        .Where(d => d.PayrollId == payrollId)
                        .Select(d => d.Employee.Username)
                        .Distinct()
                        .ToListAsync();
                }
                if (usernames.Count == 0) return;

                await NotifyAsync(usernames, NotificationTypes.PayslipAvailable,
                    new[] { start.ToString("dd/MM"), end.ToString("dd/MM") },
                    "/Payroll/MyPayslipDetail?payrollId=" + payrollId.ToString(CultureInfo.InvariantCulture));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send payslip notifications for payroll {PayrollId}", payrollId);
            }
        }

        #endregion
    }
}
