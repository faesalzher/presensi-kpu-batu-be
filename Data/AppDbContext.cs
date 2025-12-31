using AbsensiTestOne.Models;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Models;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Profile> Profiles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Department> Department { get; set; }

}
