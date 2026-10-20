using System.Text.Json;
using RWPM.Models.Entities;

namespace RWPM.Common.Validation;

public static class LeavePayrollRules
{
    public static List<LeaveScheduleEntry> Evidence(string json)
    {
        List<LeaveScheduleEntry>? rows;
        try { rows = JsonSerializer.Deserialize<List<LeaveScheduleEntry>>(json); }
        catch (JsonException) { throw new InputValidationException("Leave", InputRules.Text("Dữ liệu ca nghỉ phép bị lỗi, cần rà soát trước khi tính lương.", "Invalid leave schedule evidence; review before calculating payroll.")); }
        InputRules.Require(rows != null && rows.All(x => x.RegistrationId > 0 && x.ShiftId > 0 && x.Date != default
            && double.IsFinite(x.ScheduledHours) && x.ScheduledHours is > 0 and <= 12)
            && rows.Select(x => x.RegistrationId).Distinct().Count() == rows.Count,
            "Leave", "Dữ liệu ca nghỉ phép không hợp lệ hoặc bị trùng.", "Invalid or duplicate leave schedule evidence.");
        return rows!;
    }
    public static double PayableHours(double scheduled, double worked)
    {
        InputRules.Require(double.IsFinite(scheduled) && double.IsFinite(worked) && scheduled is > 0 and <= 12 && worked >= 0,
            "Leave", "Giờ nghỉ hưởng lương không hợp lệ.", "Invalid paid leave hours.");
        return Math.Max(0, scheduled - worked);
    }
}
