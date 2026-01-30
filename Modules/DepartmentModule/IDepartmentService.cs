using presensi_kpu_batu_be.Domain.Entities;
using System.Collections.Generic;

public interface IDepartmentService
{
    Task<Guid?> GetPrimaryDepartmentIdAsync(Guid userId);
    Task<Department?> GetByNameAsync(string name);

    // Get departments where the specified user is head
    Task<List<Department>> GetByHeadAsync(Guid headId);

    // Get departments where the specified user is a member
    Task<List<Department>> GetByMemberAsync(Guid memberId);
}