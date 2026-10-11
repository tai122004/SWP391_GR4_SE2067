using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Services.Abstraction;

namespace RWPM.Services.Implementation
{
    public class ShiftSwapService : IShiftSwapService
    {
        private readonly DefaultDatabaseContext _context;

        public ShiftSwapService(DefaultDatabaseContext context)
        {
            _context = context;
        }

        public async Task<List<ShiftRegistration>> GetEligibleShiftsToSwapAsync(int employeeId)
        {
            var today = DateTime.Today;
            return await _context.ShiftRegistration
                .Include(x => x.Shift)
                .Include(x => x.Store)
                .Where(x => x.EmployeeId == employeeId 
                         && x.Status == RegistrationStatus.Approved 
                         && x.WorkDate >= today)
                .OrderBy(x => x.WorkDate)
                .ThenBy(x => x.Shift.StartTime)
                .ToListAsync();
        }

        public async Task<List<Employee>> GetStoreCoworkersAsync(int employeeId, int storeId)
        {
            return await _context.Employee
                .Include(x => x.Account)
                .Where(x => x.StoreId == storeId 
                         && x.EmployeeId != employeeId 
                         && x.IsActive)
                .OrderBy(x => x.EmployeeCode)
                .ToListAsync();
        }

        public async Task<List<ShiftRegistration>> GetCoworkerShiftsAsync(int coworkerId, DateTime? fromDate = null)
        {
            var date = fromDate ?? DateTime.Today;
            return await _context.ShiftRegistration
                .Include(x => x.Shift)
                .Where(x => x.EmployeeId == coworkerId 
                         && x.Status == RegistrationStatus.Approved 
                         && x.WorkDate >= date)
                .OrderBy(x => x.WorkDate)
                .ThenBy(x => x.Shift.StartTime)
                .ToListAsync();
        }

        public async Task<ShiftSwapRequest> CreateSwapRequestAsync(ShiftSwapCreateVM model, string username)
        {
            var sourceReg = await _context.ShiftRegistration
                .Include(x => x.Employee)
                .FirstOrDefaultAsync(x => x.ShiftRegistrationId == model.SourceRegistrationId);

            if (sourceReg == null)
            {
                throw new InvalidOperationException("Ca làm việc cần đổi không tồn tại.");
            }

            if (sourceReg.Status != RegistrationStatus.Approved)
            {
                throw new InvalidOperationException("Chỉ có thể đổi những ca làm việc đã được phê duyệt.");
            }

            var request = new ShiftSwapRequest
            {
                RequesterEmployeeId = sourceReg.EmployeeId,
                SourceRegistrationId = sourceReg.ShiftRegistrationId,
                StoreId = sourceReg.StoreId,
                Reason = model.Reason,
                Status = RegistrationStatus.Pending,
                CreatedDate = DateTime.Now,
                CreatedBy = username
            };

            if (model.SwapType == "peer")
            {
                if (!model.TargetEmployeeId.HasValue || !model.TargetRegistrationId.HasValue)
                {
                    throw new InvalidOperationException("Vui lòng chọn đồng nghiệp và ca làm việc muốn đổi cùng.");
                }

                var targetReg = await _context.ShiftRegistration
                    .FirstOrDefaultAsync(x => x.ShiftRegistrationId == model.TargetRegistrationId.Value 
                                           && x.EmployeeId == model.TargetEmployeeId.Value);

                if (targetReg == null)
                {
                    throw new InvalidOperationException("Ca làm việc của đồng nghiệp không tồn tại.");
                }

                if (targetReg.StoreId != sourceReg.StoreId)
                {
                    throw new InvalidOperationException("Chỉ có thể đổi ca với đồng nghiệp trong cùng một chi nhánh.");
                }

                request.TargetEmployeeId = model.TargetEmployeeId.Value;
                request.TargetRegistrationId = model.TargetRegistrationId.Value;
            }
            else
            {
                if (!model.TargetShiftId.HasValue || !model.TargetWorkDate.HasValue)
                {
                    throw new InvalidOperationException("Vui lòng chọn ca mới và ngày làm việc mới muốn đổi sang.");
                }

                var shift = await _context.Shift.FirstOrDefaultAsync(x => x.ShiftId == model.TargetShiftId.Value && x.IsActive);
                if (shift == null)
                {
                    throw new InvalidOperationException("Ca làm việc mới không tồn tại hoặc đã ngừng hoạt động.");
                }

                request.TargetShiftId = model.TargetShiftId.Value;
                request.TargetWorkDate = model.TargetWorkDate.Value.Date;
            }

            _context.ShiftSwapRequest.Add(request);
            await _context.SaveChangesAsync();

            return request;
        }

        public async Task<List<ShiftSwapRequest>> GetSwapRequestsAsync(DateTime start, DateTime end, int? storeId = null, RegistrationStatus? status = null)
        {
            var query = _context.ShiftSwapRequest
                .Include(x => x.RequesterEmployee).ThenInclude(e => e.Account)
                .Include(x => x.TargetEmployee).ThenInclude(e => e.Account)
                .Include(x => x.SourceRegistration).ThenInclude(r => r.Shift)
                .Include(x => x.TargetRegistration).ThenInclude(r => r.Shift)
                .Include(x => x.TargetShift)
                .Include(x => x.Store)
                .Where(x => x.CreatedDate >= start && x.CreatedDate <= end);

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

        public async Task<ShiftSwapRequest?> GetByIdAsync(int id)
        {
            return await _context.ShiftSwapRequest
                .Include(x => x.RequesterEmployee).ThenInclude(e => e.Account)
                .Include(x => x.TargetEmployee).ThenInclude(e => e.Account)
                .Include(x => x.SourceRegistration).ThenInclude(r => r.Shift)
                .Include(x => x.TargetRegistration).ThenInclude(r => r.Shift)
                .Include(x => x.TargetShift)
                .Include(x => x.Store)
                .FirstOrDefaultAsync(x => x.ShiftSwapRequestId == id);
        }

        public async Task ApproveSwapAsync(int id, string approverUsername)
        {
            var swapReq = await GetByIdAsync(id);
            if (swapReq == null)
            {
                throw new InvalidOperationException("Yêu cầu đổi ca không tồn tại.");
            }

            if (swapReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException("Yêu cầu đổi ca này đã được xử lý trước đó.");
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sourceReg = await _context.ShiftRegistration.FindAsync(swapReq.SourceRegistrationId);
                if (sourceReg == null)
                {
                    throw new InvalidOperationException("Ca làm việc gốc không tồn tại.");
                }

                if (swapReq.TargetRegistrationId.HasValue && swapReq.TargetEmployeeId.HasValue)
                {
                    // Đổi ca với đồng nghiệp (Peer Swap)
                    var targetReg = await _context.ShiftRegistration.FindAsync(swapReq.TargetRegistrationId.Value);
                    if (targetReg == null)
                    {
                        throw new InvalidOperationException("Ca làm việc của đồng nghiệp không tồn tại.");
                    }

                    // Hoán đổi nhân viên giữa 2 ca
                    var originalRequesterId = sourceReg.EmployeeId;
                    var originalTargetId = targetReg.EmployeeId;

                    sourceReg.EmployeeId = originalTargetId;
                    sourceReg.Note = (string.IsNullOrEmpty(sourceReg.Note) ? "" : sourceReg.Note + " | ") 
                                     + $"Nhận đổi ca từ NV {originalRequesterId}";
                    sourceReg.UpdatedDate = DateTime.Now;
                    sourceReg.UpdatedBy = approverUsername;

                    targetReg.EmployeeId = originalRequesterId;
                    targetReg.Note = (string.IsNullOrEmpty(targetReg.Note) ? "" : targetReg.Note + " | ") 
                                     + $"Nhận đổi ca từ NV {originalTargetId}";
                    targetReg.UpdatedDate = DateTime.Now;
                    targetReg.UpdatedBy = approverUsername;
                }
                else if (swapReq.TargetShiftId.HasValue && swapReq.TargetWorkDate.HasValue)
                {
                    // Chuyển sang ca khác của chính mình (Direct Change)
                    // Kiểm tra xem nhân viên đã có ca trùng vào ngày và ca mới chưa
                    var isDuplicate = await _context.ShiftRegistration.AnyAsync(x => 
                        x.EmployeeId == sourceReg.EmployeeId &&
                        x.ShiftId == swapReq.TargetShiftId.Value &&
                        x.WorkDate == swapReq.TargetWorkDate.Value &&
                        x.ShiftRegistrationId != sourceReg.ShiftRegistrationId &&
                        x.Status != RegistrationStatus.Rejected);

                    if (isDuplicate)
                    {
                        throw new InvalidOperationException("Nhân viên đã có lịch làm việc trong ca và ngày mới này.");
                    }

                    sourceReg.ShiftId = swapReq.TargetShiftId.Value;
                    sourceReg.WorkDate = swapReq.TargetWorkDate.Value;
                    sourceReg.Note = (string.IsNullOrEmpty(sourceReg.Note) ? "" : sourceReg.Note + " | ") 
                                     + $"Chuyển ca theo yêu cầu #{swapReq.ShiftSwapRequestId}";
                    sourceReg.UpdatedDate = DateTime.Now;
                    sourceReg.UpdatedBy = approverUsername;
                }

                swapReq.Status = RegistrationStatus.Approved;
                swapReq.ApprovedBy = approverUsername;
                swapReq.ApprovedAt = DateTime.Now;
                swapReq.UpdatedDate = DateTime.Now;
                swapReq.UpdatedBy = approverUsername;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task RejectSwapAsync(int id, string? reason, string approverUsername)
        {
            var swapReq = await _context.ShiftSwapRequest.FindAsync(id);
            if (swapReq == null)
            {
                throw new InvalidOperationException("Yêu cầu đổi ca không tồn tại.");
            }

            if (swapReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException("Yêu cầu đổi ca này đã được xử lý trước đó.");
            }

            swapReq.Status = RegistrationStatus.Rejected;
            swapReq.RejectionReason = reason;
            swapReq.ApprovedBy = approverUsername;
            swapReq.ApprovedAt = DateTime.Now;
            swapReq.UpdatedDate = DateTime.Now;
            swapReq.UpdatedBy = approverUsername;

            await _context.SaveChangesAsync();
        }

        public async Task DeleteSwapAsync(int id, string username)
        {
            var swapReq = await _context.ShiftSwapRequest
                .Include(x => x.RequesterEmployee)
                .FirstOrDefaultAsync(x => x.ShiftSwapRequestId == id);

            if (swapReq == null)
            {
                throw new InvalidOperationException("Yêu cầu đổi ca không tồn tại.");
            }

            if (swapReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException("Chỉ có thể xóa yêu cầu đổi ca đang chờ phê duyệt.");
            }

            if (swapReq.RequesterEmployee?.Username != username && swapReq.CreatedBy != username)
            {
                throw new UnauthorizedAccessException("Bạn không có quyền xóa yêu cầu đổi ca này.");
            }

            _context.ShiftSwapRequest.Remove(swapReq);
            await _context.SaveChangesAsync();
        }
    }
}
