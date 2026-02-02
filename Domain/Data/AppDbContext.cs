using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Infrastucture.Models;
using presensi_kpu_batu_be.Modules.UserModule;
using System.Security.Claims;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService currentUserService) : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Profile> Profiles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Department> Department { get; set; }
    public DbSet<Attendance> Attendance { get; set; }
    public DbSet<LeaveRequest> LeaveRequest { get; set; }
    public DbSet<GeneralSetting> GeneralSetting { get; set; }
    public DbSet<AttendanceViolation> AttendanceViolation { get; set; }
    public DbSet<FileMetadata> FileMetadata { get; set; }
    public DbSet<RefTunjanganKinerja> RefTunjanganKinerja { get; set; }
    public DbSet<SchedulerLog> SchedulerLogs { get; set; }
    public DbSet<UserFcmToken> UserFcmTokens { get; set; }

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

        modelBuilder.Entity<FileMetadata>(builder =>
        {
            builder.Property(x => x.Category)
                   .HasConversion<string>();
        });

        modelBuilder.Entity<UserFcmToken>(builder =>
        {
            builder.ToTable("user_fcm_tokens");
            builder.HasKey(x => x.Guid);

            builder.Property(x => x.Guid).HasColumnName("guid");
            builder.Property(x => x.UserId).HasColumnName("user_id");
            builder.Property(x => x.FcmToken).HasColumnName("fcm_token");
            builder.Property(x => x.DeviceId).HasColumnName("device_id");
            builder.Property(x => x.IsActive).HasColumnName("is_active");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");

            builder.HasIndex(x => x.IsActive);
        });
    }


    public override async Task<int> SaveChangesAsync(
    CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var username = _currentUserService.Username;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UsrCrt = username;
            }

            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UsrUpd = username;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }


}
