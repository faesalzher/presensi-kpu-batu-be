using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Common.Constants;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;

namespace presensi_kpu_batu_be.Modules.AttendanceModule
{
    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;
        private readonly ILeaveRequestService _leaveRequestsService;
        private readonly IDepartmentService _departmentService;
        private readonly IGeneralSettingService _settingService;
        private readonly ITimeProviderService _timeProviderService;

        public AttendanceService(AppDbContext context, ILeaveRequestService leaveRequestsService, IDepartmentService departmentService, IGeneralSettingService settingService
            , ITimeProviderService timeProvidersService)
        {
            _context = context;
            _leaveRequestsService = leaveRequestsService;
            _departmentService = departmentService;
            _settingService = settingService;
            _timeProviderService = timeProvidersService;
        }

        public async Task<AttendanceResponse?> GetTodayAttendance(Guid userGuid)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            return await _context.Attendance
                .AsNoTracking()
                .Where(a => a.UserId == userGuid && a.Date == today)
                .Select(a => new AttendanceResponse
                {
                    Guid = a.Guid,
                    UserId = a.UserId,

                    DepartmentId = a.DepartmentId,
                    DepartmentName = a.Department != null ? a.Department.Name : null,

                    Date = a.Date,

                    CheckInTime = a.CheckInTime,
                    CheckInLocation = a.CheckInLocation,
                    CheckInPhotoId = a.CheckInPhotoId,
                    CheckInNotes = a.CheckInNotes,

                    CheckOutTime = a.CheckOutTime,
                    CheckOutLocation = a.CheckOutLocation,
                    CheckOutPhotoId = a.CheckOutPhotoId,
                    CheckOutNotes = a.CheckOutNotes,

                    WorkHours = a.WorkHours,
                    Status = a.Status,

                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt
                })
                .FirstOrDefaultAsync();

        }

        public async Task<Attendance> CheckIn(Guid userId, CheckInDto dto)
        {
            var timezoneId = await _settingService.GetAsync(
                GeneralSettingCodes.TIMEZONE
            );

            var nowUtc = await _timeProviderService.NowAsync();

            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                // Render (Linux) aman Asia/Jakarta
                // fallback Windows untuk dev lokal
                tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }

            var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            // 1. Check leave
            var leaveStatus = await _leaveRequestsService
                .CheckUserLeaveStatusAsync(userId, today);

            if (leaveStatus.IsOnLeave)
                throw new BadRequestException(
                    $"You are on {leaveStatus.LeaveType} today and cannot check in");

            // 2. existing attendance today
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.Date == today);

            if (attendance?.CheckInTime != null)
                throw new BadRequestException("You have already checked in today");

            // 3. Get user department
            var departmentId = await _departmentService
                .GetPrimaryDepartmentIdAsync(userId);

            ////// 4. Verify location
            ////var location = new GeoLocation
            ////{
            ////    Latitude = dto.Latitude,
            ////    Longitude = dto.Longitude,
            ////    Accuracy = dto.Accuracy,
            ////    Provider = dto.Provider
            ////};

            ////var isWithinGeofence = _geoService.IsWithinGeofence(location);

            // 5. Determine status 
            var workStartTimeOnly = TimeOnly.Parse(await _settingService.GetAsync(GeneralSettingCodes.WORKING_START_TIME));

            int lateToleranceMinutes = Convert.ToInt32(await _settingService.GetAsync(GeneralSettingCodes.LATE_TOLERANCE_MINUTES));

            var workStartTime = new DateTime(nowLocal.Year, nowLocal.Month, nowLocal.Day, workStartTimeOnly.Hour, workStartTimeOnly.Minute, 0);

            var lateLimitTime = workStartTime.AddMinutes(lateToleranceMinutes);

            var isLate = nowLocal > lateLimitTime;

            // default
            var status = WorkingStatus.PRESENT;


            //if (!isWithinGeofence)
            //    status = WorkingStatus.REMOTE_WORKING;
            if (isLate)
                status = WorkingStatus.LATE;

            // 6. Create / Update
            if (attendance != null)
            {
                attendance.CheckInTime = nowUtc;
                //attendance.CheckInLocation = location.ToString();
                //attendance.CheckInPhotoId = photoFileGuid;
                attendance.CheckInNotes = dto.Notes;
                attendance.Status = status;
                attendance.UpdatedAt = nowUtc;
            }
            else
            {
                attendance = new Attendance
                {
                    Guid = Guid.NewGuid(),
                    UserId = userId,
                    DepartmentId = departmentId,
                    Date = today,
                    CheckInTime = nowUtc,
                    //CheckInLocation = location.ToString(),
                    //CheckInPhotoId = photoFileGuid,
                    CheckInNotes = dto.Notes,
                    Status = status,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                _context.Attendance.Add(attendance);
            }

            await _context.SaveChangesAsync();

            ////// 7. Update file relation
            ////if (photoFileGuid.HasValue)
            ////{
            ////    await _filesService.UpdateFileRelationAsync(
            ////        photoFileGuid.Value,
            ////        attendance.Guid);
            ////}

            return attendance;
        }
    }
}
