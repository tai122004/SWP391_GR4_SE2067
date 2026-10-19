using RWPM.Common.Attributes;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace RWPM.Models.ViewModels.ShiftRegistration
{
    public class AttendanceAdjustmentCreateVM
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
            if (string.IsNullOrWhiteSpace(value)) return null;

            value = value.Trim();

            // Handle standard TimeSpan format (hh:mm:ss, d.hh:mm:ss)
            if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
            {
                return ts;
            }

            // Handle formats without seconds (hh:mm, h:mm)
            if (TimeSpan.TryParseExact(value, new[] { "hh\\:mm", "h\\:mm", "hh\\:mm\\:ss", "h\\:mm\\:ss" }, CultureInfo.InvariantCulture, out var tsExact))
            {
                return tsExact;
            }

            // Handle Vietnamese AM/PM (SA = Sáng/AM, CH = Chiều/PM)
            var normalized = value.Replace("SA", "AM", StringComparison.OrdinalIgnoreCase)
                                  .Replace("CH", "PM", StringComparison.OrdinalIgnoreCase);

            if (DateTime.TryParse(normalized, CultureInfo.InvariantCulture, DateTimeStyles.NoCurrentDateDefault, out var dt))
            {
                return dt.TimeOfDay;
            }

            if (DateTime.TryParse(value, out var dtLocal))
            {
                return dtLocal.TimeOfDay;
            }

            return null;
        }
    }
}
