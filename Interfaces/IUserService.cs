
using presensi_kpu_batu_be.DTO.Response;

namespace presensi_kpu_batu_be.Interfaces
{
    public interface IUserService
    {
        Task<UserResponse> GetUserByGuid(Guid guid);
    }
}
