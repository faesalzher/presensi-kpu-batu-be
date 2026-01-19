using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Common.Constants;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Domain.Enums;
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

                    isForgotCheckIn = a.Status == WorkingStatus.INCOMPLETE && a.CheckInTime == null,
                    isForgotCheckOut = a.Status == WorkingStatus.INCOMPLETE && a.CheckOutTime == null,

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

        public async Task<AttendanceResponse> CheckIn(Guid userId, CheckInDto dto)
        {
            // ======================================================
            // 1. TIME & DATE (UTC + LOCAL)
            // ======================================================
            var timezoneId = await _settingService.GetAsync(
                GeneralSettingCodes.TIMEZONE);

            var nowUtc = await _timeProviderService.NowAsync();

            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            // ======================================================
            // 2. VALIDASI HARI KERJA & JAM ABSENSI
            // ======================================================
            var workingDay = await _timeProviderService.GetTodayWorkingInfoAsync();

            if (workingDay.IsHoliday)
                throw new BadRequestException(workingDay.Message);

            if (!workingDay.IsWorkAllowed)
                throw new BadRequestException(workingDay.Message);

            // ======================================================
            // 3. CEK CUTI / IZIN
            // ======================================================
            var leaveStatus = await _leaveRequestsService
                .CheckUserLeaveStatusAsync(userId, today);

            if (leaveStatus.IsOnLeave)
                throw new BadRequestException(
                    $"You are on {leaveStatus.LeaveType} today and cannot check in");

            // ======================================================
            // 4. CEK ATTENDANCE HARI INI
            // ======================================================
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.Date == today);

            if (attendance?.CheckInTime != null)
                throw new BadRequestException("You have already checked in today");

            // ======================================================
            // 5. STATUS KEHADIRAN (DEFAULT)
            // ======================================================
            var status = WorkingStatus.PRESENT;

            // ======================================================
            // 6. CREATE / UPDATE ATTENDANCE
            // ======================================================
            if (attendance == null)
            {
                attendance = new Attendance
                {
                    Guid = Guid.NewGuid(),
                    UserId = userId,
                    Date = today,
                    Status = status,
                    CheckInTime = nowUtc,   // SIMPAN UTC
                    CheckInNotes = dto.Notes,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                _context.Attendance.Add(attendance);
                await _context.SaveChangesAsync();
                // ⬆️ PENTING: supaya attendance.Guid tersedia untuk FK
            }
            else
            {
                attendance.CheckInTime = nowUtc;
                attendance.CheckInNotes = dto.Notes;
                attendance.Status = status;
                attendance.UpdatedAt = nowUtc;

                await _context.SaveChangesAsync();
            }

            // ======================================================
            // 7. DETEKSI TELAT → INSERT VIOLATION
            // ======================================================
            var workStart = TimeOnly.Parse(workingDay.WorkStart!);

            int lateToleranceMinutes = Convert.ToInt32(
                await _settingService.GetAsync(
                    GeneralSettingCodes.LATE_TOLERANCE_MINUTES));

            var workStartTimeLocal = new DateTime(
                nowLocal.Year,
                nowLocal.Month,
                nowLocal.Day,
                workStart.Hour,
                workStart.Minute,
                0);

            var lateLimitTime = workStartTimeLocal
                .AddMinutes(lateToleranceMinutes);

            if (nowLocal > lateLimitTime)
            {
                var violation = new AttendanceViolation
                {
                    Guid = Guid.NewGuid(),
                    AttendanceId = attendance.Guid,
                    Type = AttendanceViolationType.LATE,
                    Source = ViolationSource.CHECK_IN,
                    PenaltyPercent = 2.5m,
                    OccurredAt = nowUtc,
                    Notes = "Terlambat masuk kerja"
                };

                _context.AttendanceViolation.Add(violation);
                await _context.SaveChangesAsync();
            }

            // ======================================================
            // 8. RETURN
            // ======================================================

            var response = new AttendanceResponse
            {
                Guid = attendance.Guid,
                UserId = attendance.UserId,
                Date = attendance.Date,
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                WorkHours = attendance.WorkHours,
                Status = attendance.Status,
            };
            return response;
        }


        public async Task<AttendanceResponse> CheckOut(Guid userId, CheckOutDto dto)
        {
            // ======================================================
            // 1. TIMEZONE & WAKTU
            // ======================================================
            var timezoneId = await _settingService.GetAsync(
                GeneralSettingCodes.TIMEZONE);

            var nowUtc = await _timeProviderService.NowAsync();

            TimeZoneInfo tz;
            try
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            // ======================================================
            // 2. VALIDASI HARI & JAM KERJA
            // ======================================================
            var workingDay = await _timeProviderService.GetTodayWorkingInfoAsync();

            if (workingDay.IsHoliday)
                throw new BadRequestException(workingDay.Message);

            if (!workingDay.IsWorkAllowed)
                throw new BadRequestException(workingDay.Message);

            // ======================================================
            // 3. AMBIL ATTENDANCE
            // ======================================================
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a =>
                    a.UserId == userId &&
                    a.Date == today);

            if (attendance == null)
                throw new BadRequestException("You have not checked in today");

            if (attendance.CheckOutTime != null)
                throw new BadRequestException("You have already checked out today");

            if (attendance.CheckInTime == null)
                throw new BadRequestException("Invalid check-in data");

            // ======================================================
            // 4. HITUNG JAM KERJA (UTC)
            // ======================================================
            var workHours =
                (decimal)(nowUtc - attendance.CheckInTime.Value).TotalHours;

            // ======================================================
            // 5. DETEKSI PULANG CEPAT
            // ======================================================
            var workEnd = TimeOnly.Parse(workingDay.WorkEnd!);

            int earlyLeaveToleranceMinutes = Convert.ToInt32(
                await _settingService.GetAsync(
                    GeneralSettingCodes.EARLY_LEAVE_TOLERANCE_MINUTES));

            var workEndTimeLocal = new DateTime(
                nowLocal.Year,
                nowLocal.Month,
                nowLocal.Day,
                workEnd.Hour,
                workEnd.Minute,
                0);

            var earlyLeaveLimit =
                workEndTimeLocal.AddMinutes(-earlyLeaveToleranceMinutes);

            bool isEarlyDeparture = nowLocal < earlyLeaveLimit;

            // ======================================================
            // 6. UPDATE ATTENDANCE (TANPA UBAH STATUS)
            // ======================================================
            attendance.CheckOutTime = nowUtc; // UTC
            attendance.CheckOutNotes = dto.Notes ?? string.Empty;
            attendance.WorkHours = Math.Round(workHours, 2);
            attendance.UpdatedAt = nowUtc;

            await _context.SaveChangesAsync();

            // ======================================================
            // 7. CATAT VIOLATION (JIKA ADA)
            // ======================================================
            if (isEarlyDeparture)
            {
                var violation = new AttendanceViolation
                {
                    Guid    = Guid.NewGuid(),
                    AttendanceId = attendance.Guid,
                    Type = AttendanceViolationType.EARLY_DEPARTURE,
                    Source = ViolationSource.CHECK_OUT,
                    PenaltyPercent = 2.5m,
                    OccurredAt = nowUtc,
                    Notes = "Pulang sebelum waktunya"
                };
                _context.AttendanceViolation.Add(violation);
                await _context.SaveChangesAsync();
            }

            var response = new AttendanceResponse
            {
                Guid = attendance.Guid,
                UserId = attendance.UserId,
                Date = attendance.Date,
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                WorkHours = attendance.WorkHours,
                Status = attendance.Status,
            };
            return response;
        }

    }
}
