using presensi_kpu_batu_be.Domain.Entities;

public interface IDepartmentService
{
    Task<Guid?> GetPrimaryDepartmentIdAsync(Guid userId);
    Task<Department?> GetByNameAsync(string name);
}