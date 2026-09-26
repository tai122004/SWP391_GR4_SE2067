namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeImportResultDto
    {
        public int TotalRows { get; set; }
        public int SuccessCount { get; set; }
        public int FailureCount { get; set; }
        public List<string> ErrorMessages { get; set; } = new();
    }
}
