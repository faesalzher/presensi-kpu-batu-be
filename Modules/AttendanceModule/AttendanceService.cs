using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Common.Constants;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;
using presensi_kpu_batu_be.Modules.UserModule;

namespace presensi_kpu_batu_be.Modules.AttendanceModule
{
    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;
        private readonly ILeaveRequestService _leaveRequestsService;
        private readonly IDepartmentService _departmentService;
        private readonly IGeneralSettingService _settingService;
        private readonly ITimeProviderService _timeProviderService;
        private readonly IUserService _userService;

        
        public AttendanceService(AppDbContext context, ILeaveRequestService leaveRequestsService, IDepartmentService departmentService, IGeneralSettingService settingService
            , ITimeProviderService timeProvidersService, IUserService userService)
        {
            _context = context;
            _leaveRequestsService = leaveRequestsService;
            _departmentService = departmentService;
            _settingService = settingService;
            _timeProviderService = timeProvidersService;
            _userService = userService;
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
            // 1. TIME & DATE
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
            // 2. VALIDASI HARI KERJA
            // ======================================================
            var workingDay = await _timeProviderService.GetTodayWorkingInfoAsync();

            if (workingDay.IsHoliday || !workingDay.IsWorkAllowed)
                throw new BadRequestException(workingDay.Message);

            // ======================================================
            // 3. CEK CUTI
            // ======================================================
            var leaveStatus = await _leaveRequestsService
                .CheckUserLeaveStatusAsync(userId, today);

            if (leaveStatus.IsOnLeave)
                throw new BadRequestException("You are on leave today");

            // ======================================================
            // 4. AMBIL ATTENDANCE
            // ======================================================
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            if (attendance?.CheckInTime != null)
                throw new BadRequestException("Already checked in");

            // ======================================================
            // 5. HITUNG TELAT
            // ======================================================
            var workStart = TimeOnly.Parse(workingDay.WorkStart!);

            var workStartLocal = new DateTime(
                nowLocal.Year,
                nowLocal.Month,
                nowLocal.Day,
                workStart.Hour,
                workStart.Minute,
                0);

            int lateMinutes = 0;
            if (nowLocal > workStartLocal)
                lateMinutes = (int)(nowLocal - workStartLocal).TotalMinutes;

            // ======================================================
            // 6. SIMPAN ATTENDANCE
            // ======================================================
            if (attendance == null)
            {
                attendance = new Attendance
                {
                    Guid = Guid.NewGuid(),
                    UserId = userId,
                    Date = today,
                    Status = WorkingStatus.PRESENT,
                    CheckInTime = nowUtc,
                    CheckInNotes = dto.Notes,
                    LateMinutes = lateMinutes,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };
                _context.Attendance.Add(attendance);
            }
            else
            {
                attendance.CheckInTime = nowUtc;
                attendance.CheckInNotes = dto.Notes;
                attendance.LateMinutes = lateMinutes;
                attendance.UpdatedAt = nowUtc;
            }

            await _context.SaveChangesAsync();

            // ======================================================
            // 7. CATAT VIOLATION TELAT (SELALU, JIKA TELAT)
            // ======================================================

            int lateToleranceMinutes = Convert.ToInt32(
                await _settingService.GetAsync(
                    GeneralSettingCodes.LATE_TOLERANCE_MINUTES));
            if (lateMinutes > 0)
            {
                var alreadyExists = await _context.AttendanceViolation
                    .AnyAsync(v =>
                        v.AttendanceId == attendance.Guid &&
                        v.Type == AttendanceViolationType.LATE);

                if (!alreadyExists)
                {
                    _context.AttendanceViolation.Add(new AttendanceViolation
                    {
                        Guid = Guid.NewGuid(),
                        AttendanceId = attendance.Guid,
                        Type = AttendanceViolationType.LATE,
                        Source = ViolationSource.CHECK_IN,
                        PenaltyPercent = 2.5m,
                        OccurredAt = nowUtc,
                        Notes = lateMinutes <= lateToleranceMinutes
                            ? "Terlambat (dalam batas toleransi, menunggu kompensasi)"
                            : "Terlambat masuk kerja"
                    });

                    await _context.SaveChangesAsync();
                }
            }

            return new AttendanceResponse
            {
                Guid = attendance.Guid,
                UserId = attendance.UserId,
                Date = attendance.Date,
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                WorkHours = attendance.WorkHours,
                Status = attendance.Status
            };
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
            // VALIDASI HARI & JAM KERJA
            // ======================================================
            var workingDay = await _timeProviderService.GetTodayWorkingInfoAsync();

            if (workingDay.IsHoliday)
                throw new BadRequestException(workingDay.Message);

            if (!workingDay.IsWorkAllowed)
                throw new BadRequestException(workingDay.Message);

            // ======================================================
            // 2. AMBIL ATTENDANCE
            // ======================================================
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            if (attendance == null || attendance.CheckInTime == null)
                throw new BadRequestException("Invalid attendance data");

            if (attendance.CheckOutTime != null)
                throw new BadRequestException("Already checked out today");

            // ======================================================
            // 3. SIMPAN CHECK-OUT & JAM KERJA
            // ======================================================
            attendance.CheckOutTime = nowUtc;
            attendance.CheckOutNotes = dto.Notes ?? string.Empty;
            attendance.WorkHours = Math.Round(
                (decimal)(nowUtc - attendance.CheckInTime.Value).TotalHours, 2);

            // ======================================================
            // 4. DATA JAM KERJA & TOLERANSI
            // ======================================================
            var workStart = TimeOnly.Parse(workingDay.WorkStart!);
            var workEnd = TimeOnly.Parse(workingDay.WorkEnd!);

            var workStartLocal = new DateTime(
                today.Year, today.Month, today.Day,
                workStart.Hour, workStart.Minute, 0);

            var workEndLocal = new DateTime(
                today.Year, today.Month, today.Day,
                workEnd.Hour, workEnd.Minute, 0);

            int lateToleranceMinutes = Convert.ToInt32(
                await _settingService.GetAsync(
                    GeneralSettingCodes.LATE_TOLERANCE_MINUTES));

            var toleranceLimit =
                workStartLocal.AddMinutes(lateToleranceMinutes);

            var checkInLocal =
                TimeZoneInfo.ConvertTimeFromUtc(attendance.CheckInTime.Value, tz);

            bool isLate =
                attendance.LateMinutes.HasValue &&
                attendance.LateMinutes.Value > 0;

            bool withinTolerance =
                isLate && checkInLocal <= toleranceLimit;

            // ======================================================
            // 5. HAPUS VIOLATION TELAT JIKA KOMPENSASI TERPENUHI
            //    (HANYA UNTUK TELAT DALAM TOLERANSI)
            // ======================================================
            if (withinTolerance)
            {
                var requiredCheckout =
                    workEndLocal.AddMinutes(attendance.LateMinutes!.Value);

                if (nowLocal >= requiredCheckout)
                {
                    var lateViolation = await _context.AttendanceViolation
                        .FirstOrDefaultAsync(v =>
                            v.AttendanceId == attendance.Guid &&
                            v.Type == AttendanceViolationType.LATE);

                    if (lateViolation != null)
                        _context.AttendanceViolation.Remove(lateViolation);
                }
            }
            else
            {
                // ======================================================
                // 6. EARLY DEPARTURE
                //    - TIDAK TELAT
                //    - ATAU TELAT > BATAS TOLERANSI
                // ======================================================
                if (nowLocal < workEndLocal)
                {
                    _context.AttendanceViolation.Add(new AttendanceViolation
                    {
                        Guid = Guid.NewGuid(),
                        AttendanceId = attendance.Guid,
                        Type = AttendanceViolationType.EARLY_DEPARTURE,
                        Source = ViolationSource.CHECK_OUT,
                        PenaltyPercent = 2.5m,
                        OccurredAt = nowUtc,
                        Notes = "Pulang sebelum jam kerja selesai"
                    });
                }
            }

            attendance.UpdatedAt = nowUtc;
            await _context.SaveChangesAsync();

            // ======================================================
            // 7. RETURN
            // ======================================================
            return new AttendanceResponse
            {
                Guid = attendance.Guid,
                UserId = attendance.UserId,
                Date = attendance.Date,
                CheckInTime = attendance.CheckInTime,
                CheckOutTime = attendance.CheckOutTime,
                WorkHours = attendance.WorkHours,
                Status = attendance.Status
            };
        }


        private TimeZoneInfo GetTimeZone()
        {
            try
            {
                var timezoneId = _settingService
                    .GetAsync(GeneralSettingCodes.TIMEZONE)
                    .GetAwaiter().GetResult();

                return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }


        public async Task<SchedulerDebugResponse> RunCutOffCheckInAsync()
        {
            var nowUtc = await _timeProviderService.NowAsync();
            var tz = GetTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            var response = new SchedulerDebugResponse
            {
                Scheduler = "CUT_OFF_CHECKIN",
                Date = today,
                ExecutedAtUtc = nowUtc
            };

            if (nowLocal.TimeOfDay <= new TimeSpan(12, 0, 0))
                return response;

            // 1️⃣ Ambil user aktif
            var users = await _userService.GetActiveUsersAsync();
            var userIds = users.Select(u => u.Guid).ToList();

            // 2️⃣ Ambil attendance hari ini
            var existingAttendances = await _context.Attendance
                .Where(a => a.Date == today && userIds.Contains(a.UserId))
                .ToListAsync();

            var attendanceByUserId = existingAttendances
                .ToDictionary(a => a.UserId, a => a);

            var newAttendances = new List<Attendance>();
            var newViolations = new List<AttendanceViolation>();

            foreach (var user in users)
            {
                if (attendanceByUserId.ContainsKey(user.Guid))
                    continue;

                var attendance = new Attendance
                {
                    Guid = Guid.NewGuid(),
                    UserId = user.Guid,
                    Date = today,
                    Status = WorkingStatus.INCOMPLETE,
                    CheckInTime = null,
                    CheckOutTime = null,
                    CreatedAt = nowUtc,
                    UpdatedAt = nowUtc
                };

                newAttendances.Add(attendance);

                newViolations.Add(new AttendanceViolation
                {
                    Guid = Guid.NewGuid(),
                    AttendanceId = attendance.Guid,
                    Type = AttendanceViolationType.NOT_CHECKED_IN,
                    Source = ViolationSource.CHECK_IN,
                    PenaltyPercent = 2.5m,
                    OccurredAt = nowUtc,
                    Notes = "Tidak melakukan absen masuk sampai cut off"
                });

                response.AttendanceCreated++;
                response.ViolationsAdded++;
                response.AffectedUserIds.Add(user.Guid);
            }

            if (newAttendances.Any())
            {
                _context.Attendance.AddRange(newAttendances);
                _context.AttendanceViolation.AddRange(newViolations);
                await _context.SaveChangesAsync();
            }

            return response;
        }


        public async Task<SchedulerDebugResponse> RunCutOffCheckOutAsync()
        {
            var nowUtc = await _timeProviderService.NowAsync();
            var tz = GetTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            var response = new SchedulerDebugResponse
            {
                Scheduler = "CUT_OFF_CHECKOUT",
                Date = today,
                ExecutedAtUtc = nowUtc
            };

            if (nowLocal.TimeOfDay < new TimeSpan(18, 0, 0))
                return response;

            // 1️⃣ Ambil user aktif
            var users = await _userService.GetActiveUsersAsync();
            var userIds = users.Select(u => u.Guid).ToList();

            // 2️⃣ Ambil attendance hari ini
            var attendances = await _context.Attendance
                .Where(a => a.Date == today && userIds.Contains(a.UserId))
                .ToListAsync();

            var attendanceByUserId = attendances
                .ToDictionary(a => a.UserId, a => a);

            // preload violation hari ini
            var violations = await _context.AttendanceViolation
                .Where(v => attendances.Select(a => a.Guid).Contains(v.AttendanceId))
                .ToListAsync();

            var violationsByAttendanceId = violations
                .GroupBy(v => v.AttendanceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            var violationsToAdd = new List<AttendanceViolation>();

            foreach (var userId in userIds)
            {
                // ===============================
                // CASE 1: BELUM ADA ATTENDANCE
                // ===============================
                if (!attendanceByUserId.TryGetValue(userId, out var attendance))
                {
                    attendance = new Attendance
                    {
                        Guid = Guid.NewGuid(),
                        UserId = userId,
                        Date = today,
                        Status = WorkingStatus.ABSENT,
                        CheckInTime = null,
                        CheckOutTime = null,
                        CreatedAt = nowUtc,
                        UpdatedAt = nowUtc
                    };

                    _context.Attendance.Add(attendance);
                    response.AttendanceCreated++;
                    response.AffectedUserIds.Add(userId);
                    continue;
                }

                // ===============================
                // CASE 2: CHECK-IN ❌ & CHECK-OUT ❌
                // ===============================
                if (attendance.CheckInTime == null && attendance.CheckOutTime == null)
                {
                    if (violationsByAttendanceId.TryGetValue(attendance.Guid, out var existing))
                    {
                        var partials = existing
                            .Where(v => v.Type == AttendanceViolationType.NOT_CHECKED_IN ||
                                        v.Type == AttendanceViolationType.NOT_CHECKED_OUT)
                            .ToList();

                        if (partials.Any())
                        {
                            _context.AttendanceViolation.RemoveRange(partials);
                            response.ViolationsRemoved += partials.Count;
                        }
                    }

                    attendance.Status = WorkingStatus.ABSENT;
                    attendance.UpdatedAt = nowUtc;
                    response.AttendanceUpdated++;
                    response.AffectedUserIds.Add(attendance.UserId);

                    var hasAbsent = existing?.Any(v => v.Type == AttendanceViolationType.ABSENT) == true;
                    if (!hasAbsent)
                    {
                        violationsToAdd.Add(new AttendanceViolation
                        {
                            Guid = Guid.NewGuid(),
                            AttendanceId = attendance.Guid,
                            Type = AttendanceViolationType.ABSENT,
                            Source = ViolationSource.SYSTEM,
                            PenaltyPercent = 5.0m,
                            OccurredAt = nowUtc,
                            Notes = "Tidak melakukan presensi masuk dan pulang"
                        });

                        response.ViolationsAdded++;
                    }

                    continue;
                }

                // ===============================
                // CASE 3: CHECK-IN ✔ & CHECK-OUT ❌
                // ===============================
                if (attendance.CheckInTime != null && attendance.CheckOutTime == null)
                {
                    attendance.Status = WorkingStatus.INCOMPLETE;
                    attendance.UpdatedAt = nowUtc;
                    response.AttendanceUpdated++;
                    response.AffectedUserIds.Add(attendance.UserId);

                    var hasViolation = violationsByAttendanceId.TryGetValue(attendance.Guid, out var vlist)
                        && vlist.Any(v => v.Type == AttendanceViolationType.NOT_CHECKED_OUT);

                    if (!hasViolation)
                    {
                        violationsToAdd.Add(new AttendanceViolation
                        {
                            Guid = Guid.NewGuid(),
                            AttendanceId = attendance.Guid,
                            Type = AttendanceViolationType.NOT_CHECKED_OUT,
                            Source = ViolationSource.CHECK_OUT,
                            PenaltyPercent = 2.5m,
                            OccurredAt = nowUtc,
                            Notes = "Tidak melakukan absen pulang sampai cut off"
                        });

                        response.ViolationsAdded++;
                    }
                }
            }

            if (violationsToAdd.Any())
                _context.AttendanceViolation.AddRange(violationsToAdd);

            await _context.SaveChangesAsync();
            return response;
        }



    }
}
