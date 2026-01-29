using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Common.Constants;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;
using presensi_kpu_batu_be.Modules.UserModule;
using presensi_kpu_batu_be.Modules.TunjanganModule;

namespace presensi_kpu_batu_be.Modules.AttendanceModule
{
    public class AttendanceService : IAttendanceService
    {
        private readonly AppDbContext _context;
        private readonly ILeaveRequestService _leaveRequestsService;
        private readonly IGeneralSettingService _settingService;
        private readonly ITimeProviderService _timeProviderService;
        private readonly IUserService _userService;
        private readonly ITunjanganService _tunjanganService;

        public AttendanceService(AppDbContext context, ILeaveRequestService leaveRequestsService, IGeneralSettingService settingService
            , ITimeProviderService timeProvidersService, IUserService userService, ITunjanganService tunjanganService)
        {
            _context = context;
            _leaveRequestsService = leaveRequestsService;
            _settingService = settingService;
            _timeProviderService = timeProvidersService;
            _userService = userService;
            _tunjanganService = tunjanganService;
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

                    isForgotCheckIn = a.Status == WorkingStatus.PROBLEM && a.CheckInTime == null,
                    isForgotCheckOut = a.Status == WorkingStatus.PROBLEM && a.CheckOutTime == null,

                    CheckInTime = a.CheckInTime,
                    CheckInLocation = a.CheckInLocation,
                    CheckInPhotoId = a.CheckInPhotoId,
                    CheckInNotes = a.CheckInNotes,

                    CheckOutTime = a.CheckOutTime,
                    CheckOutLocation = a.CheckOutLocation,
                    CheckOutPhotoId = a.CheckOutPhotoId,
                    CheckOutNotes = a.CheckOutNotes,

                    WorkHours = a.WorkHours,
                    Status = a.Status.ToString(),
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
                throw new BadRequestException("Anda sedang cuti hari ini");

            // ======================================================
            // 4. AMBIL ATTENDANCE
            // ======================================================
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            if (attendance?.CheckInTime != null)
                throw new BadRequestException("Sudah Checkin");

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
                    Status = lateMinutes > 0 ? WorkingStatus.PROBLEM : WorkingStatus.PRESENT,
                    CheckInTime = nowUtc,
                    CheckInNotes = dto.Notes,
                    LateMinutes = lateMinutes,
                };
                _context.Attendance.Add(attendance);
            }
            else
            {
                attendance.CheckInTime = nowUtc;
                attendance.CheckInNotes = dto.Notes;
                attendance.LateMinutes = lateMinutes;
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
                    const decimal penaltyPercent = 2.5m;

                    var tukinBase = await GetTukinBaseAmountForUserAsync(userId);
                    var penaltyAmount = Math.Round(tukinBase * penaltyPercent / 100m, 2);

                    _context.AttendanceViolation.Add(new AttendanceViolation
                    {
                        Guid = Guid.NewGuid(),
                        AttendanceId = attendance.Guid,
                        Type = AttendanceViolationType.LATE,
                        Source = ViolationSource.CHECK_IN,
                        PenaltyPercent = penaltyPercent,
                        TukinBaseAmount = tukinBase,
                        PenaltyAmount = penaltyAmount,
                        OccurredAt = nowUtc,
                        Notes = lateMinutes <= lateToleranceMinutes
                            ? "Terlambat (dalam batas toleransi, tidak dikompensasi)"
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
                Status = attendance.Status.ToString()
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
            // 3. CEK CUTI
            // ======================================================
            var leaveStatus = await _leaveRequestsService
                .CheckUserLeaveStatusAsync(userId, today);

            if (leaveStatus.IsOnLeave)
                throw new BadRequestException("Anda sedang cuti hari ini");

            // ======================================================
            // 2. AMBIL ATTENDANCE
            // ======================================================
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == today);

            if (attendance == null)
                throw new BadRequestException("Invalid attendance data");

            if (attendance.CheckOutTime != null)
                throw new BadRequestException("Sudah checkout");

            // ======================================================
            // 3. SIMPAN CHECK-OUT & JAM KERJA
            // ======================================================
            attendance.CheckOutTime = nowUtc;
            attendance.CheckOutNotes = dto.Notes ?? string.Empty;
            if (attendance.CheckInTime != null)
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


            DateTime? checkInLocal = null;

            if (attendance.CheckInTime.HasValue)
            {
                checkInLocal = TimeZoneInfo.ConvertTimeFromUtc(
                    attendance.CheckInTime.Value,
                    tz
                );
            }

            bool isLate =
                attendance.CheckInTime.HasValue &&
                attendance.LateMinutes.HasValue &&
                attendance.LateMinutes.Value > 0;

            bool withinTolerance =
                isLate &&
                checkInLocal.HasValue &&
                checkInLocal.Value <= toleranceLimit;

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
                    attendance.Status = WorkingStatus.PROBLEM;

                    const decimal penaltyPercent = 2.5m;
                    var tukinBase = await GetTukinBaseAmountForUserAsync(userId);
                    var penaltyAmount = Math.Round(tukinBase * penaltyPercent / 100m, 2);

                    _context.AttendanceViolation.Add(new AttendanceViolation
                    {
                        Guid = Guid.NewGuid(),
                        AttendanceId = attendance.Guid,
                        Type = AttendanceViolationType.EARLY_DEPARTURE,
                        Source = ViolationSource.CHECK_OUT,
                        PenaltyPercent = penaltyPercent,
                        TukinBaseAmount = tukinBase,
                        PenaltyAmount = penaltyAmount,
                        OccurredAt = nowUtc,
                        Notes = "Pulang sebelum jam kerja selesai"
                    });
                }
            }

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
                Status = attendance.Status.ToString(),
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
                
        public async Task<SchedulerDebugResponse> RunCutOffCheckInAsync(DateOnly? targetDate = null)
        {
            // ======================================================
            // TENTUKAN TANGGAL TARGET
            // ======================================================
            var nowUtc = await _timeProviderService.NowAsync();
            var tz = GetTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            // Gunakan targetDate jika diberikan, otherwise gunakan hari ini
            var today = targetDate ?? DateOnly.FromDateTime(nowLocal);

            // ======================================================
            // VALIDASI HARI KERJA
            // ======================================================
            var isWorkingDay = await _timeProviderService.IsWorkingDayAsync(today);

            var response = new SchedulerDebugResponse
            {
                Scheduler = "CUT_OFF_CHECKIN",
                Date = today,
                ExecutedAtUtc = nowUtc
            };

            if (!isWorkingDay)
            {
                response.AttendanceCreated = 0;
                response.ViolationsAdded = 0;
                response.AffectedUserIds = new();
                // 🔥 TAMBAH PESAN HOLIDAY
                response.DebugMessage = "Skipped: Holiday or non-working day";
                return response;
            }

            // 1️⃣ Ambil user aktif
            var users = await _userService.GetActiveUsersAsync();
            var usersToProcess = users.Select(u => u.Guid).ToList();

            // 2️⃣ Ambil user yang CUTI pada tanggal target
            var usersOnLeave = await _leaveRequestsService.GetUserIdsOnLeaveAsync(today);

            // 3️⃣ Filter → hanya yang WAJIB presensi
            var userIds = usersToProcess
                .Except(usersOnLeave)
                .ToList();

            // 4️⃣ Ambil attendance pada tanggal target
            var existingAttendances = await _context.Attendance
                .Where(a => a.Date == today && userIds.Contains(a.UserId))
                .ToListAsync();

            var attendanceByUserId = existingAttendances
                .ToDictionary(a => a.UserId, a => a);

            // 🔥 compute OccurredAtUtc aligned to targetDate (use nowLocal time-of-day on targetDate)
            var executedLocalTime = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var occurredLocal = new DateTime(today.Year, today.Month, today.Day,
                                             executedLocalTime.Hour, executedLocalTime.Minute, executedLocalTime.Second);
            var occurredAtUtc = TimeZoneInfo.ConvertTimeToUtc(occurredLocal, tz);

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
                    Status = WorkingStatus.PROBLEM,
                    CheckInTime = null,
                    CheckOutTime = null,
                };

                newAttendances.Add(attendance);

                const decimal penaltyPercent = 2.5m;
                var tukinBase = await GetTukinBaseAmountForUserAsync(user.Guid);
                var penaltyAmount = Math.Round(tukinBase * penaltyPercent / 100m, 2);

                newViolations.Add(new AttendanceViolation
                {
                    Guid = Guid.NewGuid(),
                    AttendanceId = attendance.Guid,
                    Type = AttendanceViolationType.NOT_CHECKED_IN,
                    Source = ViolationSource.CHECK_IN,
                    PenaltyPercent = penaltyPercent,
                    TukinBaseAmount = tukinBase,
                    PenaltyAmount = penaltyAmount,
                    OccurredAt = occurredAtUtc,
                    Notes = "Tidak presensi masuk"
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

        public async Task<SchedulerDebugResponse> RunCutOffCheckOutAsync(DateOnly? targetDate = null)
        {
            // ======================================================
            // TENTUKAN TANGGAL TARGET
            // ======================================================
            var nowUtc = await _timeProviderService.NowAsync();
            var tz = GetTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);

            // Gunakan targetDate jika diberikan, otherwise gunakan hari ini
            var today = targetDate ?? DateOnly.FromDateTime(nowLocal);

            // ======================================================
            // VALIDASI HARI KERJA
            // ======================================================
            var isWorkingDay = await _timeProviderService.IsWorkingDayAsync(today);

            var response = new SchedulerDebugResponse
            {
                Scheduler = "CUT_OFF_CHECKOUT",
                Date = today,
                ExecutedAtUtc = nowUtc
            };

            if (!isWorkingDay)
            {
                // 🔥 TAMBAH PESAN HOLIDAY
                response.DebugMessage = "Skipped: Holiday or non-working day";
                return response;
            }

            // 1️⃣ Ambil user aktif
            var users = await _userService.GetActiveUsersAsync();
            var usersToProcess = users.Select(u => u.Guid).ToList();

            // 2️⃣ Ambil user yang CUTI pada tanggal target
            var usersOnLeave = await _leaveRequestsService.GetUserIdsOnLeaveAsync(today);

            // 3️⃣ Filter → hanya yang WAJIB presensi
            var userIds = usersToProcess
                .Except(usersOnLeave)
                .ToList();

            // 4️⃣ Ambil attendance pada tanggal target
            var attendances = await _context.Attendance
                .Where(a => a.Date == today && userIds.Contains(a.UserId))
                .ToListAsync();

            var attendanceByUserId = attendances
                .ToDictionary(a => a.UserId, a => a);

            // Preload violation pada tanggal target
            var violations = await _context.AttendanceViolation
                .Where(v => attendances.Select(a => a.Guid).Contains(v.AttendanceId))
                .ToListAsync();

            var violationsByAttendanceId = violations
                .GroupBy(v => v.AttendanceId)
                .ToDictionary(g => g.Key, g => g.ToList());

            // 🔥 compute OccurredAtUtc aligned to targetDate (use nowLocal time-of-day on targetDate)
            var executedLocalTime2 = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var occurredLocal2 = new DateTime(today.Year, today.Month, today.Day,
                                              executedLocalTime2.Hour, executedLocalTime2.Minute, executedLocalTime2.Second);
            var occurredAtUtc2 = TimeZoneInfo.ConvertTimeToUtc(occurredLocal2, tz);

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
                    response.AttendanceUpdated++;
                    response.AffectedUserIds.Add(attendance.UserId);

                    var hasAbsent = existing?.Any(v => v.Type == AttendanceViolationType.ABSENT) == true;
                    if (!hasAbsent)
                    {
                        const decimal penaltyPercent = 5.0m;
                        var tukinBase = await GetTukinBaseAmountForUserAsync(attendance.UserId);
                        var penaltyAmount = Math.Round(tukinBase * penaltyPercent / 100m, 2);

                        violationsToAdd.Add(new AttendanceViolation
                        {
                            Guid = Guid.NewGuid(),
                            AttendanceId = attendance.Guid,
                            Type = AttendanceViolationType.ABSENT,
                            Source = ViolationSource.SYSTEM,
                            PenaltyPercent = penaltyPercent,
                            TukinBaseAmount = tukinBase,
                            PenaltyAmount = penaltyAmount,
                            OccurredAt = occurredAtUtc2,
                            Notes = "Tidak presensi masuk dan pulang"
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
                    attendance.Status = WorkingStatus.PROBLEM;
                    response.AttendanceUpdated++;
                    response.AffectedUserIds.Add(attendance.UserId);

                    var hasViolation = violationsByAttendanceId.TryGetValue(attendance.Guid, out var vlist)
                        && vlist.Any(v => v.Type == AttendanceViolationType.NOT_CHECKED_OUT);

                    if (!hasViolation)
                    {
                        const decimal penaltyPercent = 2.5m;
                        var tukinBase = await GetTukinBaseAmountForUserAsync(attendance.UserId);
                        var penaltyAmount = Math.Round(tukinBase * penaltyPercent / 100m, 2);

                        violationsToAdd.Add(new AttendanceViolation
                        {
                            Guid = Guid.NewGuid(),
                            AttendanceId = attendance.Guid,
                            Type = AttendanceViolationType.NOT_CHECKED_OUT,
                            Source = ViolationSource.CHECK_OUT,
                            PenaltyPercent = penaltyPercent,
                            TukinBaseAmount = tukinBase,
                            PenaltyAmount = penaltyAmount,
                            OccurredAt = occurredAtUtc2,
                            Notes = "Tidak presensi pulang"
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

        public async Task<List<AttendanceResponse>> GetAttendanceAsync(
            AttendanceQueryParams query)
        {
            var q = _context.Attendance
                .AsNoTracking()
                .AsQueryable();

            // 🔐 user wajib
            if (query.UserId.HasValue)
            {
                q = q.Where(x => x.UserId == query.UserId.Value);
            }

            // 📅 filter tanggal
            if (!string.IsNullOrEmpty(query.StartDate))
            {
                var start = DateOnly.Parse(query.StartDate);
                q = q.Where(x => x.Date >= start);
            }

            if (!string.IsNullOrEmpty(query.EndDate))
            {
                var end = DateOnly.Parse(query.EndDate);
                q = q.Where(x => x.Date <= end);
            }

            // 🏢 department
            if (query.DepartmentId.HasValue)
            {
                q = q.Where(x => x.DepartmentId == query.DepartmentId.Value);
            }

            return await q
                .OrderByDescending(a => a.Date)
                .Select(a => new AttendanceResponse
                {
                    Guid = a.Guid,
                    UserId = a.UserId,

                    DepartmentId = a.DepartmentId,
                    DepartmentName = a.Department != null ? a.Department.Name : null,

                    Date = a.Date,

                    isForgotCheckIn = a.Status == WorkingStatus.PROBLEM && a.CheckInTime == null,
                    isForgotCheckOut = a.Status == WorkingStatus.PROBLEM && a.CheckOutTime == null,

                    CheckInTime = a.CheckInTime,
                    CheckInLocation = a.CheckInLocation,
                    CheckInPhotoId = a.CheckInPhotoId,
                    CheckInNotes = a.CheckInNotes,

                    CheckOutTime = a.CheckOutTime,
                    CheckOutLocation = a.CheckOutLocation,
                    CheckOutPhotoId = a.CheckOutPhotoId,
                    CheckOutNotes = a.CheckOutNotes,

                    WorkHours = a.WorkHours,
                    Status = a.Status.ToString(),
                })
                .ToListAsync();
        }

        public async Task<AttendanceResponse?> GetAttendanceByGuidAsync(
            Guid attendanceGuid,
            Guid userId)
        {
            return await _context.Attendance
                .AsNoTracking()
                .Where(a => a.Guid == attendanceGuid && a.UserId == userId)
                .Select(a => new AttendanceResponse
                {
                    Guid = a.Guid,
                    UserId = a.UserId,

                    DepartmentId = a.DepartmentId,
                    DepartmentName = a.Department != null ? a.Department.Name : null,

                    Date = a.Date,

                    isForgotCheckIn = a.Status == WorkingStatus.PROBLEM && a.CheckInTime == null,
                    isForgotCheckOut = a.Status == WorkingStatus.PROBLEM && a.CheckOutTime == null,

                    CheckInTime = a.CheckInTime,
                    CheckInLocation = a.CheckInLocation,
                    CheckInPhotoId = a.CheckInPhotoId,
                    CheckInNotes = a.CheckInNotes,

                    CheckOutTime = a.CheckOutTime,
                    CheckOutLocation = a.CheckOutLocation,
                    CheckOutPhotoId = a.CheckOutPhotoId,
                    CheckOutNotes = a.CheckOutNotes,

                    WorkHours = a.WorkHours,
                    Status = a.Status.ToString(),
                    ViolationNotes = string.Join(
                        ", ",
                        a.Violation.Select(v => v.Notes)
                    ),
                })
                .FirstOrDefaultAsync();
        }

        // helper: ambil tukin base dari ref_tunjangan_kinerja
        private Task<decimal> GetTukinBaseAmountForUserAsync(Guid userId)
            => _tunjanganService.GetTukinBaseAmountForUserAsync(userId);
    }
}
