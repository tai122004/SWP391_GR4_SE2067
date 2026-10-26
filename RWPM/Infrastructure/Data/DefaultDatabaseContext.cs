using Microsoft.EntityFrameworkCore;
using RWPM.Models.Entities;

namespace RWPM.Infrastructure.Data
{
    public class DefaultDatabaseContext : DbContext
    {
        public DefaultDatabaseContext(DbContextOptions<DefaultDatabaseContext> options)
        : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            #region Acc
            modelBuilder.Entity<Acc>()
                .HasIndex(i => i.Email)
                .IsUnique()
                .HasFilter("[Email] <> N''")
                .HasDatabaseName("IX_Acc_Email");
            modelBuilder.Entity<Acc>()
                .HasIndex(i => i.PhoneNumber)
                .IsUnique()
                .HasFilter("[PhoneNumber] IS NOT NULL AND [PhoneNumber] <> N''")
                .HasDatabaseName("IX_Acc_PhoneNumber");
            #endregion

            #region Store
            modelBuilder.Entity<Store>(e =>
            {
                e.HasIndex(i => i.StoreCode)
                 .IsUnique()
                 .HasDatabaseName("IX_Store_StoreCode");

                e.HasOne(x => x.Manager)
                 .WithMany()
                 .HasForeignKey(x => x.ManagerId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
            #endregion

            #region Employee
            modelBuilder.Entity<Employee>(e =>
            {
                e.HasIndex(x => x.EmployeeCode)
                 .IsUnique()
                 .HasDatabaseName("IX_Employee_EmployeeCode");

                e.HasOne(x => x.Account)
                 .WithOne()
                 .HasForeignKey<Employee>(x => x.Username)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasIndex(x => x.Username)
                 .IsUnique()
                 .HasDatabaseName("IX_Employee_Username");

                e.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);
            });
            #endregion

            #region Shift
            modelBuilder.Entity<Shift>(e =>
            {
                e.HasCheckConstraint("CK_Shift_DayOffsets", "[EndDayOffset] IN (0,1) AND ([BreakStartDayOffset] IS NULL OR [BreakStartDayOffset] IN (0,1)) AND ([BreakEndDayOffset] IS NULL OR [BreakEndDayOffset] IN (0,1))");
                e.HasCheckConstraint("CK_Shift_BreakFields", "([BreakStartTime] IS NULL AND [BreakEndTime] IS NULL AND [BreakStartDayOffset] IS NULL AND [BreakEndDayOffset] IS NULL) OR ([BreakStartTime] IS NOT NULL AND [BreakEndTime] IS NOT NULL AND [BreakStartDayOffset] IS NOT NULL AND [BreakEndDayOffset] IS NOT NULL)");
                e.HasCheckConstraint("CK_Shift_EffectiveDates", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
            });
            modelBuilder.Entity<StoreShift>(e =>
            {
                e.HasKey(x => new { x.StoreId, x.ShiftId });
                e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
                e.HasOne(x => x.Shift).WithMany(x => x.StoreShifts).HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Restrict);
            });
            #endregion

            #region ShiftRegistration
            modelBuilder.Entity<ShiftRegistration>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.ShiftId, x.WorkDate })
                 .IsUnique()
                 .HasDatabaseName("IX_ShiftRegistration_Employee_Shift_Date");

                e.HasOne(x => x.Employee)
                 .WithMany()
                 .HasForeignKey(x => x.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Shift)
                 .WithMany()
                 .HasForeignKey(x => x.ShiftId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
            #endregion

            #region ImportLog
            modelBuilder.Entity<ImportLog>()
                .HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            #endregion

            #region AttendanceRecord
            modelBuilder.Entity<AttendanceRecord>()
                .HasOne(x => x.Account)
                .WithMany()
                .HasForeignKey(x => x.Username)
                .OnDelete(DeleteBehavior.Restrict);
            #endregion
            #region AttendanceAdjustmentHistory
            modelBuilder.Entity<AttendanceAdjustmentHistory>()
                .HasOne(x => x.AttendanceRecord)
                .WithMany(x => x.AdjustmentHistories)
                .HasForeignKey(x => x.AttendanceRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            #endregion

            #region AttendanceAdjustmentRequest
            modelBuilder.Entity<AttendanceAdjustmentRequest>(e =>
            {
                e.HasOne(x => x.AttendanceRecord)
                 .WithMany(x => x.AdjustmentRequests)
                 .HasForeignKey(x => x.AttendanceRecordId)
                 .OnDelete(DeleteBehavior.SetNull);

                e.HasOne(x => x.Employee)
                 .WithMany()
                 .HasForeignKey(x => x.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Shift)
                 .WithMany()
                 .HasForeignKey(x => x.ShiftId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
            #endregion

            #region ShiftSwapRequest
            modelBuilder.Entity<ShiftSwapRequest>(e =>
            {
                e.HasOne(x => x.RequesterEmployee)
                 .WithMany()
                 .HasForeignKey(x => x.RequesterEmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.TargetEmployee)
                 .WithMany()
                 .HasForeignKey(x => x.TargetEmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.SourceRegistration)
                 .WithMany()
                 .HasForeignKey(x => x.SourceRegistrationId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.TargetRegistration)
                 .WithMany()
                 .HasForeignKey(x => x.TargetRegistrationId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.TargetShift)
                 .WithMany()
                 .HasForeignKey(x => x.TargetShiftId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
            #endregion

            #region LeaveRequest
            modelBuilder.Entity<LeaveRequest>(e =>
            {
                e.HasOne(x => x.Employee)
                 .WithMany()
                 .HasForeignKey(x => x.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .OnDelete(DeleteBehavior.Restrict);
            });
            #endregion

            #region Payroll
            modelBuilder.Entity<Payroll>(e =>
            {
                e.HasIndex(x => new { x.StartDate, x.EndDate })
                 .IsUnique()
                 .HasDatabaseName("IX_Payroll_StartDate_EndDate");

                e.HasMany(x => x.Details)
                 .WithOne(x => x.Payroll)
                 .HasForeignKey(x => x.PayrollId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            #endregion

            #region PayrollDetail
            modelBuilder.Entity<PayrollDetail>(e =>
            {
                e.HasIndex(x => new { x.PayrollId, x.EmployeeId })
                 .IsUnique()
                 .HasDatabaseName("IX_PayrollDetail_PayrollId_EmployeeId");

                e.HasOne(x => x.Employee)
                 .WithMany()
                 .HasForeignKey(x => x.EmployeeId)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .IsRequired(false)
                 .OnDelete(DeleteBehavior.Restrict);

                e.HasMany(x => x.ItemSnapshots)
                 .WithOne(x => x.PayrollDetail)
                 .HasForeignKey(x => x.PayrollDetailId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            #endregion

            #region PayrollItemSnapshot
            modelBuilder.Entity<PayrollItemSnapshot>(e =>
            {
                e.HasOne(x => x.PayrollDetail)
                 .WithMany(x => x.ItemSnapshots)
                 .HasForeignKey(x => x.PayrollDetailId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(x => x.Shift)
                 .WithMany()
                 .HasForeignKey(x => x.ShiftId)
                 .OnDelete(DeleteBehavior.SetNull);
            });
            #endregion

            #region PayrollAuditLog
            modelBuilder.Entity<PayrollAuditLog>(e =>
            {
                e.HasOne(x => x.Payroll)
                 .WithMany(x => x.AuditLogs)
                 .HasForeignKey(x => x.PayrollId)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            #endregion

            #region PerformanceReview
            modelBuilder.Entity<PerformanceReview>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.Year, x.Month }).IsUnique();
                e.HasIndex(x => new { x.StoreId, x.Year, x.Month });
                e.HasOne(x => x.Employee)
                 .WithMany()
                 .HasForeignKey(x => x.EmployeeId)
                 .OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Store)
                 .WithMany()
                 .HasForeignKey(x => x.StoreId)
                 .OnDelete(DeleteBehavior.Restrict);
                e.HasCheckConstraint("CK_PerformanceReview_Score", "[Score] BETWEEN 1 AND 5");
                e.HasCheckConstraint("CK_PerformanceReview_Month", "[Month] BETWEEN 1 AND 12");
            });
            #endregion

            #region ShiftDemand
            modelBuilder.Entity<ShiftDemand>(e =>
            {
                e.HasIndex(x => new { x.StoreId, x.ShiftId, x.DayOfWeek }).IsUnique().HasFilter("[DayOfWeek] IS NOT NULL");
                e.HasIndex(x => new { x.StoreId, x.ShiftId, x.Date }).IsUnique().HasFilter("[Date] IS NOT NULL");
                e.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Cascade);
                e.HasOne(x => x.Shift).WithMany().HasForeignKey(x => x.ShiftId).OnDelete(DeleteBehavior.Cascade);
                e.HasCheckConstraint("CK_ShiftDemand_Kind", "([DayOfWeek] IS NULL AND [Date] IS NOT NULL) OR ([DayOfWeek] IS NOT NULL AND [Date] IS NULL)");
                e.HasCheckConstraint("CK_ShiftDemand_DayOfWeek", "[DayOfWeek] IS NULL OR [DayOfWeek] BETWEEN 0 AND 6");
                e.HasCheckConstraint("CK_ShiftDemand_RequiredCount", "[RequiredCount] BETWEEN 0 AND 100");
            });
            #endregion

            #region EmployeeAvailability
            modelBuilder.Entity<EmployeeAvailability>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.DayOfWeek, x.Status });
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
                e.HasCheckConstraint("CK_EmployeeAvailability_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
                e.HasCheckConstraint("CK_EmployeeAvailability_Times", "[StartTime] <> [EndTime]");
            });
            #endregion

            #region EmployeeUnavailability
            modelBuilder.Entity<EmployeeUnavailability>(e =>
            {
                e.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
                e.HasOne(x => x.Employee).WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Cascade);
            });
            #endregion

            #region Notification
            modelBuilder.Entity<Notification>(e =>
            {
                e.HasIndex(x => new { x.Username, x.IsRead, x.CreatedDate });
                e.HasOne(x => x.Account)
                 .WithMany()
                 .HasForeignKey(x => x.Username)
                 .OnDelete(DeleteBehavior.Cascade);
            });
            #endregion
        }

        public DbSet<Acc> Acc { get; set; } = default!;
        public DbSet<Store> Store { get; set; } = default!;
        public DbSet<Employee> Employee { get; set; } = default!;
        public DbSet<ImportLog> ImportLog { get; set; } = default!;
        public DbSet<Shift> Shift { get; set; } = default!;
        public DbSet<StoreShift> StoreShift { get; set; } = default!;

        public DbSet<AttendanceRecord> AttendanceRecord { get; set; } = default!;
        public DbSet<AttendanceAdjustmentHistory> AttendanceAdjustmentHistory { get; set; } = default!;
        public DbSet<AttendanceAdjustmentRequest> AttendanceAdjustmentRequest { get; set; } = default!;
        public DbSet<ShiftRegistration> ShiftRegistration { get; set; } = default!;
        public DbSet<ShiftSwapRequest> ShiftSwapRequest { get; set; } = default!;
        public DbSet<LeaveRequest> LeaveRequest { get; set; } = default!;
        public DbSet<Payroll> Payroll { get; set; } = default!;
        public DbSet<PayrollDetail> PayrollDetail { get; set; } = default!;
        public DbSet<PayrollItemSnapshot> PayrollItemSnapshot { get; set; } = default!;
        public DbSet<PayrollAuditLog> PayrollAuditLog { get; set; } = default!;
        public DbSet<PerformanceReview> PerformanceReview { get; set; } = default!;
        public DbSet<Notification> Notification { get; set; } = default!;
        public DbSet<ShiftDemand> ShiftDemand { get; set; } = default!;
        public DbSet<EmployeeAvailability> EmployeeAvailability { get; set; } = default!;
        public DbSet<EmployeeUnavailability> EmployeeUnavailability { get; set; } = default!;

    }
}
