namespace RWPM.Models.ViewModels.Employee
{
    public class EmployeeStatisticsDto
    {
        public int TotalEmployees { get; set; }
        public int ActiveEmployees { get; set; }
        public int FullTimeCount { get; set; }
        public int PartTimeCount { get; set; }
        public int ProbationCount { get; set; }
    }
}
