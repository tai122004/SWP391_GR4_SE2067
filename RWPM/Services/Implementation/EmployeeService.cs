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
                throw new KeyNotFoundException($"Khong tim thay nhan vien voi ID {employeeId}.");
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
            entity.EmployeeCode = entity.EmployeeCode.Trim();
            entity.Username = entity.Username.Trim();

            await ValidateAsync(entity);

            entity.CreatedDate = DateTime.Now;
            entity.CreatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);

            await _ctx.Employee.AddAsync(entity);
            await _ctx.SaveChangesAsync();
            return entity;
        }

        public async Task UpdateAsync(Employee entity)
        {
            entity.EmployeeCode = entity.EmployeeCode.Trim();
            entity.Username = entity.Username.Trim();

            var existing = await _ctx.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == entity.EmployeeId);
            if (existing == null) throw new KeyNotFoundException($"Không tìm thấy nhân viên ID {entity.EmployeeId}");

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

            await _ctx.SaveChangesAsync();
        }

        public async Task DeleteAsync(Employee entity)
        {
            var employee = await GetRequiredByIdAsync(entity.EmployeeId);
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
            var employee = await GetRequiredByIdAsync(employeeId, new QueryOptions<Employee>() { NoTracking = false });
            employee.IsActive = active;
            employee.UpdatedDate = DateTime.Now;
            employee.UpdatedBy = AccountHelper.GetCurrentUsername(_httpContextAccessor);
            await _ctx.SaveChangesAsync();
        }

        public async Task ResignAsync(int employeeId, DateTime resignDate, string? reason)
        {
            var employee = await _ctx.Employee.Include(e => e.Account).FirstOrDefaultAsync(e => e.EmployeeId == employeeId);
            if (employee == null) throw new KeyNotFoundException($"Không tìm thấy nhân viên ID {employeeId}.");

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
            var worksheet = workbook.Worksheets.Add("Employees");

            // Header labels
            worksheet.Cell(1, 1).Value = "STT";
            worksheet.Cell(1, 2).Value = "Mã nhân viên";
            worksheet.Cell(1, 3).Value = "Họ và tên";
            worksheet.Cell(1, 4).Value = "Tài khoản";
            worksheet.Cell(1, 5).Value = "Vai trò";
            worksheet.Cell(1, 6).Value = "Cửa hàng";
            worksheet.Cell(1, 7).Value = "Hình thức";
            worksheet.Cell(1, 8).Value = "Trạng thái";
            worksheet.Cell(1, 9).Value = "Số điện thoại";
            worksheet.Cell(1, 10).Value = "Ngày vào làm";
            worksheet.Cell(1, 11).Value = "Lương theo giờ";
            worksheet.Cell(1, 12).Value = "Lương cơ bản";
            worksheet.Cell(1, 13).Value = "Phép năm còn lại";

            var headerRange = worksheet.Range(1, 1, 1, 13);
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
            var ws = workbook.Worksheets.Add("Danh sách nhân viên");
            ws.Cell(1, 1).Value = "Họ và tên (*)";
            ws.Cell(1, 2).Value = "Tên đăng nhập (*)";
            ws.Cell(1, 3).Value = "Email (*)";
            ws.Cell(1, 4).Value = "Số điện thoại";
            ws.Cell(1, 5).Value = "Giới tính (Nam/Nữ/Khác)";
            ws.Cell(1, 6).Value = "Ngày sinh (dd/MM/yyyy)";
            ws.Cell(1, 7).Value = "Vai trò (*)";
            ws.Cell(1, 8).Value = "Mã cửa hàng (*)";
            ws.Cell(1, 9).Value = "Mã nhân viên (*)";
            ws.Cell(1, 10).Value = "Hình thức (Toàn thời gian/Bán thời gian)";
            ws.Cell(1, 11).Value = "Ngày vào làm (dd/MM/yyyy)";
            ws.Cell(1, 12).Value = "Lương theo giờ (VNĐ)";
            ws.Cell(1, 13).Value = "Lương cơ bản (VNĐ)";

            var header = ws.Range(1, 1, 1, 13);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#0d6efd");
            header.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            var firstStore = await _ctx.Store.AsNoTracking().FirstOrDefaultAsync();
            int sampleStoreId = firstStore?.StoreId ?? 1;

            // Dòng mẫu 1: Nhân viên bán hàng
            ws.Cell(2, 1).Value = "Nguyễn Văn An";
            ws.Cell(2, 2).Value = "nguyenvanan";
            ws.Cell(2, 3).Value = "an.nguyen@example.com";
            ws.Cell(2, 4).Value = "0912345678";
            ws.Cell(2, 5).Value = "Nam";
            ws.Cell(2, 6).Value = "15/05/1998";
            ws.Cell(2, 7).Value = "SalesStaff";
            ws.Cell(2, 8).Value = sampleStoreId;
            ws.Cell(2, 9).Value = "NV26001";
            ws.Cell(2, 10).Value = "Bán thời gian";
            ws.Cell(2, 11).Value = DateTime.Today.ToString("dd/MM/yyyy");
            ws.Cell(2, 12).Value = 25000;
            ws.Cell(2, 13).Value = "";

            // Dòng mẫu 2: Quản lý cửa hàng
            ws.Cell(3, 1).Value = "Trần Thị Bích";
            ws.Cell(3, 2).Value = "tranthibich";
            ws.Cell(3, 3).Value = "bich.tran@example.com";
            ws.Cell(3, 4).Value = "0987654321";
            ws.Cell(3, 5).Value = "Nữ";
            ws.Cell(3, 6).Value = "20/10/1995";
            ws.Cell(3, 7).Value = "StoreManager";
            ws.Cell(3, 8).Value = sampleStoreId;
            ws.Cell(3, 9).Value = "NV26002";
            ws.Cell(3, 10).Value = "Toàn thời gian";
            ws.Cell(3, 11).Value = DateTime.Today.ToString("dd/MM/yyyy");
            ws.Cell(3, 12).Value = "";
            ws.Cell(3, 13).Value = 12000000;

            ws.Columns().AdjustToContents();

            // Sheet 2: Danh mục Cửa hàng tham chiếu
            var wsStore = workbook.Worksheets.Add("Danh mục Cửa hàng");
            wsStore.Cell(1, 1).Value = "Mã cửa hàng (StoreId)";
            wsStore.Cell(1, 2).Value = "Tên cửa hàng";
            wsStore.Cell(1, 3).Value = "Địa chỉ";

            var storeHeader = wsStore.Range(1, 1, 1, 3);
            storeHeader.Style.Font.Bold = true;
            storeHeader.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.FromHtml("#198754");
            storeHeader.Style.Font.FontColor = ClosedXML.Excel.XLColor.White;

            var stores = await _ctx.Store.AsNoTracking().ToListAsync();
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
            var result = new EmployeeImportResultDto();
            using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);
            if (worksheet == null)
            {
                result.ErrorMessages.Add("File Excel không đúng định dạng hoặc không có sheet dữ liệu.");
                return result;
            }

            var rows = worksheet.RangeUsed()?.RowsUsed()?.Skip(1);
            if (rows == null || !rows.Any())
            {
                result.ErrorMessages.Add("File Excel không có dòng dữ liệu nào để nhập.");
                return result;
            }

            var cachedUsernames = (await _ctx.Acc.Select(x => x.Username).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cachedEmployeeCodes = (await _ctx.Employee.Select(x => x.EmployeeCode).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validStoreIds = (await _ctx.Store.Select(x => x.StoreId).ToListAsync()).ToHashSet();

            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Acc>();
            string defaultPasswordHash = hasher.HashPassword(null!, "Aa@123456");

            var newAccounts = new List<Acc>();
            var newEmployees = new List<Employee>();

            foreach (var row in rows)
            {
                int rowNum = row.RowNumber();
                string fullName = row.Cell(1).GetString()?.Trim() ?? string.Empty;
                string username = row.Cell(2).GetString()?.Trim().ToLower() ?? string.Empty;
                string email = row.Cell(3).GetString()?.Trim() ?? string.Empty;
                string phone = row.Cell(4).GetString()?.Trim() ?? string.Empty;
                string genderStr = row.Cell(5).GetString()?.Trim() ?? string.Empty;
                string dobStr = row.Cell(6).GetString()?.Trim() ?? string.Empty;
                string roleStr = row.Cell(7).GetString()?.Trim() ?? string.Empty;
                string storeStr = row.Cell(8).GetString()?.Trim() ?? string.Empty;
                string empCode = row.Cell(9).GetString()?.Trim() ?? string.Empty;
                string empTypeStr = row.Cell(10).GetString()?.Trim() ?? string.Empty;
                string joinDateStr = row.Cell(11).GetString()?.Trim() ?? string.Empty;
                string hourlyRateStr = row.Cell(12).GetString()?.Trim() ?? string.Empty;
                string baseSalaryStr = row.Cell(13).GetString()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(username))
                {
                    continue; // Bỏ qua dòng trống
                }

                result.TotalRows++;

                // Validation cơ bản
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    result.FailureCount++;
                    result.ErrorMessages.Add($"Dòng {rowNum}: Họ và tên không được để trống.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    result.FailureCount++;
                    result.ErrorMessages.Add($"Dòng {rowNum}: Tên đăng nhập không được để trống.");
                    continue;
                }

                if (cachedUsernames.Contains(username))
                {
                    result.FailureCount++;
                    result.ErrorMessages.Add($"Dòng {rowNum}: Tên đăng nhập '{username}' đã tồn tại trong hệ thống.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(email))
                {
                    result.FailureCount++;
                    result.ErrorMessages.Add($"Dòng {rowNum}: Email không được để trống.");
                    continue;
                }

                // Parse Role
                AccountRole role = AccountRole.SalesStaff;
                if (roleStr.Equals("StoreManager", StringComparison.OrdinalIgnoreCase) || roleStr.Contains("Quản lý", StringComparison.OrdinalIgnoreCase))
                {
                    role = AccountRole.StoreManager;
                }
                else if (roleStr.Equals("HR", StringComparison.OrdinalIgnoreCase) || roleStr.Contains("Nhân sự", StringComparison.OrdinalIgnoreCase))
                {
                    role = AccountRole.HR;
                }
                else if (roleStr.Equals("Admin", StringComparison.OrdinalIgnoreCase) || roleStr.Contains("Quản trị", StringComparison.OrdinalIgnoreCase))
                {
                    role = AccountRole.Admin;
                }
                else
                {
                    role = AccountRole.SalesStaff;
                }

                // Parse Gender
                Gender gender = Gender.Other;
                if (genderStr.Equals("Nam", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("Male", StringComparison.OrdinalIgnoreCase))
                {
                    gender = Gender.Male;
                }
                else if (genderStr.Equals("Nữ", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("Nu", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("Female", StringComparison.OrdinalIgnoreCase))
                {
                    gender = Gender.Female;
                }

                // Parse DateOfBirth
                DateTime? dateOfBirth = null;
                if (DateTime.TryParse(dobStr, out var parsedDob))
                {
                    dateOfBirth = parsedDob;
                }

                bool isStoreRole = (role == AccountRole.StoreManager || role == AccountRole.SalesStaff);
                int parsedStoreId = 0;

                if (isStoreRole)
                {
                    if (!int.TryParse(storeStr, out parsedStoreId) || !validStoreIds.Contains(parsedStoreId))
                    {
                        result.FailureCount++;
                        result.ErrorMessages.Add($"Dòng {rowNum}: Mã cửa hàng '{storeStr}' không hợp lệ hoặc không tồn tại. Vui lòng xem sheet 'Danh mục Cửa hàng'.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(empCode))
                    {
                        empCode = $"NV{DateTime.Now:yyMMdd}{rowNum:D3}";
                    }

                    if (cachedEmployeeCodes.Contains(empCode))
                    {
                        result.FailureCount++;
                        result.ErrorMessages.Add($"Dòng {rowNum}: Mã nhân viên '{empCode}' đã tồn tại trong hệ thống.");
                        continue;
                    }
                }

                // Parse EmploymentType
                EmploymentType empType = (empTypeStr.Contains("Toàn thời gian", StringComparison.OrdinalIgnoreCase) || empTypeStr.Equals("FullTime", StringComparison.OrdinalIgnoreCase))
                    ? EmploymentType.FullTime
                    : EmploymentType.PartTime;

                // Parse JoinDate
                DateTime joinDate = DateTime.Today;
                if (DateTime.TryParse(joinDateStr, out var parsedJoinDate))
                {
                    joinDate = parsedJoinDate;
                }

                // Parse Rates
                decimal? hourlyRate = decimal.TryParse(hourlyRateStr, out var hr) ? hr : null;
                decimal? baseSalary = decimal.TryParse(baseSalaryStr, out var bs) ? bs : null;

                var acc = new Acc
                {
                    Username = username,
                    Password = defaultPasswordHash,
                    FullName = fullName,
                    Email = email,
                    PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                    Gender = gender,
                    DateOfBirth = dateOfBirth,
                    Role = role,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    CreatedBy = currentUsername
                };

                newAccounts.Add(acc);
                cachedUsernames.Add(username);

                if (isStoreRole)
                {
                    var emp = new Employee
                    {
                        EmployeeCode = empCode,
                        Username = username,
                        StoreId = parsedStoreId,
                        EmploymentType = empType,
                        Status = EmploymentStatus.Official,
                        JoinDate = joinDate,
                        HourlyRate = hourlyRate,
                        BaseSalary = baseSalary,
                        AnnualLeaveBalance = 12,
                        IsActive = true
                    };

                    newEmployees.Add(emp);
                    cachedEmployeeCodes.Add(empCode);
                }

                result.SuccessCount++;
            }

            if (newAccounts.Any())
            {
                await _ctx.Acc.AddRangeAsync(newAccounts);
            }

            if (newEmployees.Any())
            {
                await _ctx.Employee.AddRangeAsync(newEmployees);
            }

            if (result.SuccessCount > 0)
            {
                await _ctx.SaveChangesAsync();
            }

            return result;
        }

        public async Task<EmployeeImportPreviewResultDto> PreviewImportFromExcelAsync(Stream fileStream)
        {
            var result = new EmployeeImportPreviewResultDto
            {
                ImportToken = Guid.NewGuid().ToString("N")
            };

            using var workbook = new ClosedXML.Excel.XLWorkbook(fileStream);
            var worksheet = workbook.Worksheet(1);
            if (worksheet == null)
            {
                throw new Exception("File Excel không đúng định dạng hoặc không có trang tính dữ liệu.");
            }

            var rows = worksheet.RangeUsed()?.RowsUsed()?.Skip(1);
            if (rows == null || !rows.Any())
            {
                throw new Exception("File Excel không chứa dòng dữ liệu nào.");
            }

            var cachedUsernames = (await _ctx.Acc.Select(x => x.Username).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var cachedEmployeeCodes = (await _ctx.Employee.Select(x => x.EmployeeCode).ToListAsync()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var storesDict = await _ctx.Store.AsNoTracking().ToDictionaryAsync(s => s.StoreId, s => s.StoreName);

            var batchUsernames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var batchEmployeeCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var row in rows)
            {
                int rowNum = row.RowNumber();
                string fullName = row.Cell(1).GetString()?.Trim() ?? string.Empty;
                string username = row.Cell(2).GetString()?.Trim().ToLower() ?? string.Empty;
                string email = row.Cell(3).GetString()?.Trim() ?? string.Empty;
                string phone = row.Cell(4).GetString()?.Trim() ?? string.Empty;
                string genderStr = row.Cell(5).GetString()?.Trim() ?? string.Empty;
                string dobStr = row.Cell(6).GetString()?.Trim() ?? string.Empty;
                string roleStr = row.Cell(7).GetString()?.Trim() ?? string.Empty;
                string storeStr = row.Cell(8).GetString()?.Trim() ?? string.Empty;
                string empCode = row.Cell(9).GetString()?.Trim() ?? string.Empty;
                string empTypeStr = row.Cell(10).GetString()?.Trim() ?? string.Empty;
                string joinDateStr = row.Cell(11).GetString()?.Trim() ?? string.Empty;
                string hourlyRateStr = row.Cell(12).GetString()?.Trim() ?? string.Empty;
                string baseSalaryStr = row.Cell(13).GetString()?.Trim() ?? string.Empty;

                if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(username))
                {
                    continue; // Bỏ qua dòng trống
                }

                result.TotalRows++;

                var previewRow = new EmployeeImportPreviewRowDto
                {
                    RowNum = rowNum,
                    FullName = fullName,
                    Username = username,
                    Email = email,
                    Phone = phone,
                    EmpCode = empCode,
                    IsValid = true
                };

                // Kiểm tra Họ tên
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    previewRow.IsValid = false;
                    previewRow.ErrorMessage = "Họ và tên không được để trống.";
                }

                // Kiểm tra Username
                if (previewRow.IsValid && string.IsNullOrWhiteSpace(username))
                {
                    previewRow.IsValid = false;
                    previewRow.ErrorMessage = "Tên đăng nhập không được để trống.";
                }
                else if (previewRow.IsValid && (cachedUsernames.Contains(username) || batchUsernames.Contains(username)))
                {
                    previewRow.IsValid = false;
                    previewRow.ErrorMessage = $"Tên đăng nhập '{username}' đã tồn tại.";
                }

                // Kiểm tra Email
                if (previewRow.IsValid && string.IsNullOrWhiteSpace(email))
                {
                    previewRow.IsValid = false;
                    previewRow.ErrorMessage = "Email không được để trống.";
                }

                // Parse Role
                AccountRole role = AccountRole.SalesStaff;
                if (roleStr.Equals("StoreManager", StringComparison.OrdinalIgnoreCase) || roleStr.Contains("Quản lý", StringComparison.OrdinalIgnoreCase))
                {
                    role = AccountRole.StoreManager;
                }
                else if (roleStr.Equals("HR", StringComparison.OrdinalIgnoreCase) || roleStr.Contains("Nhân sự", StringComparison.OrdinalIgnoreCase))
                {
                    role = AccountRole.HR;
                }
                else if (roleStr.Equals("Admin", StringComparison.OrdinalIgnoreCase) || roleStr.Contains("Quản trị", StringComparison.OrdinalIgnoreCase))
                {
                    role = AccountRole.Admin;
                }
                previewRow.RoleDisplay = UIHelper.GetDisplayName(role);

                // Parse Gender
                Gender gender = Gender.Other;
                if (genderStr.Equals("Nam", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("Male", StringComparison.OrdinalIgnoreCase))
                {
                    gender = Gender.Male;
                }
                else if (genderStr.Equals("Nữ", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("Nu", StringComparison.OrdinalIgnoreCase) || genderStr.Equals("Female", StringComparison.OrdinalIgnoreCase))
                {
                    gender = Gender.Female;
                }

                DateTime? dateOfBirth = DateTime.TryParse(dobStr, out var parsedDob) ? parsedDob : null;
                bool isStoreRole = (role == AccountRole.StoreManager || role == AccountRole.SalesStaff);
                int parsedStoreId = 0;

                if (isStoreRole)
                {
                    if (!int.TryParse(storeStr, out parsedStoreId) || !storesDict.ContainsKey(parsedStoreId))
                    {
                        previewRow.IsValid = false;
                        previewRow.ErrorMessage = $"Mã cửa hàng '{storeStr}' không tồn tại. Vui lòng xem sheet 'Danh mục Cửa hàng'.";
                    }
                    else
                    {
                        previewRow.StoreDisplay = storesDict[parsedStoreId];
                    }

                    if (previewRow.IsValid)
                    {
                        if (string.IsNullOrWhiteSpace(empCode))
                        {
                            empCode = $"NV{DateTime.Now:yyMMdd}{rowNum:D3}";
                            previewRow.EmpCode = empCode;
                        }

                        if (cachedEmployeeCodes.Contains(empCode) || batchEmployeeCodes.Contains(empCode))
                        {
                            previewRow.IsValid = false;
                            previewRow.ErrorMessage = $"Mã nhân viên '{empCode}' đã tồn tại.";
                        }
                    }
                }
                else
                {
                    previewRow.StoreDisplay = "Không yêu cầu";
                }

                // Parse EmploymentType
                EmploymentType empType = (empTypeStr.Contains("Toàn thời gian", StringComparison.OrdinalIgnoreCase) || empTypeStr.Equals("FullTime", StringComparison.OrdinalIgnoreCase))
                    ? EmploymentType.FullTime
                    : EmploymentType.PartTime;
                previewRow.EmploymentTypeDisplay = isStoreRole ? UIHelper.GetDisplayName(empType) : "-";

                DateTime joinDate = DateTime.TryParse(joinDateStr, out var parsedJoinDate) ? parsedJoinDate : DateTime.Today;
                decimal? hourlyRate = decimal.TryParse(hourlyRateStr, out var hr) ? hr : null;
                decimal? baseSalary = decimal.TryParse(baseSalaryStr, out var bs) ? bs : null;

                result.Rows.Add(previewRow);

                if (previewRow.IsValid)
                {
                    result.ValidCount++;
                    batchUsernames.Add(username);
                    if (isStoreRole)
                    {
                        batchEmployeeCodes.Add(empCode);
                    }

                    result.ValidItems.Add(new EmployeeImportValidItemDto
                    {
                        FullName = fullName,
                        Username = username,
                        Email = email,
                        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone,
                        Gender = gender,
                        DateOfBirth = dateOfBirth,
                        Role = role,
                        IsStoreRole = isStoreRole,
                        StoreId = parsedStoreId,
                        EmployeeCode = empCode,
                        EmploymentType = empType,
                        JoinDate = joinDate,
                        HourlyRate = hourlyRate,
                        BaseSalary = baseSalary
                    });
                }
                else
                {
                    result.InvalidCount++;
                }
            }

            // Lưu danh sách hợp lệ vào cache trong 20 phút để chờ xác nhận
            if (result.ValidItems.Any())
            {
                _cache.Set($"ImportPreview_{result.ImportToken}", result.ValidItems, TimeSpan.FromMinutes(20));
            }

            return result;
        }

        public async Task<int> ConfirmImportAsync(string importToken, string currentUsername)
        {
            if (!_cache.TryGetValue($"ImportPreview_{importToken}", out List<EmployeeImportValidItemDto>? validItems) || validItems == null || !validItems.Any())
            {
                throw new Exception("Phiên nhập dữ liệu đã hết hạn hoặc không tìm thấy dữ liệu hợp lệ. Vui lòng tải lại file.");
            }

            var hasher = new Microsoft.AspNetCore.Identity.PasswordHasher<Acc>();
            string defaultPasswordHash = hasher.HashPassword(null!, "Aa@123456");

            var newAccounts = new List<Acc>();
            var newEmployees = new List<Employee>();

            foreach (var item in validItems)
            {
                var acc = new Acc
                {
                    Username = item.Username,
                    Password = defaultPasswordHash,
                    FullName = item.FullName,
                    Email = item.Email,
                    PhoneNumber = item.Phone,
                    Gender = item.Gender,
                    DateOfBirth = item.DateOfBirth,
                    Role = item.Role,
                    IsActive = true,
                    CreatedDate = DateTime.Now,
                    CreatedBy = currentUsername
                };
                newAccounts.Add(acc);

                if (item.IsStoreRole)
                {
                    var emp = new Employee
                    {
                        EmployeeCode = item.EmployeeCode,
                        Username = item.Username,
                        StoreId = item.StoreId,
                        EmploymentType = item.EmploymentType,
                        Status = EmploymentStatus.Official,
                        JoinDate = item.JoinDate,
                        HourlyRate = item.HourlyRate,
                        BaseSalary = item.BaseSalary,
                        AnnualLeaveBalance = 12,
                        IsActive = true
                    };
                    newEmployees.Add(emp);
                }
            }

            if (newAccounts.Any())
            {
                await _ctx.Acc.AddRangeAsync(newAccounts);
            }

            if (newEmployees.Any())
            {
                await _ctx.Employee.AddRangeAsync(newEmployees);
            }

            int count = newAccounts.Count;
            if (count > 0)
            {
                await _ctx.SaveChangesAsync();
                _cache.Remove($"ImportPreview_{importToken}");
            }

            return count;
        }

        private async Task ValidateAsync(Employee entity, int? excludeId = null)
        {
            if (await ExistsByCodeAsync(entity.EmployeeCode, excludeId))
            {
                throw new ModelValidationException("Employee_CodeExists", entity.EmployeeCode);
            }

            if (await ExistsByUsernameAsync(entity.Username, excludeId))
            {
                throw new ModelValidationException("Username_Exists", entity.Username);
            }

            if (!await _ctx.Store.AnyAsync(s => s.StoreId == entity.StoreId))
            {
                throw new ModelValidationException("Store_NotFound");
            }
        }
    }
}