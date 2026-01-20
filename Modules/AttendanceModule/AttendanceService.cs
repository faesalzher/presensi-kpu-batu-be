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


        public async Task RunCutOffCheckInAsync()
        {
            var nowUtc = await _timeProviderService.NowAsync();
            var tz = GetTimeZone();

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            if (nowLocal.TimeOfDay <= new TimeSpan(12, 0, 0))
                return;

            // 1️⃣ Ambil user aktif
            var users = await _userService.GetActiveUsersAsync();
            var userIds = users.Select(u => u.Guid).ToList();

            // 2️⃣ Ambil attendance hari ini SEKALI
            var existingAttendances = await _context.Attendance
                .Where(a => a.Date == today && userIds.Contains(a.UserId))
                .ToListAsync();

            var attendanceByUserId = existingAttendances
                .ToDictionary(a => a.UserId, a => a);

            var newAttendances = new List<Attendance>();
            var newViolations = new List<AttendanceViolation>();

            // 3️⃣ Loop di memory (murah)
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
            }

            // 4️⃣ Simpan SEKALI (atomic)
            if (newAttendances.Any())
            {
                _context.Attendance.AddRange(newAttendances);
                _context.AttendanceViolation.AddRange(newViolations);

                await _context.SaveChangesAsync();
            }
        }

        public async Task RunCutOffCheckOutAsync()
        {
            var nowUtc = await _timeProviderService.NowAsync();
            var tz = GetTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var today = DateOnly.FromDateTime(nowLocal);

            if (nowLocal.TimeOfDay < new TimeSpan(18, 0, 0))
                return;

            // 1️⃣ Ambil user aktif
            var users = await _userService.GetActiveUsersAsync();
            var userIds = users.Select(u => u.Guid).ToList();

            // 2️⃣ Ambil attendance hari ini SEKALI
            var attendances = await _context.Attendance
                .Where(a => a.Date == today && userIds.Contains(a.UserId))
                .ToListAsync();

            var attendanceByUserId = attendances
                .ToDictionary(a => a.UserId, a => a);

            var violationsToAdd = new List<AttendanceViolation>();

            // 3️⃣ Proses semua user aktif
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
                    continue;
                }

                // ===============================
                // CASE 2: CHECK-IN ❌ & CHECK-OUT ❌
                // ===============================
                if (attendance.CheckInTime == null &&
                attendance.CheckOutTime == null)
                {
                    // 1️⃣ hapus violation parsial
                    var partialViolations = await _context.AttendanceViolation
                        .Where(v =>
                            v.AttendanceId == attendance.Guid &&
                            (v.Type == AttendanceViolationType.NOT_CHECKED_IN ||
                             v.Type == AttendanceViolationType.NOT_CHECKED_OUT))
                        .ToListAsync();

                    if (partialViolations.Any())
                        _context.AttendanceViolation.RemoveRange(partialViolations);

                    // 2️⃣ update attendance
                    attendance.Status = WorkingStatus.ABSENT;
                    attendance.UpdatedAt = nowUtc;

                    // 3️⃣ insert violation ABSENT (5%)
                    var absentViolationExists = await _context.AttendanceViolation
                        .AnyAsync(v =>
                            v.AttendanceId == attendance.Guid &&
                            v.Type == AttendanceViolationType.ABSENT);

                    if (!absentViolationExists)
                    {
                        _context.AttendanceViolation.Add(new AttendanceViolation
                        {
                            Guid = Guid.NewGuid(),
                            AttendanceId = attendance.Guid,
                            Type = AttendanceViolationType.ABSENT,
                            Source = ViolationSource.SYSTEM, // 🔥 tambahkan SYSTEM
                            PenaltyPercent = 5.0m,
                            OccurredAt = nowUtc,
                            Notes = "Tidak melakukan presensi masuk dan pulang"
                        });
                    }

                    continue;
                }


                // ===============================
                // CASE 3: CHECK-IN ✔ & CHECK-OUT ❌
                // ===============================
                if (attendance.CheckInTime != null &&
                    attendance.CheckOutTime == null)
                {
                    attendance.Status = WorkingStatus.INCOMPLETE;
                    attendance.UpdatedAt = nowUtc;

                    var hasViolation = await _context.AttendanceViolation
                        .AnyAsync(v =>
                            v.AttendanceId == attendance.Guid &&
                            v.Type == AttendanceViolationType.NOT_CHECKED_OUT);

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
                    }
                }

                // CASE 4: CHECK-IN ✔ & CHECK-OUT ✔
                // → tidak melakukan apa-apa
            }

            // 4️⃣ Simpan SEKALI & atomic

            if (violationsToAdd.Any())
                _context.AttendanceViolation.AddRange(violationsToAdd);

            await _context.SaveChangesAsync();
        }


    }
}
