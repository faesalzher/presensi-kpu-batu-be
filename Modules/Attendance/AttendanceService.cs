using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.Attendance.Dto;


namespace presensi_kpu_batu_be.Modules.Attendance
{
    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;

        public AttendanceService(AppDbContext context)
        {
            _context = context;
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

        public async Task<Attendance> CheckInAsync(
    Guid userId,
    CheckInDto dto,
    Guid? photoFileGuid)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var now = DateTime.UtcNow;

            // 1. Check leave
            var leaveStatus = await _leaveRequestsService
                .CheckUserLeaveStatusAsync(userId, today);

            if (leaveStatus.IsOnLeave)
                throw new BadRequestException(
                    $"You are on {leaveStatus.LeaveType} today and cannot check in");

            // 2. Existing attendance today
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.Date == today);

            if (attendance?.CheckInTime != null)
                throw new BadRequestException("You have already checked in today");

            // 3. Get user department
            var departmentId = await _departmentsService
                .GetPrimaryDepartmentIdAsync(userId);

            // 4. Verify location
            var location = new GeoLocation
            {
                Latitude = dto.Latitude,
                Longitude = dto.Longitude,
                Accuracy = dto.Accuracy,
                Provider = dto.Provider
            };

            var isWithinGeofence = _geoService.IsWithinGeofence(location);

            // 5. Determine status
            var lateTolerance = _configService.LateToleranceMinutes;
            var isLate =
                now.Hour > 10 ||
                (now.Hour == 10 && now.Minute > lateTolerance);

            var status = WorkingStatus.PRESENT;

            if (!isWithinGeofence)
                status = WorkingStatus.REMOTE_WORKING;
            else if (isLate)
                status = WorkingStatus.LATE;

            // 6. Create / Update
            if (attendance != null)
            {
                attendance.CheckInTime = now;
                attendance.CheckInLocation = location.ToString();
                attendance.CheckInPhotoId = photoFileGuid;
                attendance.CheckInNotes = dto.Notes;
                attendance.Status = status;
            }
            else
            {
                attendance = new Attendance
                {
                    Guid = Guid.NewGuid(),
                    UserId = userId,
                    DepartmentId = departmentId,
                    Date = today,
                    CheckInTime = now,
                    CheckInLocation = location.ToString(),
                    CheckInPhotoId = photoFileGuid,
                    CheckInNotes = dto.Notes,
                    Status = status,
                    CreatedAt = now
                };

                _context.Attendance.Add(attendance);
            }

            await _context.SaveChangesAsync();

            // 7. Update file relation
            if (photoFileGuid.HasValue)
            {
                await _filesService.UpdateFileRelationAsync(
                    photoFileGuid.Value,
                    attendance.Guid);
            }

            return attendance;
        }

    }
}
