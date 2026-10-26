using System.Globalization;
using RWPM.Common.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RWPM.Common;
using RWPM.Common.Enums;
using RWPM.Common.Exceptions;
using RWPM.Common.Helper;
using RWPM.Common.Models;
using RWPM.Infrastructure.Data;
using RWPM.Models.Entities;
using RWPM.Models.ViewModels.Employee;
using RWPM.Services.Abstraction;
using System.IO;
using Microsoft.Extensions.Caching.Memory;
using EmpRes = RWPM.Resources.Shared.Employee;

namespace RWPM.Services.Implementation
{
    public class EmployeeService : IEmployeeService
    {
        private readonly DefaultDatabaseContext _ctx;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Microsoft.Extensions.Caching.Memory.IMemoryCache _cache;

        public EmployeeService(DefaultDatabaseContext ctx, IHttpContextAccessor httpContextAccessor, Microsoft.Extensions.Caching.Memory.IMemoryCache cache)
        {
            _ctx = ctx;
            _httpContextAccessor = httpContextAccessor;
            _cache = cache;
        }

        public async Task<Employee?> GetByIdAsync(int employeeId, QueryOptions<Employee>? options = null)
        {
            var query = _ctx.Employee
                .Include(x => x.Account)
                .Include(x => x.Store)
                .Where(x => x.EmployeeId == employeeId);
            query = QueryHelper.ApplyQueryOptions(query, options);
            return await query.FirstOrDefaultAsync();
        }

        public async Task<Employee> GetRequiredByIdAsync(int employeeId, QueryOptions<Employee>? options = null)
        {
            var employee = await GetByIdAsync(employeeId, options);
            if (employee == null)
            {
                throw new KeyNotFoundException(string.Format(EmpRes.Err_EmployeeNotFound, employeeId));
            }
            return employee;
        }

        public async Task<SelectList> GetAvailableAccountsSelectListAsync(string? currentUsername = null)
        {
            var data = new List<object>
            {
                new { Id = string.Empty, Display = Resources.Shared.SharedResource.Dropdown_SelectAccount }
            };

            var assignedUsernamesQuery = _ctx.Employee.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(currentUsername))
            {
                assignedUsernamesQuery = assignedUsernamesQuery.Where(e => e.Username != currentUsername);
            }
            var assignedUsernames = await assignedUsernamesQuery.Select(e => e.Username).ToListAsync();

            var availableAccounts = await _ctx.Acc.AsNoTracking()
                .Where(a => a.IsActive && !assignedUsernames.Contains(a.Username))
                .OrderBy(a => a.FullName)
                .ToListAsync();

            var list = availableAccounts.Select(a => new
            {
                Id = a.Username,
                Display = $"{a.FullName} ({a.Username}) - {UIHelper.GetDisplayName(a.Role)}"
            });

            data.AddRange(list);
            return new SelectList(data, "Id", "Display", currentUsername);
        }

        public async Task<PaginationRes<Employee>> SearchAsync(EmployeeSearch searchObject, QueryOptions<Employee>? options = null)
        {
            searchObject.PageNumber = Math.Clamp(searchObject.PageNumber, 1, 1000000);
            searchObject.PageSize = Math.Clamp(searchObject.PageSize, 1, 50);

            var query = _ctx.Employee
                .Include(x => x.Account)
                .Include(x => x.Store)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                var search = searchObject.Search.Trim();
                query = query.Where(e => e.EmployeeCode.Contains(search) ||
                                         e.Username.Contains(search) ||
                                         e.Account.FullName.Contains(search));
            }

            if (searchObject.StoreId.HasValue && searchObject.StoreId.Value > 0)
            {
                query = query.Where(e => e.StoreId == searchObject.StoreId.Value);
            }

            if (searchObject.IsActive.HasValue)
            {
                query = query.Where(e => e.IsActive == searchObject.IsActive.Value);
            }

            query = QueryHelper.ApplyQueryOptions(query, options);

            var totalRecords = await query.CountAsync();
            var data = await query
                .OrderByDescending(e => e.EmployeeId)
                .Skip((searchObject.PageNumber - 1) * searchObject.PageSize)
                .Take(searchObject.PageSize)
                .ToListAsync();

            return new PaginationRes<Employee>(data, searchObject.PageNumber, searchObject.PageSize, totalRecords);
        }

        public async Task<Employee> CreateAsync(Employee entity)
        {
            await using var profileTransaction = _ctx.Database.CurrentTransaction == null
                ? await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            entity.EmployeeCode = entity.EmployeeCode.Trim();
            entity.Username = entity.Username.Trim();

            await ValidateAsync(entity);

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Employee.AddAsync(entity);
            await _ctx.SaveChangesAsync();
            if (profileTransaction != null) await profileTransaction.CommitAsync();
            return entity;
        }

        public async Task UpdateAsync(Employee entity)
        {
            await using var profileTransaction = _ctx.Database.CurrentTransaction == null
                ? await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable) : null;

            entity.EmployeeCode = entity.EmployeeCode.Trim();
            entity.Username = entity.Username.Trim();

            var existing = await _ctx.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == entity.EmployeeId);
            if (existing == null) throw new KeyNotFoundException(string.Format(EmpRes.Err_EmployeeNotFound, entity.EmployeeId));

            // Thông tin cá nhân (SĐT, ngày sinh, giới tính) nằm ở tài khoản liên kết; entity truyền vào có thể là bản no-tracking
            if (entity.Account != null && !ReferenceEquals(entity.Account, existing.Account) && entity.Account.Username == entity.Username)
            {
                var linkedAccount = await _ctx.Acc.FirstOrDefaultAsync(a => a.Username == entity.Username);
                if (linkedAccount != null)
                {
                    linkedAccount.PhoneNumber = string.IsNullOrWhiteSpace(entity.Account.PhoneNumber) ? null : entity.Account.PhoneNumber.Trim();
                    linkedAccount.DateOfBirth = entity.Account.DateOfBirth;
                    linkedAccount.Gender = entity.Account.Gender;
                }
            }

            await ValidateAsync(entity, entity.EmployeeId);

            existing.EmployeeCode = entity.EmployeeCode;
            existing.Username = entity.Username;
            existing.StoreId = entity.StoreId;
            existing.JoinDate = entity.JoinDate;
            existing.EmploymentType = entity.EmploymentType;
            existing.Status = entity.Status;
            existing.HourlyRate = entity.HourlyRate;
            existing.BaseSalary = entity.BaseSalary;
            existing.AnnualLeaveBalance = entity.AnnualLeaveBalance;
            existing.CitizenId = entity.CitizenId;
            existing.ResignDate = entity.ResignDate;
            existing.ResignReason = entity.ResignReason;
            existing.IsActive = entity.IsActive;
            existing.UpdatedDate = DateTime.Now;
            existing.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            // Đồng bộ trạng thái thôi việc nếu có
            if (existing.Status == EmploymentStatus.Resigned)
            {
                existing.IsActive = false;
                if (existing.Account != null)
                {
                    existing.Account.IsActive = false;
                }
            }
            else if (existing.Account != null)
            {
                // Hồ sơ không còn ở trạng thái thôi việc: trạng thái tài khoản theo trạng thái hoạt động của hồ sơ
                existing.Account.IsActive = existing.IsActive;
            }

            await _ctx.SaveChangesAsync();
            if (profileTransaction != null) await profileTransaction.CommitAsync();
        }

        public async Task DeleteAsync(Employee entity)
        {
            var employee = await GetRequiredByIdAsync(entity.EmployeeId);
            if (await _ctx.PayrollDetail.AnyAsync(x => x.EmployeeId == employee.EmployeeId))
                throw new InputValidationException("EmployeeId", InputRules.Text(
                    "Không thể xóa nhân viên vì đã có dữ liệu bảng lương. Hãy chuyển nhân viên sang trạng thái ngừng hoạt động hoặc thôi việc để giữ lịch sử lương.",
                    "Cannot delete an employee with payroll records. Deactivate or resign the employee to preserve payroll history."));
            _ctx.Employee.Remove(employee);
            await _ctx.SaveChangesAsync();
        }

        public async Task<bool> ExistsByCodeAsync(string employeeCode, int? excludeId = null)
        {
            var code = employeeCode.Trim();
            if (excludeId.HasValue)
            {
                return await _ctx.Employee.AnyAsync(e => e.EmployeeCode == code && e.EmployeeId != excludeId.Value);
            }
            return await _ctx.Employee.AnyAsync(e => e.EmployeeCode == code);
        }

        public async Task<bool> ExistsByUsernameAsync(string username, int? excludeId = null)
        {
            var u = username.Trim();
            if (excludeId.HasValue)
            {
                return await _ctx.Employee.AnyAsync(e => e.Username == u && e.EmployeeId != excludeId.Value);
            }
            return await _ctx.Employee.AnyAsync(e => e.Username == u);
        }

        public async Task UpdateActiveStatusAsync(int employeeId, bool active)
        {
            var employee = await _ctx.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (employee == null) throw new ModelValidationException($"Employee_IdNotExists", employeeId.ToString());
            InputRules.Require(!active || employee.Status != EmploymentStatus.Resigned, "IsActive",
                "Nhân viên đã thôi việc. Hãy sửa trạng thái làm việc của hồ sơ trước khi kích hoạt lại.",
                "The employee has resigned. Change the employment status before reactivating.");

            employee.IsActive = active;
            employee.UpdatedDate = DateTime.Now;
            employee.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            if (employee.Account != null)
            {
                employee.Account.IsActive = active;
                employee.Account.UpdatedDate = DateTime.Now;
                employee.Account.UpdatedBy = employee.UpdatedBy;
            }

            await _ctx.SaveChangesAsync();
        }

        public async Task ResignAsync(int employeeId, DateTime resignDate, string? reason)
        {
            var employee = await _ctx.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (employee == null) throw new KeyNotFoundException(string.Format(EmpRes.Err_EmployeeNotFound, employeeId));

            InputRules.Require(resignDate != default && resignDate.Date >= employee.JoinDate.Date, "ResignDate", "Ngày nghỉ việc không được trước ngày vào làm.", "Resignation date cannot precede join date.");
            InputRules.Require((reason?.Trim().Length ?? 0) <= 255, "ResignReason", "Lý do thôi việc không được vượt quá 255 ký tự.", "Resignation reason cannot exceed 255 characters.");
            employee.Status = EmploymentStatus.Resigned;
            employee.IsActive = false;
            employee.ResignDate = resignDate.Date;
            employee.ResignReason = reason?.Trim();
            employee.UpdatedDate = DateTime.Now;
            employee.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            if (employee.Account != null)
            {
                employee.Account.IsActive = false;
                employee.Account.UpdatedDate = DateTime.Now;
                employee.Account.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            }

            // Tự động hủy các ca làm việc trong tương lai của nhân viên
            var futureRegistrations = await _ctx.ShiftRegistration
                .Where(sr => sr.EmployeeId == employeeId && sr.WorkDate >= resignDate.Date && 
                            (sr.Status == RegistrationStatus.Pending || sr.Status == RegistrationStatus.Approved))
                .ToListAsync();

            foreach (var reg in futureRegistrations)
            {
                reg.Status = RegistrationStatus.Rejected;
                reg.Note = $"[Hủy do thôi việc từ {resignDate:dd/MM/yyyy}] {reg.Note}".Trim();
                reg.UpdatedDate = DateTime.Now;
                reg.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            }

            await _ctx.SaveChangesAsync();
        }

        public async Task<EmployeeStatisticsDto> GetStatisticsAsync(int? storeId = null)
        {
            var query = _ctx.Employee.AsNoTracking().AsQueryable();
            if (storeId.HasValue && storeId.Value > 0)
            {
                query = query.Where(e => e.StoreId == storeId.Value);
            }

            var stats = new EmployeeStatisticsDto
            {
                TotalEmployees = await query.CountAsync(),
                ActiveEmployees = await query.CountAsync(e => e.IsActive && e.Status != EmploymentStatus.Resigned),
                FullTimeCount = await query.CountAsync(e => e.EmploymentType == EmploymentType.FullTime && e.IsActive),
                PartTimeCount = await query.CountAsync(e => e.EmploymentType == EmploymentType.PartTime && e.IsActive),
                ProbationCount = await query.CountAsync(e => e.Status == EmploymentStatus.Probation && e.IsActive)
            };

            return stats;
        }

        public async Task<byte[]> ExportToExcelAsync(EmployeeSearch searchObject)
        {
            var query = _ctx.Employee
                .Include(e => e.Account)
                .Include(e => e.Store)
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchObject.Search))
            {
                var s = searchObject.Search.Trim();
                query = query.Where(e => e.EmployeeCode.Contains(s) || e.Username.Contains(s) || (e.Account != null && e.Account.FullName.Contains(s)));
            }
            if (searchObject.StoreId.HasValue && searchObject.StoreId.Value > 0)
            {
                query = query.Where(e => e.StoreId == searchObject.StoreId.Value);
            }
            if (searchObject.IsActive.HasValue)
            {
                query = query.Where(e => e.IsActive == searchObject.IsActive.Value);
            }

            var employees = await query.OrderBy(e => e.StoreId).ThenBy(e => e.EmployeeCode).ToListAsync();

            using var workbook = new ClosedXML.Excel.XLWorkbook();
            var worksheet = workbook.Worksheets.Add(EmpRes.Export_SheetName);

            // Header labels
            worksheet.Cell(1, 1).Value = EmpRes.Export_Col_No;
            worksheet.Cell(1, 2).Value = EmpRes.Export_Col_EmpCode;
            worksheet.Cell(1, 3).Value = EmpRes.Export_Col_FullName;
            worksheet.Cell(1, 4).Value = EmpRes.Export_Col_Account;
            worksheet.Cell(1, 5).Value = EmpRes.Export_Col_Role;
            worksheet.Cell(1, 6).Value = EmpRes.Export_Col_Store;
            worksheet.Cell(1, 7).Value = EmpRes.Export_Col_EmploymentType;
            worksheet.Cell(1, 8).Value = EmpRes.Export_Col_Status;
            worksheet.Cell(1, 9).Value = EmpRes.Template_Col_Phone;
            worksheet.Cell(1, 10).Value = EmpRes.Export_Col_JoinDate;
            worksheet.Cell(1, 11).Value = EmpRes.Export_Col_HourlyRate;
            worksheet.Cell(1, 12).Value = EmpRes.Export_Col_BaseSalary;
            worksheet.Cell(1, 13).Value = EmpRes.Export_Col_LeaveBalance;
            worksheet.Cell(1, 14).Value = RWPM.Resources.Models.Employee.CitizenId;

            var headerRange = worksheet.Range(1, 1, 1, 14);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0d6efd");
            headerRange.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            int row = 2;
            for (int i = 0; i < employees.Count; i++)
            {
                var emp = employees[i];
                worksheet.Cell(row, 1).Value = i + 1;
                worksheet.Cell(row, 2).Value = emp.EmployeeCode;
                worksheet.Cell(row, 3).Value = emp.Account?.FullName ?? "-";
                worksheet.Cell(row, 4).Value = emp.Username;
                worksheet.Cell(row, 5).Value = emp.Account != null ? UIHelper.GetDisplayName(emp.Account.Role) : "-";
                worksheet.Cell(row, 6).Value = emp.Store?.StoreName ?? "-";
                worksheet.Cell(row, 7).Value = UIHelper.GetDisplayName(emp.EmploymentType);
                worksheet.Cell(row, 8).Value = UIHelper.GetDisplayName(emp.Status);
                worksheet.Cell(row, 9).Value = emp.Account?.PhoneNumber ?? "-";
                worksheet.Cell(row, 10).Value = emp.JoinDate.ToString("dd/MM/yyyy");
                worksheet.Cell(row, 11).Value = emp.HourlyRate.HasValue ? emp.HourlyRate.Value.ToString("N0") : "-";
                worksheet.Cell(row, 12).Value = emp.BaseSalary.HasValue ? emp.BaseSalary.Value.ToString("N0") : "-";
                worksheet.Cell(row, 13).Value = emp.AnnualLeaveBalance;
                worksheet.Cell(row, 14).Style.NumberFormat.Format = "@";
                worksheet.Cell(row, 14).Value = emp.CitizenId ?? "-";
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<byte[]> GenerateImportTemplateAsync()
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();

            // Sheet 1: Danh sách nhân viên
            var ws = workbook.Worksheets.Add(EmpRes.Template_SheetEmployees);
            ws.Cell(1, 1).Value = EmpRes.Template_Col_FullName;
            ws.Cell(1, 2).Value = EmpRes.Template_Col_Username;
            ws.Cell(1, 3).Value = EmpRes.Template_Col_Email;
            ws.Cell(1, 4).Value = EmpRes.Template_Col_Phone;
            ws.Cell(1, 5).Value = EmpRes.Template_Col_Gender;
            ws.Cell(1, 6).Value = EmpRes.Template_Col_DateOfBirth;
            ws.Cell(1, 7).Value = EmpRes.Template_Col_Role;
            ws.Cell(1, 8).Value = EmpRes.Template_Col_StoreId;
            ws.Cell(1, 9).Value = EmpRes.Template_Col_EmpCode;
            ws.Cell(1, 10).Value = EmpRes.Template_Col_EmploymentType;
            ws.Cell(1, 11).Value = EmpRes.Template_Col_JoinDate;
            ws.Cell(1, 12).Value = EmpRes.Template_Col_HourlyRate;
            ws.Cell(1, 13).Value = EmpRes.Template_Col_BaseSalary;
            ws.Cell(1, 14).Value = RWPM.Resources.Models.Employee.CitizenId + " (*)";
            // Cột CCCD định dạng Text để Excel không làm mất số 0 ở đầu
            ws.Column(14).Style.NumberFormat.Format = "@";

            var header = ws.Range(1, 1, 1, 14);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0d6efd");
            header.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            var firstStore = await _ctx.Store.AsNoTracking().Where(x => x.IsActive).FirstOrDefaultAsync();
            int sampleStoreId = firstStore?.StoreId ?? 1;

            // Dòng mẫu 1: Nhân viên
            ws.Cell(2, 1).Value = "Nguyễn Văn An";
            ws.Cell(2, 2).Value = "nguyenvanan";
            ws.Cell(2, 3).Value = "an.nguyen@example.com";
            ws.Cell(2, 4).Value = "0912345678";
            ws.Cell(2, 5).Value = EmpRes.Template_Sample_Male;
            ws.Cell(2, 6).Value = "15/05/1998";
            ws.Cell(2, 7).Value = "Employee";
            ws.Cell(2, 8).Value = sampleStoreId;
            ws.Cell(2, 9).Value = "NV26001";
            ws.Cell(2, 10).Value = EmpRes.Template_Sample_PartTime;
            ws.Cell(2, 11).Value = DateTime.Today.ToString("dd/MM/yyyy");
            ws.Cell(2, 12).Value = 25000;
            ws.Cell(2, 13).Value = "";
            ws.Cell(2, 14).Value = "001098012345";

            // Dòng mẫu 2: Quản lý cửa hàng
            ws.Cell(3, 1).Value = "Trần Thị Bích";
            ws.Cell(3, 2).Value = "tranthibich";
            ws.Cell(3, 3).Value = "bich.tran@example.com";
            ws.Cell(3, 4).Value = "0987654321";
            ws.Cell(3, 5).Value = EmpRes.Template_Sample_Female;
            ws.Cell(3, 6).Value = "20/10/1995";
            ws.Cell(3, 7).Value = "StoreManager";
            ws.Cell(3, 8).Value = sampleStoreId;
            ws.Cell(3, 9).Value = "NV26002";
            ws.Cell(3, 10).Value = EmpRes.Template_Sample_FullTime;
            ws.Cell(3, 11).Value = DateTime.Today.ToString("dd/MM/yyyy");
            ws.Cell(3, 12).Value = "";
            ws.Cell(3, 13).Value = 12000000;
            ws.Cell(3, 14).Value = "079195001234";

            ws.Columns().AdjustToContents();

            // Sheet 2: Danh mục Cửa hàng tham chiếu
            var wsStore = workbook.Worksheets.Add(EmpRes.Template_SheetStores);
            wsStore.Cell(1, 1).Value = EmpRes.Template_Store_Col_Id;
            wsStore.Cell(1, 2).Value = EmpRes.Template_Store_Col_Name;
            wsStore.Cell(1, 3).Value = EmpRes.Template_Store_Col_Address;

            var storeHeader = wsStore.Range(1, 1, 1, 3);
            storeHeader.Style.Font.Bold = true;
            storeHeader.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#198754");
            storeHeader.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            var stores = await _ctx.Store.AsNoTracking().Where(x => x.IsActive).ToListAsync();
            int sRow = 2;
            foreach (var st in stores)
            {
                wsStore.Cell(sRow, 1).Value = st.StoreId;
                wsStore.Cell(sRow, 2).Value = st.StoreName;
                wsStore.Cell(sRow, 3).Value = st.Address ?? "-";
                sRow++;
            }
            wsStore.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            return stream.ToArray();
        }

        public async Task<EmployeeImportResultDto> ImportFromExcelAsync(Stream fileStream, string currentUsername)
        {
            var preview = await PreviewImportFromExcelAsync(fileStream);
            var result = new EmployeeImportResultDto { TotalRows = preview.TotalRows, FailureCount = preview.InvalidCount,
                ErrorMessages = preview.Rows.Where(x => !x.IsValid).Select(x => $"{x.RowNum}: {x.ErrorMessage}").ToList() };
            if (preview.ValidCount > 0) result.SuccessCount = await ConfirmImportAsync(preview.ImportToken, currentUsername);
            return result;
        }

        private sealed record ImportSession(string Owner, List<EmployeeImportValidItemDto> Items);

        public async Task<EmployeeImportPreviewResultDto> PreviewImportFromExcelAsync(Stream fileStream)
        {
            var username = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            var actor = await WorkflowGuard.ActorAsync(_ctx, username);
            if (actor.Role is not (AccountRole.Admin or AccountRole.HR)) throw new UnauthorizedAccessException();
            using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
            var worksheet = workbook.Worksheets.FirstOrDefault() ?? throw new InvalidOperationException(EmpRes.Import_NoWorksheet);
            var rows = worksheet.RangeUsed()?.RowsUsed().Skip(1).ToList();
            if (rows == null || rows.Count == 0) throw new InvalidOperationException(EmpRes.Import_NoRowsPreview);
            InputRules.Require(rows.Count <= 5000, "File", "Mỗi lần chỉ nhập tối đa 5.000 dòng.", "Import at most 5,000 rows at a time.");
            var stores = await _ctx.Store.AsNoTracking().Where(x => x.IsActive).ToDictionaryAsync(x => x.StoreId, x => x.StoreName);
            var accounts = await _ctx.Acc.AsNoTracking().Select(x => new { x.Username, x.Email, x.PhoneNumber }).ToListAsync();
            var usernames = accounts.Select(x => x.Username.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var emails = accounts.Select(x => x.Email.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var phones = accounts.Where(x => !string.IsNullOrWhiteSpace(x.PhoneNumber)).Select(x => x.PhoneNumber!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var codes = (await _ctx.Employee.AsNoTracking().Select(x => x.EmployeeCode).ToListAsync()).Select(x => x.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var citizenIds = (await _ctx.Employee.AsNoTracking().Where(x => x.CitizenId != null).Select(x => x.CitizenId!).ToListAsync()).Select(x => x.Trim()).ToHashSet();
            var result = new EmployeeImportPreviewResultDto { ImportToken = Guid.NewGuid().ToString("N") };
            foreach (var row in rows)
            {
                string Cell(int col) => row.Cell(col).GetString().Trim();
                if (Enumerable.Range(1, 14).All(i => string.IsNullOrWhiteSpace(Cell(i)))) continue;
                var preview = new EmployeeImportPreviewRowDto { RowNum = row.RowNumber(), FullName = Cell(1), Username = Cell(2), Email = Cell(3), Phone = Cell(4), EmpCode = Cell(9) };
                result.TotalRows++;
                result.Rows.Add(preview);
                try
                {
                    var roleText = Cell(7).ToLowerInvariant();
                    var role = roleText switch { "employee" or "nhân viên" => AccountRole.Employee,
                        "storemanager" or "quản lý cửa hàng" => AccountRole.StoreManager,
                        "hr" or "nhân sự" or "nhân sự (hr)" => AccountRole.HR,
                        _ => throw new InputValidationException("Role", InputRules.Text("Vai trò không hợp lệ; không được nhập Admin.", "Invalid role; importing Admin is not allowed.")) };
                    var genderText = Cell(5).ToLowerInvariant();
                    var gender = genderText switch { "nam" or "male" => Gender.Male, "nữ" or "nu" or "female" => Gender.Female,
                        "" or "other" or "khác" => Gender.Other,
                        _ => throw new InputValidationException("Gender", InputRules.Text("Giới tính không hợp lệ.", "Invalid gender.")) };
                    var phone = InputRules.ImportPhone(Cell(4));
                    InputRules.Require(Cell(4).Length == 0 || phone.Length > 0, "PhoneNumber", "Số điện thoại không hợp lệ.", "Invalid phone number.");
                    DateTime? Date(int col, bool required)
                    {
                        if (Cell(col).Length == 0 && !required) return null;
                        if (row.Cell(col).DataType == ClosedXML.Excel.XLDataType.DateTime && row.Cell(col).TryGetValue<DateTime>(out var native)) return native.Date;
                        if (DateTime.TryParseExact(Cell(col), new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd" }, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)) return parsed.Date;
                        throw new InputValidationException($"Column{col}", InputRules.Text($"Cột {col}: ngày phải theo dd/MM/yyyy hoặc yyyy-MM-dd.", $"Column {col}: date must use dd/MM/yyyy or yyyy-MM-dd."));
                    }
                    decimal? Money(int col)
                    {
                        if (Cell(col).Length == 0) return null;
                        if (row.Cell(col).DataType == ClosedXML.Excel.XLDataType.Number && row.Cell(col).TryGetValue<decimal>(out var numeric)) return numeric;
                        if (decimal.TryParse(Cell(col), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed)) return parsed;
                        throw new InputValidationException($"Column{col}", InputRules.Text($"Cột {col}: số tiền không hợp lệ.", $"Column {col}: invalid amount."));
                    }
                    var dob = Date(6, false);
                    var joinDate = Date(11, true)!.Value;
                    var empType = Cell(10).ToLowerInvariant() switch { "" or "fulltime" or "toàn thời gian" => EmploymentType.FullTime,
                        "parttime" or "bán thời gian" => EmploymentType.PartTime,
                        _ => throw new InputValidationException("EmploymentType", InputRules.Text("Hình thức làm việc không hợp lệ.", "Invalid employment type.")) };
                    var code = Cell(9).Length > 0 ? Cell(9) : $"NV{DateTime.Today:yyMMdd}{row.RowNumber():D3}";
                    var storeId = int.TryParse(Cell(8), out var id) ? id : 0;
                    if (storeId == 0 && role == AccountRole.HR && Cell(8).Length == 0) storeId = stores.Keys.FirstOrDefault();
                    var hourly = Money(12); var salary = Money(13);
                    // CCCD: nếu Excel lưu dạng số thì số 0 ở đầu bị mất -> bổ sung lại cho đủ 9/12 chữ số
                    var citizenId = InputRules.NormalizeCitizenId(Cell(14));
                    if (citizenId != null && row.Cell(14).DataType == ClosedXML.Excel.XLDataType.Number && citizenId.All(char.IsDigit))
                        citizenId = citizenId.Length is 10 or 11 ? citizenId.PadLeft(12, '0') : citizenId.Length == 8 ? citizenId.PadLeft(9, '0') : citizenId;
                    InputRules.ValidateAccount(Cell(2), Cell(1), Cell(3), phone, dob, gender, role);
                    InputRules.ValidateEmployment(code, joinDate, dob, null, empType, EmploymentStatus.Official, hourly, salary, 12, citizenId);
                    InputRules.Require(!citizenIds.Contains(citizenId!), "CitizenId", "Số CCCD/CMND đã tồn tại hoặc trùng trong file.", "Citizen ID already exists or is duplicated in the file.");
                    InputRules.Require(role != AccountRole.HR || hourly > 0, "HourlyRate", "HR phải có đơn giá dương.", "HR requires a positive hourly rate.");
                    InputRules.Require(stores.ContainsKey(storeId), "StoreId", "Chi nhánh không tồn tại hoặc đã ngừng hoạt động.", "Store does not exist or is inactive.");
                    InputRules.Require(!usernames.Contains(Cell(2)), "Username", "Tài khoản đã tồn tại hoặc trùng trong file.", "Username already exists or is duplicated in the file.");
                    InputRules.Require(!emails.Contains(Cell(3)), "Email", "Email đã tồn tại hoặc trùng trong file.", "Email already exists or is duplicated in the file.");
                    InputRules.Require(phone.Length == 0 || !phones.Contains(phone), "PhoneNumber", "SĐT đã tồn tại hoặc trùng trong file.", "Phone already exists or is duplicated in the file.");
                    InputRules.Require(!codes.Contains(code), "EmployeeCode", "Mã nhân viên đã tồn tại hoặc trùng trong file.", "Employee code already exists or is duplicated in the file.");
                    usernames.Add(Cell(2)); emails.Add(Cell(3)); codes.Add(code); citizenIds.Add(citizenId!); if (phone.Length > 0) phones.Add(phone);
                    preview.IsValid = true; preview.Phone = phone; preview.EmpCode = code;
                    preview.RoleDisplay = UIHelper.GetDisplayName(role); preview.StoreDisplay = stores[storeId]; preview.EmploymentTypeDisplay = UIHelper.GetDisplayName(empType);
                    result.ValidItems.Add(new EmployeeImportValidItemDto { FullName = Cell(1), Username = Cell(2), Email = Cell(3), Phone = phone.Length == 0 ? null : phone,
                        Gender = gender, DateOfBirth = dob, Role = role, IsStoreRole = true, StoreId = storeId, EmployeeCode = code,
                        EmploymentType = empType, JoinDate = joinDate, HourlyRate = hourly, BaseSalary = salary, CitizenId = citizenId });
                    result.ValidCount++;
                }
                catch (InputValidationException ex) { preview.IsValid = false; preview.ErrorMessage = $"{ex.Field}: {ex.Message}"; result.InvalidCount++; }
            }
            if (result.ValidItems.Count > 0) _cache.Set($"ImportPreview_{result.ImportToken}", new ImportSession(username, result.ValidItems), TimeSpan.FromMinutes(20));
            return result;
        }

        public async Task<int> ConfirmImportAsync(string importToken, string currentUsername)
        {
            if (!_cache.TryGetValue($"ImportPreview_{importToken}", out ImportSession? session) || session == null || session.Owner != currentUsername)
                throw new InvalidOperationException(EmpRes.Import_SessionExpired);
            var actor = await WorkflowGuard.ActorAsync(_ctx, currentUsername);
            if (actor.Role is not (AccountRole.Admin or AccountRole.HR)) throw new UnauthorizedAccessException();
            await using var transaction = await _ctx.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Acc>();
            foreach (var item in session.Items)
            {
                InputRules.ValidateAccount(item.Username, item.FullName, item.Email, item.Phone, item.DateOfBirth, item.Gender, item.Role);
                InputRules.ValidateEmployment(item.EmployeeCode, item.JoinDate, item.DateOfBirth, null, item.EmploymentType, EmploymentStatus.Official, item.HourlyRate, item.BaseSalary, 12, item.CitizenId);
                InputRules.Require(!await _ctx.Employee.AnyAsync(x => x.CitizenId == item.CitizenId), "CitizenId", "Số CCCD/CMND đã tồn tại; hãy xem trước file lại.", "Citizen ID now exists; preview again.");
                InputRules.Require(await _ctx.Store.AnyAsync(x => x.StoreId == item.StoreId && x.IsActive), "StoreId", "Chi nhánh đã thay đổi; hãy xem trước file lại.", "Store changed; preview the file again.");
                InputRules.Require(!await _ctx.Acc.AnyAsync(x => x.Username == item.Username || x.Email.Trim().ToLower() == item.Email.ToLower()
                    || (item.Phone != null && x.PhoneNumber != null && x.PhoneNumber.Trim() == item.Phone)), "Email", "Tài khoản/email/SĐT đã tồn tại; hãy xem trước file lại.", "Account/email/phone now exists; preview again.");
                InputRules.Require(!await _ctx.Employee.AnyAsync(x => x.EmployeeCode == item.EmployeeCode), "EmployeeCode", "Mã nhân viên đã tồn tại; hãy xem trước file lại.", "Employee code now exists; preview again.");
                var acc = new Acc { Username = item.Username, FullName = item.FullName, Email = item.Email, PhoneNumber = item.Phone,
                    DateOfBirth = item.DateOfBirth, Gender = item.Gender, Role = item.Role, IsActive = true, CreatedDate = DateTime.Now, CreatedBy = currentUsername };
                acc.Password = hasher.HashPassword(acc, "Aa@123456");
                _ctx.Acc.Add(acc);
                _ctx.Employee.Add(new Employee { EmployeeCode = item.EmployeeCode, Username = item.Username, Account = acc, StoreId = item.StoreId,
                    EmploymentType = item.EmploymentType, Status = EmploymentStatus.Official, JoinDate = item.JoinDate, HourlyRate = item.HourlyRate,
                    BaseSalary = item.BaseSalary, CitizenId = item.CitizenId, AnnualLeaveBalance = 12, IsActive = true, CreatedDate = DateTime.Now, CreatedBy = currentUsername });
            }
            await _ctx.SaveChangesAsync();
            await transaction.CommitAsync();
            _cache.Remove($"ImportPreview_{importToken}");
            return session.Items.Count;
        }

        private async Task ValidateAsync(Employee entity, int? excludeId = null)
        {
            var account = await _ctx.Acc.FirstOrDefaultAsync(x => x.Username == entity.Username);
            var keepingAccount = excludeId.HasValue && await _ctx.Employee.AsNoTracking().AnyAsync(x => x.EmployeeId == excludeId && x.Username == entity.Username);
            InputRules.Require(account != null && (account.IsActive || keepingAccount)
                && account.Role is AccountRole.Employee or AccountRole.StoreManager or AccountRole.HR,
                "Username", "Tài khoản liên kết không tồn tại, bị khóa hoặc không phù hợp vai trò.", "Linked account is missing, inactive or has an invalid role.");
            InputRules.ValidateBirthDate(account!.DateOfBirth);
            InputRules.Require(!account.Gender.HasValue || Enum.IsDefined(account.Gender.Value), "Gender", "Giới tính không hợp lệ.", "Invalid gender.");
            InputRules.Require(string.IsNullOrEmpty(account.PhoneNumber) || System.Text.RegularExpressions.Regex.IsMatch(account.PhoneNumber, @"^0[0-9]{9}$"), "PhoneNumber", "SĐT phải có 10 chữ số bắt đầu bằng 0.", "Phone must contain 10 digits starting with 0.");
            InputRules.ValidateEmployment(entity.EmployeeCode, entity.JoinDate, account.DateOfBirth, entity.ResignDate,
                entity.EmploymentType, entity.Status, entity.HourlyRate, entity.BaseSalary, entity.AnnualLeaveBalance, entity.CitizenId);
            InputRules.Require(!await _ctx.Employee.AnyAsync(x => x.CitizenId == entity.CitizenId && (!excludeId.HasValue || x.EmployeeId != excludeId.Value)),
                "CitizenId", "Số CCCD/CMND đã được dùng cho nhân viên khác.", "This citizen ID is already used by another employee.");
            InputRules.Require(entity.StoreId.HasValue && await _ctx.Store.AnyAsync(x => x.StoreId == entity.StoreId && x.IsActive),
                "StoreId", "Chi nhánh không tồn tại hoặc đã ngừng hoạt động.", "Store does not exist or is inactive.");
            if (await ExistsByCodeAsync(entity.EmployeeCode, excludeId))
            {
                throw new InputValidationException("EmployeeCode", InputRules.Text("Mã nhân viên đã tồn tại.", "Employee code already exists."));
            }

            if (await ExistsByUsernameAsync(entity.Username, excludeId))
            {
                throw new InputValidationException("Username", InputRules.Text("Tài khoản đã có hồ sơ nhân viên.", "Account already has an employee profile."));
            }

            if (entity.StoreId.HasValue && !await _ctx.Store.AnyAsync(s => s.StoreId == entity.StoreId.Value))
            {
                throw new ModelValidationException("Store_NotFound");
            }
        }
    }
}
