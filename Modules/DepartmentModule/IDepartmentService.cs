public interface IDepartmentService
{
    Task<Guid?> GetPrimaryDepartmentIdAsync(Guid userId);
}