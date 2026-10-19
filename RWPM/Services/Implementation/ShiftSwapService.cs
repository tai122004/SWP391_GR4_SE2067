using Microsoft.EntityFrameworkCore;
using RWPM.Common.Enums;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.ShiftRegistration;
using RWPM.Services.Abstraction;
using SR = RWPM.Resources.Shared.ShiftRegistrationResource;

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
                .Include(x => x.Shift)
                .FirstOrDefaultAsync(x => x.ShiftRegistrationId == model.SourceRegistrationId);

            if (sourceReg == null)
            {
                throw new InvalidOperationException(SR.Swap_SourceNotFound);
            }

            if (sourceReg.Status != RegistrationStatus.Approved)
            {
                throw new InvalidOperationException(SR.Swap_OnlyApproved);
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
                    throw new InvalidOperationException(SR.Swap_SelectPeerAndShift);
                }

                if (model.SourceRegistrationId == model.TargetRegistrationId.Value)
                {
                    throw new InvalidOperationException(SR.Swap_SameShift);
                }

                if (model.TargetEmployeeId.Value == sourceReg.EmployeeId)
                {
                    throw new InvalidOperationException(SR.Swap_WithSelf);
                }

                var targetReg = await _context.ShiftRegistration
                    .Include(x => x.Shift)
                    .Include(x => x.Employee)
                    .FirstOrDefaultAsync(x => x.ShiftRegistrationId == model.TargetRegistrationId.Value 
                                           && x.EmployeeId == model.TargetEmployeeId.Value);

                if (targetReg == null)
                {
                    throw new InvalidOperationException(SR.Swap_PeerShiftNotFound);
                }

                if (targetReg.Status != RegistrationStatus.Approved)
                {
                    throw new InvalidOperationException(SR.Swap_PeerShiftNotApproved);
                }

                if (targetReg.StoreId != sourceReg.StoreId)
                {
                    throw new InvalidOperationException(SR.Swap_SameBranchOnly);
                }

                if (sourceReg.ShiftId == targetReg.ShiftId && sourceReg.WorkDate.Date == targetReg.WorkDate.Date)
                {
                    throw new InvalidOperationException(SR.Swap_SameShiftAndDay);
                }

                // Kiểm tra xem đồng nghiệp đã có ca trùng với ca gốc của người yêu cầu chưa
                var targetHasDuplicate = await _context.ShiftRegistration.AnyAsync(x =>
                    x.EmployeeId == targetReg.EmployeeId &&
                    x.ShiftId == sourceReg.ShiftId &&
                    x.WorkDate == sourceReg.WorkDate.Date &&
                    x.ShiftRegistrationId != targetReg.ShiftRegistrationId &&
                    x.Status != RegistrationStatus.Rejected);
                if (targetHasDuplicate)
                {
                    throw new InvalidOperationException(string.Format(SR.Swap_PeerAlreadyScheduled, sourceReg.WorkDate.ToString("dd/MM/yyyy")));
                }

                // Kiểm tra xem người yêu cầu đã có ca trùng với ca đích của đồng nghiệp chưa
                var requesterHasDuplicate = await _context.ShiftRegistration.AnyAsync(x =>
                    x.EmployeeId == sourceReg.EmployeeId &&
                    x.ShiftId == targetReg.ShiftId &&
                    x.WorkDate == targetReg.WorkDate.Date &&
                    x.ShiftRegistrationId != sourceReg.ShiftRegistrationId &&
                    x.Status != RegistrationStatus.Rejected);
                if (requesterHasDuplicate)
                {
                    throw new InvalidOperationException(string.Format(SR.Swap_RequesterAlreadyScheduled, targetReg.WorkDate.ToString("dd/MM/yyyy")));
                }

                // Kiểm tra xem một trong hai ca có đơn đổi ca khác đang chờ duyệt không
                var hasPendingSwap = await _context.ShiftSwapRequest.AnyAsync(x =>
                    x.Status == RegistrationStatus.Pending &&
                    (x.SourceRegistrationId == sourceReg.ShiftRegistrationId ||
                     x.TargetRegistrationId == sourceReg.ShiftRegistrationId ||
                     x.SourceRegistrationId == targetReg.ShiftRegistrationId ||
                     x.TargetRegistrationId == targetReg.ShiftRegistrationId));
                if (hasPendingSwap)
                {
                    throw new InvalidOperationException(SR.Swap_EitherHasPending);
                }

                request.TargetEmployeeId = model.TargetEmployeeId.Value;
                request.TargetRegistrationId = model.TargetRegistrationId.Value;
            }
            else
            {
                if (!model.TargetShiftId.HasValue || !model.TargetWorkDate.HasValue)
                {
                    throw new InvalidOperationException(SR.Swap_SelectNewShiftAndDate);
                }

                if (sourceReg.ShiftId == model.TargetShiftId.Value && sourceReg.WorkDate.Date == model.TargetWorkDate.Value.Date)
                {
                    throw new InvalidOperationException(SR.Swap_SameAsCurrent);
                }

                var shift = await _context.Shift.FirstOrDefaultAsync(x => x.ShiftId == model.TargetShiftId.Value && x.IsActive);
                if (shift == null)
                {
                    throw new InvalidOperationException(SR.Swap_NewShiftNotFound);
                }

                var isDuplicate = await _context.ShiftRegistration.AnyAsync(x => 
                    x.EmployeeId == sourceReg.EmployeeId &&
                    x.ShiftId == model.TargetShiftId.Value &&
                    x.WorkDate == model.TargetWorkDate.Value.Date &&
                    x.ShiftRegistrationId != sourceReg.ShiftRegistrationId &&
                    x.Status != RegistrationStatus.Rejected);

                if (isDuplicate)
                {
                    throw new InvalidOperationException(string.Format(SR.Swap_YouAlreadyScheduled, model.TargetWorkDate.Value.ToString("dd/MM/yyyy")));
                }

                var hasPendingSwap = await _context.ShiftSwapRequest.AnyAsync(x =>
                    x.Status == RegistrationStatus.Pending &&
                    (x.SourceRegistrationId == sourceReg.ShiftRegistrationId ||
                     x.TargetRegistrationId == sourceReg.ShiftRegistrationId));
                if (hasPendingSwap)
                {
                    throw new InvalidOperationException(SR.Swap_ThisHasPending);
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
                throw new InvalidOperationException(SR.Swap_RequestNotFound);
            }

            if (swapReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException(SR.Swap_AlreadyProcessed);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sourceReg = await _context.ShiftRegistration
                    .Include(x => x.Shift)
                    .Include(x => x.Employee).ThenInclude(e => e.Account)
                    .FirstOrDefaultAsync(x => x.ShiftRegistrationId == swapReq.SourceRegistrationId);

                if (sourceReg == null)
                {
                    throw new InvalidOperationException(SR.Swap_OriginalNotFound);
                }

                if (sourceReg.Status != RegistrationStatus.Approved)
                {
                    throw new InvalidOperationException(SR.Swap_OriginalNoLongerApproved);
                }

                if (sourceReg.EmployeeId != swapReq.RequesterEmployeeId)
                {
                    throw new InvalidOperationException(SR.Swap_OriginalNotOwned);
                }

                if (swapReq.TargetRegistrationId.HasValue && swapReq.TargetEmployeeId.HasValue)
                {
                    // Đổi ca với đồng nghiệp (Peer Swap)
                    var targetReg = await _context.ShiftRegistration
                        .Include(x => x.Shift)
                        .Include(x => x.Employee).ThenInclude(e => e.Account)
                        .FirstOrDefaultAsync(x => x.ShiftRegistrationId == swapReq.TargetRegistrationId.Value);

                    if (targetReg == null)
                    {
                        throw new InvalidOperationException(SR.Swap_PeerShiftNotFound);
                    }

                    if (targetReg.Status != RegistrationStatus.Approved)
                    {
                        throw new InvalidOperationException(SR.Swap_PeerShiftNoLongerApproved);
                    }

                    if (targetReg.EmployeeId != swapReq.TargetEmployeeId.Value)
                    {
                        throw new InvalidOperationException(SR.Swap_PeerShiftNotOwned);
                    }

                    var originalRequesterId = sourceReg.EmployeeId;
                    var originalTargetId = targetReg.EmployeeId;

                    // Kiểm tra trùng lịch 2 chiều trước khi cập nhật
                    var targetHasDuplicate = await _context.ShiftRegistration.AnyAsync(x =>
                        x.EmployeeId == originalTargetId &&
                        x.ShiftId == sourceReg.ShiftId &&
                        x.WorkDate == sourceReg.WorkDate.Date &&
                        x.ShiftRegistrationId != targetReg.ShiftRegistrationId &&
                        x.Status != RegistrationStatus.Rejected);

                    if (targetHasDuplicate)
                    {
                        var targetName = targetReg.Employee?.Account?.FullName ?? targetReg.Employee?.EmployeeCode ?? $"ID {originalTargetId}";
                        throw new InvalidOperationException(string.Format(SR.Swap_TargetEmployeeAlreadyScheduled, targetName, sourceReg.WorkDate.ToString("dd/MM/yyyy")));
                    }

                    var requesterHasDuplicate = await _context.ShiftRegistration.AnyAsync(x =>
                        x.EmployeeId == originalRequesterId &&
                        x.ShiftId == targetReg.ShiftId &&
                        x.WorkDate == targetReg.WorkDate.Date &&
                        x.ShiftRegistrationId != sourceReg.ShiftRegistrationId &&
                        x.Status != RegistrationStatus.Rejected);

                    if (requesterHasDuplicate)
                    {
                        var requesterName = sourceReg.Employee?.Account?.FullName ?? sourceReg.Employee?.EmployeeCode ?? $"ID {originalRequesterId}";
                        throw new InvalidOperationException(string.Format(SR.Swap_RequesterEmployeeAlreadyScheduled, requesterName, targetReg.WorkDate.ToString("dd/MM/yyyy")));
                    }

                    // Hoán đổi nhân viên giữa 2 ca
                    sourceReg.EmployeeId = originalTargetId;
                    sourceReg.Note = (string.IsNullOrEmpty(sourceReg.Note) ? "" : sourceReg.Note + " | ") 
                                     + $"Nhận đổi ca từ NV {originalRequesterId} (Đơn #{swapReq.ShiftSwapRequestId})";
                    sourceReg.UpdatedDate = DateTime.Now;
                    sourceReg.UpdatedBy = approverUsername;

                    targetReg.EmployeeId = originalRequesterId;
                    targetReg.Note = (string.IsNullOrEmpty(targetReg.Note) ? "" : targetReg.Note + " | ") 
                                     + $"Nhận đổi ca từ NV {originalTargetId} (Đơn #{swapReq.ShiftSwapRequestId})";
                    targetReg.UpdatedDate = DateTime.Now;
                    targetReg.UpdatedBy = approverUsername;
                }
                else if (swapReq.TargetShiftId.HasValue && swapReq.TargetWorkDate.HasValue)
                {
                    // Chuyển sang ca khác của chính mình (Direct Change)
                    var isDuplicate = await _context.ShiftRegistration.AnyAsync(x => 
                        x.EmployeeId == sourceReg.EmployeeId &&
                        x.ShiftId == swapReq.TargetShiftId.Value &&
                        x.WorkDate == swapReq.TargetWorkDate.Value.Date &&
                        x.ShiftRegistrationId != sourceReg.ShiftRegistrationId &&
                        x.Status != RegistrationStatus.Rejected);

                    if (isDuplicate)
                    {
                        throw new InvalidOperationException(string.Format(SR.Swap_EmployeeAlreadyScheduledNewDate, swapReq.TargetWorkDate.Value.ToString("dd/MM/yyyy")));
                    }

                    sourceReg.ShiftId = swapReq.TargetShiftId.Value;
                    sourceReg.WorkDate = swapReq.TargetWorkDate.Value.Date;
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
            catch (DbUpdateException ex)
            {
                await transaction.RollbackAsync();
                var innerMsg = ex.InnerException?.Message ?? ex.Message;
                if (innerMsg.Contains("IX_ShiftRegistration_Employee_Shift_Date") || innerMsg.Contains("UNIQUE KEY"))
                {
                    throw new InvalidOperationException(SR.Swap_DuplicateViolation);
                }
                throw new InvalidOperationException(string.Format(SR.Swap_DatabaseError, innerMsg));
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
                throw new InvalidOperationException(SR.Swap_RequestNotFound);
            }

            if (swapReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException(SR.Swap_AlreadyProcessed);
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
                throw new InvalidOperationException(SR.Swap_RequestNotFound);
            }

            if (swapReq.Status != RegistrationStatus.Pending)
            {
                throw new InvalidOperationException(SR.Swap_DeleteOnlyPending);
            }

            if (swapReq.RequesterEmployee?.Username != username && swapReq.CreatedBy != username)
            {
                throw new UnauthorizedAccessException(SR.Swap_DeleteNoPermission);
            }

            _context.ShiftSwapRequest.Remove(swapReq);
            await _context.SaveChangesAsync();
        }
    }
}
