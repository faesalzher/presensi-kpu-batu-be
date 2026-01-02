using Microsoft.EntityFrameworkCore;
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
}
