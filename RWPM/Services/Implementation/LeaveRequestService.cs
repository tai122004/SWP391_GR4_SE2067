using System.Text.Json;
using RWPM.Common.Validation;
using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Resources.Shared;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class LeaveRequestService : ILeaveRequestService
    {
        private readonly DefaultDatabaseContext _context;
        public LeaveRequestService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<LeaveRequest>> GetLeaveRequestsAsync(DateTime start, DateTime end, int? storeId = null, RegistrationStatus? status = null)
        {
            var startDay = start.Date;
            var endExclusive = end.Date.AddDays(1);
            var query = _context.LeaveRequest
                .Include(x => x.Employee).ThenInclude(e => e.Account)
                .Include(x => x.Store)
                .Where(x => (x.StartDate <= end && x.EndDate >= start) || (x.CreatedDate >= startDay && x.CreatedDate < endExclusive));

            if (storeId.HasValue)
            {
                query = query.Where(x => x.StoreId == storeId.Value);
            }

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            return await query.OrderByDescending(x => x.CreatedDate).ToListAsync();
        }

        public async Task<LeaveRequest?> GetByIdAsync(int id)
        {
            return await _context.LeaveRequest
                .Include(x => x.Employee).ThenInclude(e => e.Account)
                .Include(x => x.Store)
                .FirstOrDefaultAsync(x => x.LeaveRequestId == id);
        }

        public async Task<LeaveRequest> CreateLeaveRequestAsync(LeaveRequestCreateVM model, string username)
        {
            ArgumentNullException.ThrowIfNull(model);
            model.Reason = InputRules.Reason(model.Reason, 500);
            InputRules.Require(Enum.IsDefined(model.LeaveType), "LeaveType", "Loại nghỉ không hợp lệ.", "Invalid leave type.");
            await WorkflowGuard.EmployeeAsync(_context, username, model.EmployeeId);
            await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var emp = await _context.Employee.Include(e => e.Account).Include(e => e.Store).FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId);
            if (emp == null)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrEmployeeNotFound);
            }

            InputRules.Require(emp.IsActive && emp.Account.IsActive && emp.Store != null && emp.Store.IsActive
                && model.StartDate.Date >= emp.JoinDate.Date && (!emp.ResignDate.HasValue || model.EndDate.Date < emp.ResignDate.Value.Date),
                "EmployeeId", "Nhân viên, chi nhánh hoặc ngày nghỉ không hợp lệ.", "Employee, store or leave dates are invalid.");
            await WorkflowGuard.UnlockedPeriodAsync(_context, model.StartDate.Date, model.EndDate.Date);
            if (model.EndDate.Date < model.StartDate.Date)
            {
                throw new InvalidOperationException(RequestResource.Leave_EndBeforeStart);
            }

            if (model.StartDate.Date < DateTime.Today)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrPastDates);
            }

            // Kiểm tra trùng lặp với đơn nghỉ phép đã có (đang chờ duyệt hoặc đã duyệt)
            var hasOverlap = await _context.LeaveRequest.AnyAsync(x =>
                x.EmployeeId == emp.EmployeeId &&
                x.Status != RegistrationStatus.Rejected &&
                x.StartDate.Date <= model.EndDate.Date &&
                x.EndDate.Date >= model.StartDate.Date);

            if (hasOverlap)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrOverlap);
            }

            int leaveDays = (model.EndDate.Date - model.StartDate.Date).Days + 1;

            // Kiểm tra phép năm nếu là nghỉ có phép
            if (model.LeaveType == LeaveType.Paid)
            {
                if (emp.AnnualLeaveBalance < leaveDays)
                {
                    throw new InvalidOperationException(string.Format(RequestResource.Leave_ErrInsufficientCreate, emp.AnnualLeaveBalance, leaveDays));
                }
            }

            var entity = new LeaveRequest
            {
                EmployeeId = emp.EmployeeId,
                StoreId = emp.StoreId ?? 0,
                StartDate = model.StartDate.Date,
                EndDate = model.EndDate.Date,
                LeaveType = model.LeaveType,
                Reason = model.Reason.Trim(),
                Status = RegistrationStatus.Pending,
                CreatedDate = DateTime.Now,
                CreatedBy = username
            };

            _context.LeaveRequest.Add(entity);
            await _context.SaveChangesAsync();

            if (transaction != null) await transaction.CommitAsync();
            return entity;
        }

        public async Task ApproveLeaveRequestAsync(int id, string approverUsername)
        {
            await using var transaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            var leaveReq = await _context.LeaveRequest
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.LeaveRequestId == id);

            if (leaveReq == null)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrNotFound);
            }

            await _context.Entry(leaveReq).ReloadAsync();
            if (leaveReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrAlreadyProcessed);
            }

            await _context.Entry(leaveReq.Employee).ReloadAsync();
            await WorkflowGuard.EmployeeAsync(_context, approverUsername, leaveReq.EmployeeId, true);
            InputRules.Require(Enum.IsDefined(leaveReq.LeaveType) && leaveReq.StartDate <= leaveReq.EndDate && leaveReq.Employee.IsActive,
                "LeaveType", "Đơn nghỉ hoặc trạng thái nhân viên không còn hợp lệ.", "Leave request or employee status is no longer valid.");
            await WorkflowGuard.UnlockedPeriodAsync(_context, leaveReq.StartDate.Date, leaveReq.EndDate.Date);
            try
            {
                int leaveDays = (leaveReq.EndDate.Date - leaveReq.StartDate.Date).Days + 1;

                // 1. Trừ AnnualLeaveBalance của Employee nếu là Có phép (Paid)
                if (leaveReq.LeaveType == LeaveType.Paid)
                {
                    if (leaveReq.Employee.AnnualLeaveBalance < leaveDays)
                    {
                        throw new InvalidOperationException(string.Format(RequestResource.Leave_ErrInsufficientApprove, leaveReq.Employee.AnnualLeaveBalance, leaveDays));
                    }

                    leaveReq.Employee.AnnualLeaveBalance -= leaveDays;
                    leaveReq.Employee.UpdatedDate = DateTime.Now;
                    leaveReq.Employee.UpdatedBy = approverUsername;
                }

                // 2. Tự động hủy/đánh dấu các ca làm việc trong ngày nghỉ
                var affectedShifts = await _context.ShiftRegistration
                    .Include(sr => sr.Shift)
                    .Where(sr => sr.EmployeeId == leaveReq.EmployeeId 
                              && sr.WorkDate >= leaveReq.StartDate.Date 
                              && sr.WorkDate <= leaveReq.EndDate.Date 
                              && sr.Status != RegistrationStatus.Rejected)
                    .ToListAsync();

                leaveReq.ScheduleSnapshotJson = JsonSerializer.Serialize(affectedShifts
                    .Where(x => x.Status == RegistrationStatus.Approved)
                    .Select(x => new LeaveScheduleEntry { RegistrationId = x.ShiftRegistrationId, ShiftId = x.ShiftId,
                        Date = x.WorkDate.Date, ShiftName = x.Shift.ShiftName, StartTime = x.Shift.StartTime,
                        EndTime = x.Shift.EndTime, ScheduledHours = x.Shift.ScheduledWorkHours }).ToList());
                string leaveTypeText = leaveReq.LeaveType == LeaveType.Paid
                    ? InputRules.Text("nghỉ có phép", "paid leave")
                    : InputRules.Text("nghỉ không lương", "unpaid leave");
                foreach (var shift in affectedShifts)
                {
                    shift.Status = RegistrationStatus.Rejected;
                    shift.RejectionReason = InputRules.Text(
                        $"Hủy do {leaveTypeText} (Đơn nghỉ #{leaveReq.LeaveRequestId} từ {leaveReq.StartDate:dd/MM/yyyy} đến {leaveReq.EndDate:dd/MM/yyyy})",
                        $"Cancelled due to {leaveTypeText} (Leave request #{leaveReq.LeaveRequestId} from {leaveReq.StartDate:dd/MM/yyyy} to {leaveReq.EndDate:dd/MM/yyyy})");
                    shift.Note = (string.IsNullOrEmpty(shift.Note) ? "" : shift.Note + " | ") + InputRules.Text($"[Đã hủy do {leaveTypeText}]", $"[Cancelled due to {leaveTypeText}]");
                    shift.UpdatedDate = DateTime.Now;
                    shift.UpdatedBy = approverUsername;
                }

                // 3. Cập nhật trạng thái đơn xin nghỉ
                leaveReq.Status = RegistrationStatus.Approved;
                leaveReq.ApprovedBy = approverUsername;
                leaveReq.ApprovedAt = DateTime.Now;
                leaveReq.UpdatedDate = DateTime.Now;
                leaveReq.UpdatedBy = approverUsername;

                await _context.SaveChangesAsync();
                if (transaction != null) await transaction.CommitAsync();
            }
            catch
            {
                if (transaction != null) await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RejectLeaveRequestAsync(int id, string? reason, string approverUsername)
        {
            reason = InputRules.Reason(reason);
            await using var rejectionTransaction = _context.Database.CurrentTransaction == null
                ? await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;
            var leaveReq = await _context.LeaveRequest.FindAsync(id);
            if (leaveReq == null)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrNotFound);
            }

            await _context.Entry(leaveReq).ReloadAsync();
            if (leaveReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrAlreadyProcessed);
            }

            await WorkflowGuard.EmployeeAsync(_context, approverUsername, leaveReq.EmployeeId, true);
            leaveReq.Status = RegistrationStatus.Rejected;
            leaveReq.RejectionReason = reason;
            leaveReq.ApprovedBy = approverUsername;
            leaveReq.ApprovedAt = DateTime.Now;
            leaveReq.UpdatedDate = DateTime.Now;
            leaveReq.UpdatedBy = approverUsername;

            await _context.SaveChangesAsync();
            if (rejectionTransaction != null) await rejectionTransaction.CommitAsync();
        }

        public async Task DeleteLeaveRequestAsync(int id, string username)
        {
            var leaveReq = await _context.LeaveRequest
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.LeaveRequestId == id);

            if (leaveReq == null)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrNotFound);
            }

            await _context.Entry(leaveReq).ReloadAsync();
            if (leaveReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException(RequestResource.Leave_ErrDeleteOnlyPending);
            }

            if (leaveReq.Employee?.Username != username && leaveReq.CreatedBy != username)
            {
                throw new UnauthorizedAccessException(RequestResource.Leave_ErrNoPermissionDelete);
            }

            _context.LeaveRequest.Remove(leaveReq);
            await _context.SaveChangesAsync();
        }
    }
}
