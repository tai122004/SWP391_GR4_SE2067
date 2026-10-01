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
                .HasDatabaseName("IX_Acc_Email");
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
        }

        public DbSet<Acc> Acc { get; set; } = default!;
        public DbSet<Store> Store { get; set; } = default!;
        public DbSet<Employee> Employee { get; set; } = default!;
        public DbSet<ImportLog> ImportLog { get; set; } = default!;
        public DbSet<Shift> Shift { get; set; } = default!;
        public DbSet<StoreShift> StoreShift { get; set; } = default!;

        public DbSet<AttendanceRecord> AttendanceRecord { get; set; } = default!;
        public DbSet<AttendanceAdjustmentHistory> AttendanceAdjustmentHistory { get; set; } = default!;
        public DbSet<ShiftRegistration> ShiftRegistration { get; set; } = default!;

        //public DbSet<IdCounter> IdCounter { get; set; } = default!;

    }
}
