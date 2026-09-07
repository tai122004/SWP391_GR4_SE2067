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

            #region ImportLog
            modelBuilder.Entity<ImportLog>()
                .HasOne(x => x.Creator)
                .WithMany()
                .HasForeignKey(w => w.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            #endregion
        }

        public DbSet<Acc> Acc { get; set; } = default!;
        public DbSet<ImportLog> ImportLog { get; set; } = default!;
        //public DbSet<IdCounter> IdCounter { get; set; } = default!;

    }
}
