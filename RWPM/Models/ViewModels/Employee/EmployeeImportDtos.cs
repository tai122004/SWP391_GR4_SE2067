using System;
using System.Collections.Generic;
using RWPM.Common.Enums;

namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeImportPreviewRowDto
    {
        public int RowNum { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string RoleDisplay { get; set; } = string.Empty;
        public string StoreDisplay { get; set; } = string.Empty;
        public string EmpCode { get; set; } = string.Empty;
        public string EmploymentTypeDisplay { get; set; } = string.Empty;
        public bool IsValid { get; set; }
        public string? ErrorMessage { get; set; }
    }

    public class EmployeeImportValidItemDto
    {
        public string FullName { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public Gender Gender { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public AccountRole Role { get; set; }
        public bool IsStoreRole { get; set; }
        public int StoreId { get; set; }
        public string EmployeeCode { get; set; } = string.Empty;
        public EmploymentType EmploymentType { get; set; }
        public DateTime JoinDate { get; set; }
        public decimal? HourlyRate { get; set; }
        public decimal? BaseSalary { get; set; }
    }

    public class EmployeeImportPreviewResultDto
    {
        public string ImportToken { get; set; } = string.Empty;
        public int TotalRows { get; set; }
        public int ValidCount { get; set; }
        public int InvalidCount { get; set; }
        public List<EmployeeImportPreviewRowDto> Rows { get; set; } = new();
        public List<EmployeeImportValidItemDto> ValidItems { get; set; } = new();
    }

    public class ConfirmImportRequest
    {
        public string ImportToken { get; set; } = string.Empty;
    }
}
