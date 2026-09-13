using RWPM.Common.Enums;
using RWPM.Models.Entities;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class ShiftRegistrationEventVM
    {
        public int id { get; set; }
        public string title { get; set; } = string.Empty;
        public string start { get; set; } = string.Empty;
        public string end { get; set; } = string.Empty;
        public string backgroundColor { get; set; } = string.Empty;
        public string borderColor { get; set; } = string.Empty;
        public string textColor { get; set; } = string.Empty;
        
        // Custom properties
        public int employeeId { get; set; }
        public string employeeName { get; set; } = string.Empty;
        public int shiftId { get; set; }
        public string shiftName { get; set; } = string.Empty;
        public int statusId { get; set; }
        public string statusName { get; set; } = string.Empty;
        public string note { get; set; } = string.Empty;

        public static ShiftRegistrationEventVM FromEntity(Models.Entities.ShiftRegistration entity)
        {
            // Calculate start and end datetime based on WorkDate and Shift.StartTime/EndTime
            var startDateTime = entity.WorkDate.Add(entity.Shift.StartTime);
            var endDateTime = entity.WorkDate.Add(entity.Shift.EndTime);

            // Handle overnight shifts if EndTime < StartTime
            if (entity.Shift.EndTime < entity.Shift.StartTime)
            {
                endDateTime = endDateTime.AddDays(1);
            }

            var color = GetColorByStatus(entity.Status);

            var empName = entity.Employee.Account?.FullName ?? entity.Employee.Username;

            return new ShiftRegistrationEventVM
            {
                id = entity.ShiftRegistrationId,
                title = $"{entity.Shift.ShiftCode} - {entity.Employee.EmployeeCode}",
                start = startDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                end = endDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                backgroundColor = color,
                borderColor = color,
                textColor = "#ffffff",
                employeeId = entity.EmployeeId,
                employeeName = empName,
                shiftId = entity.ShiftId,
                shiftName = entity.Shift.GetLocalizedName(),
                statusId = (int)entity.Status,
                statusName = entity.Status.ToString(),
                note = entity.Note
            };
        }

        private static string GetColorByStatus(RegistrationStatus status)
        {
            return status switch
            {
                RegistrationStatus.Pending => "#f39c12", // Warning/Orange
                RegistrationStatus.Approved => "#00a65a", // Success/Green
                RegistrationStatus.Rejected => "#dd4b39", // Danger/Red
                _ => "#3c8dbc"
            };
        }
    }
}
