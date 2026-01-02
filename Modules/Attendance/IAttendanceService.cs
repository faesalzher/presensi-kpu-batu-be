

using presensi_kpu_batu_be.Modules.Attendance.Dto;

namespace presensi_kpu_batu_be.Modules.Attendance
{
    public interface IAttendanceService
    {
        Task<AttendanceResponse?> GetTodayAttendance(Guid userGuid);
        Task<Attendance> CheckInAsync(
            Guid userId,
                CheckInDto checkInDto,
    Guid? photoFileGuid);
    }


    public class A { public void B() { int x = 1; } }

}
