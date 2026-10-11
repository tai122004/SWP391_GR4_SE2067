using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
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
            var query = _context.LeaveRequest
                .Include(x => x.Employee).ThenInclude(e => e.Account)
                .Include(x => x.Store)
                .Where(x => (x.StartDate <= end && x.EndDate >= start) || (x.CreatedDate >= start && x.CreatedDate <= end));

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
            var emp = await _context.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == model.EmployeeId);
            if (emp == null)
            {
                throw new InvalidOperationException("Hồ sơ nhân viên không tồn tại.");
            }

            if (model.EndDate.Date < model.StartDate.Date)
            {
                throw new InvalidOperationException("Ngày kết thúc phải lớn hơn hoặc bằng ngày bắt đầu.");
            }

            if (model.StartDate.Date < DateTime.Today)
            {
                throw new InvalidOperationException("Không thể tạo đơn xin nghỉ phép cho các ngày trong quá khứ.");
            }

            // Kiểm tra trùng lặp với đơn nghỉ phép đã có (đang chờ duyệt hoặc đã duyệt)
            var hasOverlap = await _context.LeaveRequest.AnyAsync(x =>
                x.EmployeeId == emp.EmployeeId &&
                x.Status != RegistrationStatus.Rejected &&
                x.StartDate.Date <= model.EndDate.Date &&
                x.EndDate.Date >= model.StartDate.Date);

            if (hasOverlap)
            {
                throw new InvalidOperationException("Bạn đã có đơn xin nghỉ phép đang chờ duyệt hoặc đã duyệt trong khoảng thời gian này.");
            }

            int leaveDays = (model.EndDate.Date - model.StartDate.Date).Days + 1;

            // Kiểm tra phép năm nếu là nghỉ có phép
            if (model.LeaveType == LeaveType.Paid)
            {
                if (emp.AnnualLeaveBalance < leaveDays)
                {
                    throw new InvalidOperationException($"Số ngày phép năm còn lại ({emp.AnnualLeaveBalance} ngày) không đủ để xin nghỉ {leaveDays} ngày có phép. Vui lòng chọn loại 'Nghỉ không phép' hoặc điều chỉnh thời gian.");
                }
            }

            var entity = new LeaveRequest
            {
                EmployeeId = emp.EmployeeId,
                StoreId = emp.StoreId,
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

            return entity;
        }

        public async Task ApproveLeaveRequestAsync(int id, string approverUsername)
        {
            var leaveReq = await _context.LeaveRequest
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.LeaveRequestId == id);

            if (leaveReq == null)
            {
                throw new InvalidOperationException("Đơn xin nghỉ phép không tồn tại.");
            }

            if (leaveReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException("Đơn xin nghỉ phép này đã được xử lý trước đó.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                int leaveDays = (leaveReq.EndDate.Date - leaveReq.StartDate.Date).Days + 1;

                // 1. Trừ AnnualLeaveBalance của Employee nếu là Có phép (Paid)
                if (leaveReq.LeaveType == LeaveType.Paid)
                {
                    if (leaveReq.Employee.AnnualLeaveBalance < leaveDays)
                    {
                        throw new InvalidOperationException($"Nhân viên chỉ còn {leaveReq.Employee.AnnualLeaveBalance} ngày phép năm, không đủ để duyệt đơn nghỉ {leaveDays} ngày có phép.");
                    }

                    leaveReq.Employee.AnnualLeaveBalance -= leaveDays;
                    leaveReq.Employee.UpdatedDate = DateTime.Now;
                    leaveReq.Employee.UpdatedBy = approverUsername;
                }

                // 2. Tự động hủy/đánh dấu các ca làm việc trong ngày nghỉ
                var affectedShifts = await _context.ShiftRegistration
                    .Where(sr => sr.EmployeeId == leaveReq.EmployeeId 
                              && sr.WorkDate >= leaveReq.StartDate.Date 
                              && sr.WorkDate <= leaveReq.EndDate.Date 
                              && sr.Status != RegistrationStatus.Rejected)
                    .ToListAsync();

                string leaveTypeText = leaveReq.LeaveType == LeaveType.Paid ? "nghỉ có phép" : "nghỉ không phép";
                foreach (var shift in affectedShifts)
                {
                    shift.Status = RegistrationStatus.Rejected;
                    shift.RejectionReason = $"Hủy do {leaveTypeText} (Đơn nghỉ #{leaveReq.LeaveRequestId} từ {leaveReq.StartDate:dd/MM/yyyy} đến {leaveReq.EndDate:dd/MM/yyyy})";
                    shift.Note = (string.IsNullOrEmpty(shift.Note) ? "" : shift.Note + " | ") + $"[Đã hủy do {leaveTypeText}]";
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
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RejectLeaveRequestAsync(int id, string? reason, string approverUsername)
        {
            var leaveReq = await _context.LeaveRequest.FindAsync(id);
            if (leaveReq == null)
            {
                throw new InvalidOperationException("Đơn xin nghỉ phép không tồn tại.");
            }

            if (leaveReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException("Đơn xin nghỉ phép này đã được xử lý trước đó.");
            }

            leaveReq.Status = RegistrationStatus.Rejected;
            leaveReq.RejectionReason = reason;
            leaveReq.ApprovedBy = approverUsername;
            leaveReq.ApprovedAt = DateTime.Now;
            leaveReq.UpdatedDate = DateTime.Now;
            leaveReq.UpdatedBy = approverUsername;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteLeaveRequestAsync(int id, string username)
        {
            var leaveReq = await _context.LeaveRequest
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.LeaveRequestId == id);

            if (leaveReq == null)
            {
                throw new InvalidOperationException("Đơn xin nghỉ phép không tồn tại.");
            }

            if (leaveReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException("Chỉ có thể xóa hoặc hủy đơn xin nghỉ phép đang chờ phê duyệt.");
            }

            if (leaveReq.Employee?.Username != username && leaveReq.CreatedBy != username)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa đơn xin nghỉ phép này.");
            }

            _context.LeaveRequest.Remove(leaveReq);
            await _context.SaveChangesAsync();
        }

        public async Task<int> GetRemainingLeaveBalanceAsync(int employeeId)
        {
            var emp = await _context.Employee.FindAsync(employeeId);
            return emp?.AnnualLeaveBalance ?? 0;
        }
    }
}
