using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Infrastucture.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Department> Department { get; set; }
    public DbSet<Attendance> Attendance { get; set; }
    public DbSet<LeaveRequest> LeaveRequest { get; set; }
    public DbSet<GeneralSetting> GeneralSetting { get; set; }
    public DbSet<AttendanceViolation> AttendanceViolation { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AttendanceViolation>(builder =>
        {
            builder.Property(x => x.Type)
                   .HasConversion<string>();

            builder.Property(x => x.Source)
                   .HasConversion<string>();

            builder.Property(x => x.PenaltyPercent)
                   .HasPrecision(5, 2);

            builder.HasOne(x => x.Attendance)
                   .WithMany(a => a.Violation)
                   .HasForeignKey(x => x.AttendanceId)
                   .OnDelete(DeleteBehavior.Cascade);
        });


        modelBuilder.Entity<Attendance>(builder =>
        {
            builder.Property(x => x.Status)
                   .HasConversion<string>();
        });

        modelBuilder.Entity<LeaveRequest>(builder =>
        {
            builder.Property(x => x.Type)
                   .HasConversion<string>();
            builder.Property(x => x.Status)
                   .HasConversion<string>();
        });
    }

}
