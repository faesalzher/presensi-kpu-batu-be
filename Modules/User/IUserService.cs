
using presensi_kpu_batu_be.Modules.User.Dto;

namespace presensi_kpu_batu_be.Modules.User
{
    public interface IUserService
    {
        Task<UserResponse?> GetUserByGuid(Guid guid);
    }
}
