namespace RWPM.Models.Entities;

// Immutable payroll evidence captured from approved registrations before leave cancels them.
public sealed class LeaveScheduleEntry
{
    public int RegistrationId { get; set; }
    public int ShiftId { get; set; }
    public DateTime Date { get; set; }
    public string ShiftName { get; set; } = "";
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public double ScheduledHours { get; set; }
}
