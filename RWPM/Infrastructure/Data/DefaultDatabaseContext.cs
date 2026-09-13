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
            modelBuilder.Entity<Store>()
                .HasIndex(i => i.StoreCode)
                .IsUnique()
                .HasDatabaseName("IX_Store_StoreCode");
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
                e.HasIndex(x => x.ShiftCode)
                 .IsUnique()
                 .HasDatabaseName("IX_Shift_ShiftCode");
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
        }

        public DbSet<Acc> Acc { get; set; } = default!;
        public DbSet<Store> Store { get; set; } = default!;
        public DbSet<Employee> Employee { get; set; } = default!;
        public DbSet<ImportLog> ImportLog { get; set; } = default!;
        public DbSet<Shift> Shift { get; set; } = default!;
        public DbSet<ShiftRegistration> ShiftRegistration { get; set; } = default!;
        //public DbSet<IdCounter> IdCounter { get; set; } = default!;

    }
}
