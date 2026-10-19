using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RWPM.Services.Implementation
{
    public class AttendanceAdjustmentService : IAttendanceAdjustmentService
    {
        // Persisted to the DB as-is (Vietnamese); translated at display time in RequestController.
        public const string DefaultRejectionReason = "Quản lý từ chối yêu cầu.";

        private readonly DefaultDatabaseContext _context;

        public AttendanceAdjustmentService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        public async Task<AttendanceAdjustmentRequest> CreateRequestAsync(AttendanceAdjustmentCreateVM model, string username)
        {
            if (model == null)
            {
                throw new ArgumentNullException(nameof(model), RequestResource.Adj_ErrInvalidData);
            }

            var employee = await _context.Employee
                .Include(e => e.Store)
                .FirstOrDefaultAsync(e => e.Username == username);

            if (employee == null)
            {
                if (model.EmployeeId > 0)
                {
                    employee = await _context.Employee
                        .Include(e => e.Store)
                        .FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId);
                }
            }

            if (employee == null)
            {
                throw new Exception(RequestResource.Adj_ErrEmployeeNotFound);
            }

            var targetWorkDate = model.WorkDate ?? DateTime.Today;

            AttendanceRecord? record = null;
            if (model.AttendanceRecordId.HasValue && model.AttendanceRecordId.Value > 0)
            {
                record = await _context.AttendanceRecord
                    .Include(r => r.Shift)
                    .FirstOrDefaultAsync(r => r.AttendanceId == model.AttendanceRecordId.Value);

                if (record != null && record.Username != employee.Username)
                {
                    throw new Exception(RequestResource.Adj_ErrOtherEmployeeRecord);
                }
            }
            else
            {
                // Tìm kiếm bản ghi chấm công tương ứng theo ngày và ca nếu có
                var query = _context.AttendanceRecord
                    .Include(r => r.Shift)
                    .Where(r => r.Username == employee.Username && r.Date == targetWorkDate.Date);

                if (model.ShiftId.HasValue)
                {
                    query = query.Where(r => r.ShiftId == model.ShiftId.Value);
                }

                record = await query.OrderByDescending(r => r.AttendanceId).FirstOrDefaultAsync();
            }

            // Kiểm tra xem đã có yêu cầu nào đang chờ duyệt cho bản ghi / ca này chưa
            if (record != null)
            {
                var hasPending = await _context.AttendanceAdjustmentRequest
                    .AnyAsync(r => r.AttendanceRecordId == record.AttendanceId && r.Status == RegistrationStatus.Pending);

                if (hasPending)
                {
                    throw new Exception(RequestResource.Adj_ErrPendingForShift);
                }
            }
            else
            {
                var hasPending = await _context.AttendanceAdjustmentRequest
                    .AnyAsync(r => r.EmployeeId == employee.EmployeeId && r.WorkDate == targetWorkDate.Date && r.ShiftId == model.ShiftId && r.Status == RegistrationStatus.Pending);

                if (hasPending)
                {
                    throw new Exception(RequestResource.Adj_ErrPendingForShiftDate);
                }
            }

            var request = new AttendanceAdjustmentRequest
            {
                AttendanceRecordId = record?.AttendanceId,
                EmployeeId = employee.EmployeeId,
                StoreId = employee.StoreId ?? 0,
                WorkDate = record != null ? record.Date : targetWorkDate.Date,
                ShiftId = record != null ? record.ShiftId : model.ShiftId,
                CurrentCheckInTime = record?.CheckInTime,
                CurrentCheckOutTime = record?.CheckOutTime,
                RequestedCheckInTime = model.ParsedRequestedCheckInTime,
                RequestedCheckOutTime = model.ParsedRequestedCheckOutTime,
                Reason = model.Reason?.Trim() ?? string.Empty,
                Status = RegistrationStatus.Pending,
                CreatedBy = username,
                CreatedDate = DateTime.UtcNow
            };

            _context.AttendanceAdjustmentRequest.Add(request);
            await _context.SaveChangesAsync();

            return request;
        }

        public async Task<List<AttendanceAdjustmentRequest>> GetRequestsAsync(DateTime? startDate, DateTime? endDate, int? storeId, RegistrationStatus? status)
        {
            var query = _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Account)
                .Include(r => r.Store)
                .Include(r => r.Shift)
                .Include(r => r.AttendanceRecord)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(r => r.WorkDate >= startDate.Value.Date);
            }

            if (endDate.HasValue)
            {
                query = query.Where(r => r.WorkDate <= endDate.Value.Date);
            }

            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(r => r.StoreId == storeId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(r => r.Status == status.Value);
            }

            return await query.OrderByDescending(r => r.CreatedDate).ToListAsync();
        }

        public async Task<AttendanceAdjustmentRequest?> GetByIdAsync(int id)
        {
            return await _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                    .ThenInclude(e => e.Account)
                .Include(r => r.Store)
                .Include(r => r.Shift)
                .Include(r => r.AttendanceRecord)
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);
        }

        public async Task ApproveAsync(int id, string approverUsername, TimeSpan? finalCheckInTime = null, TimeSpan? finalCheckOutTime = null, string? approverNote = null)
        {
            var request = await _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                .Include(r => r.AttendanceRecord)
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);

            if (request == null)
            {
                throw new Exception(RequestResource.Adj_ErrNotFound);
            }

            if (request.Status != RegistrationStatus.Pending)
            {
                throw new Exception(RequestResource.Adj_ErrAlreadyProcessed);
            }

            // Lấy giờ thực tế do người phê duyệt quyết định (nếu không truyền thì mặc định lấy giờ nhân viên yêu cầu)
            TimeSpan? approvedIn = finalCheckInTime ?? request.RequestedCheckInTime;
            TimeSpan? approvedOut = finalCheckOutTime ?? request.RequestedCheckOutTime;

            AttendanceRecord? record = request.AttendanceRecord;

            if (record == null && request.AttendanceRecordId.HasValue)
            {
                record = await _context.AttendanceRecord
                    .FirstOrDefaultAsync(r => r.AttendanceId == request.AttendanceRecordId.Value);
            }

            string auditReason = !string.IsNullOrWhiteSpace(approverNote)
                ? $"[Duyệt điều chỉnh] Lý do NV: {request.Reason} | Quản lý ghi chú: {approverNote.Trim()}"
                : $"[Duyệt điều chỉnh] Lý do NV: {request.Reason}";

            if (record != null)
            {
                // Ghi nhận lịch sử điều chỉnh
                var history = new AttendanceAdjustmentHistory
                {
                    AttendanceRecordId = record.AttendanceId,
                    OldCheckInTime = record.CheckInTime,
                    OldCheckOutTime = record.CheckOutTime,
                    NewCheckInTime = approvedIn,
                    NewCheckOutTime = approvedOut,
                    Reason = auditReason,
                    ModifiedBy = approverUsername,
                    ModifiedAt = DateTime.Now
                };
                _context.AttendanceAdjustmentHistory.Add(history);

                // Cập nhật giờ công
                record.CheckInTime = approvedIn;
                record.CheckOutTime = approvedOut;
                record.IsAdjusted = true;
            }
            else
            {
                // Trường hợp trước đó nhân viên quên bấm giờ hoàn toàn
                var newRecord = new AttendanceRecord
                {
                    Username = request.Employee.Username,
                    Date = request.WorkDate,
                    ShiftId = request.ShiftId,
                    CheckInTime = approvedIn,
                    CheckOutTime = approvedOut,
                    IsAdjusted = true
                };
                _context.AttendanceRecord.Add(newRecord);
                await _context.SaveChangesAsync();

                request.AttendanceRecordId = newRecord.AttendanceId;

                var history = new AttendanceAdjustmentHistory
                {
                    AttendanceRecordId = newRecord.AttendanceId,
                    OldCheckInTime = null,
                    OldCheckOutTime = null,
                    NewCheckInTime = approvedIn,
                    NewCheckOutTime = approvedOut,
                    Reason = auditReason,
                    ModifiedBy = approverUsername,
                    ModifiedAt = DateTime.Now
                };
                _context.AttendanceAdjustmentHistory.Add(history);
            }

            // Đánh dấu yêu cầu là Đã duyệt
            request.Status = RegistrationStatus.Approved;
            request.ApprovedBy = approverUsername;
            request.ApprovedAt = DateTime.UtcNow;
            request.UpdatedBy = approverUsername;
            request.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task RejectAsync(int id, string reason, string rejectorUsername)
        {
            var request = await _context.AttendanceAdjustmentRequest
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);

            if (request == null)
            {
                throw new Exception(RequestResource.Adj_ErrNotFound);
            }

            if (request.Status != RegistrationStatus.Pending)
            {
                throw new Exception(RequestResource.Adj_ErrAlreadyProcessed);
            }

            request.Status = RegistrationStatus.Rejected;
            request.RejectionReason = string.IsNullOrWhiteSpace(reason) ? DefaultRejectionReason : reason.Trim();
            request.ApprovedBy = rejectorUsername;
            request.ApprovedAt = DateTime.UtcNow;
            request.UpdatedBy = rejectorUsername;
            request.UpdatedDate = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id, string username)
        {
            var request = await _context.AttendanceAdjustmentRequest
                .Include(r => r.Employee)
                .FirstOrDefaultAsync(r => r.AttendanceAdjustmentRequestId == id);

            if (request == null)
            {
                throw new Exception(RequestResource.Adj_ErrNotFound);
            }

            if (request.Status != RegistrationStatus.Pending)
            {
                throw new Exception(RequestResource.Adj_ErrDeleteOnlyPending);
            }

            if (request.Employee.Username != username && request.CreatedBy != username)
            {
                throw new Exception(RequestResource.Adj_ErrNoPermissionDelete);
            }

            _context.AttendanceAdjustmentRequest.Remove(request);
            await _context.SaveChangesAsync();
        }
    }
}
