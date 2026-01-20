

using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;

namespace presensi_kpu_batu_be.Modules.AttendanceModule
{
    public interface IAttendanceService
    {
        Task<AttendanceResponse?> GetTodayAttendance(Guid userGuid);
        Task<AttendanceResponse> CheckIn(Guid userId, CheckInDto checkInDto);
        Task<AttendanceResponse> CheckOut(Guid userId, CheckOutDto checkOutDto);
        Task RunCutOffCheckInAsync();
        Task RunCutOffCheckOutAsync();
    }



}
