using RWPM.Common.Attributes;
using System;
using System.ComponentModel.DataAnnotations;
using RWPM.Common.Validation;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class AttendanceAdjustmentCreateVM : IValidatableObject
    {
        public int? AttendanceRecordId { get; set; }

        public int EmployeeId { get; set; }

        public int StoreId { get; set; }

        public DateTime? WorkDate { get; set; }

        public int? ShiftId { get; set; }

        public string? RequestedCheckInTime { get; set; }

        public string? RequestedCheckOutTime { get; set; }

        [RequiredLocalization]
        [MaxLengthLocalized(500)]
        [Display(Name = "Adj_ReasonField", ResourceType = typeof(RWPM.Resources.Shared.RequestResource))]
        public string Reason { get; set; } = string.Empty;

        public TimeSpan? ParsedRequestedCheckInTime => ParseTime(RequestedCheckInTime);

        public TimeSpan? ParsedRequestedCheckOutTime => ParseTime(RequestedCheckOutTime);

        public static TimeSpan? ParseTime(string? value)
        {
            return AttendanceInputRules.ParseClockTime(value, "RequestedCheckInTime");
        }

        public IEnumerable<ValidationResult> Validate(ValidationContext context)
        {
            foreach (var (value, field) in new[] {
                (RequestedCheckInTime, nameof(RequestedCheckInTime)),
                (RequestedCheckOutTime, nameof(RequestedCheckOutTime)) })
            {
                string? errorMessage = null;
                try { AttendanceInputRules.ParseClockTime(value, field); }
                catch (InputValidationException error) { errorMessage = error.Message; }
                if (errorMessage != null) yield return new ValidationResult(errorMessage, new[] { field });
            }
            if (string.IsNullOrWhiteSpace(RequestedCheckInTime) && string.IsNullOrWhiteSpace(RequestedCheckOutTime))
                yield return new ValidationResult(InputRules.Text("Cần nhập ít nhất một giờ cần điều chỉnh.",
                    "Enter at least one time to adjust."), new[] { nameof(RequestedCheckInTime) });
        }
    }
}
