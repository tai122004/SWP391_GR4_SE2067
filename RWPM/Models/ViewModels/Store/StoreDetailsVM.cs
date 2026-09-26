using RWPM.Models.Entities;
using System.Collections.Generic;

namespace RWPM.Models.ViewModels.Store
{
    public class StoreDetailsVM
    {
        public global::Store Store { get; set; } = null!;
        public List<RWPM.Models.Entities.Employee> Employees { get; set; } = new();
        public List<RWPM.Models.Entities.Shift> Shifts { get; set; } = new();
        public List<AttendanceRecord> TodayAttendanceRecords { get; set; } = new();

        public int TotalEmployees => Employees.Count;
        public int ActiveShiftsCount => Shifts.Count;
        public int TodayPresentCount => TodayAttendanceRecords.Count;
    }
}
