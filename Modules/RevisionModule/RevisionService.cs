using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.RevisionModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;

namespace presensi_kpu_batu_be.Modules.RevisionModule
{
    public class RevisionService : IRevisionService
    {
        private readonly AppDbContext _context;
        private readonly IGeneralSettingService _settingService;
        private readonly ITimeProviderService _timeProviderService;

        public RevisionService(AppDbContext context, IGeneralSettingService settingService, ITimeProviderService timeProviderService)
        {
            _context = context;
            _settingService = settingService;
            _timeProviderService = timeProviderService;
        }

        public async Task<List<CorrectionResponseDto>> QueryCorrectionsAsync(QueryCorrectionsDto? query)
        {
            var q = _context.AttendanceRevision.AsNoTracking().AsQueryable();

            if (query?.UserId is { } userId)
                q = q.Where(x => x.RequestedBy == userId);

            if (query?.AttendanceId is { } attendanceId)
                q = q.Where(x => x.AttendanceId == attendanceId);

            if (query?.DepartmentId is { } deptId)
                q = q.Where(x => x.Attendance.DepartmentId == deptId);

            if (query?.Status is { Count: > 0 } statuses)
            {
                var parsed = statuses.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
                if (parsed.Count > 0)
                    q = q.Where(x => parsed.Contains(x.Status));
                else
                    q = q.Where(_ => false);
            }

            DateOnly? start = query?.StartDate is { } sd ? DateOnly.FromDateTime(sd.ToUniversalTime()) : null;
            DateOnly? end = query?.EndDate is { } ed ? DateOnly.FromDateTime(ed.ToUniversalTime()) : null;

            if (start.HasValue)
                q = q.Where(x => x.Date >= start.Value);
            if (end.HasValue)
                q = q.Where(x => x.Date <= end.Value);

            // apply ordering
            q = q.OrderByDescending(x => x.CreatedAt);

            // pagination
            if (query?.Page is { } p && query?.Limit is { } l && p > 0 && l > 0)
            {
                var skip = (p - 1) * l;
                q = q.Skip(skip).Take(l);
            }

            var rows = await q
                .GroupJoin(
                    _context.Users.AsNoTracking(),
                    r => r.RequestedBy,
                    u => u.Guid,
                    (r, users) => new { r, users }
                )
                .SelectMany(
                    x => x.users.DefaultIfEmpty(),
                    (x, u) => new
                    {
                        Revision = x.r,
                        User = u,
                        AttendanceUserId = _context.Attendance
                            .Where(a => a.Guid == x.r.AttendanceId)
                            .Select(a => a.UserId)
                            .FirstOrDefault()
                    }
                )
                .ToListAsync();

            return rows.Select(x => new CorrectionResponseDto
            {
                Id = x.Revision.Id,
                AttendanceId = x.Revision.AttendanceId,
                Date = x.Revision.Date,
                Type = x.Revision.Type,
                ReasonCode = x.Revision.ReasonCode,
                ReasonDescription = x.Revision.ReasonDescription,
                CheckInTimeOld = x.Revision.CheckInTimeOld,
                CheckInTimeNew = x.Revision.CheckInTimeNew,
                CheckOutTimeOld = x.Revision.CheckOutTimeOld,
                CheckOutTimeNew = x.Revision.CheckOutTimeNew,
                Status = x.Revision.Status,
                RequestedBy = x.Revision.RequestedBy,
                ProfileImageUrl = x.User != null ? x.User.ProfileImageUrl : null,
                Username = x.User != null ? x.User.FullName : null,
                Nip = x.User != null ? x.User.Nip : null,
                ApprovedBy = x.Revision.ApprovedBy,
                ApprovedAt = x.Revision.ApprovedAt,
                CreatedAt = x.Revision.CreatedAt,
                UpdatedAt = x.Revision.UpdatedAt
            }).ToList();
        }

        public async Task<AttendanceRevision> CreateCorrectionAsync(Guid requestedBy, CreateAttendanceCorrectionDto dto)
        {
            var attendance = await _context.Attendance
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Guid == dto.AttendanceId);

            if (attendance == null)
                throw new NotFoundException("Attendance not found");

            var entity = new AttendanceRevision
            {
                AttendanceId = dto.AttendanceId,
                Date = dto.Date,
                Type = dto.Type.Trim(),
                ReasonCode = string.IsNullOrWhiteSpace(dto.ReasonCode) ? null : dto.ReasonCode.Trim(),
                ReasonDescription = string.IsNullOrWhiteSpace(dto.ReasonDescription) ? null : dto.ReasonDescription.Trim(),
                CheckInTimeOld = dto.CheckInTimeOld,
                CheckInTimeNew = dto.CheckInTimeNew,
                CheckOutTimeOld = dto.CheckOutTimeOld,
                CheckOutTimeNew = dto.CheckOutTimeNew,
                RequestedBy = requestedBy,
                Status = "PENDING"
            };

            _context.AttendanceRevision.Add(entity);
            await _context.SaveChangesAsync();

            return entity;
        }

        public async Task<List<CorrectionResponseDto>> GetPendingCorrectionsAsync(Guid? departmentId)
        {
            var q = _context.AttendanceRevision
                .AsNoTracking()
                .Where(x => x.Status == "PENDING");

            if (departmentId.HasValue)
                q = q.Where(x => x.Attendance.DepartmentId == departmentId.Value);

            return await q
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CorrectionResponseDto
                {
                    Id = x.Id,
                    AttendanceId = x.AttendanceId,
                    Date = x.Date,
                    Type = x.Type,
                    ReasonCode = x.ReasonCode,
                    ReasonDescription = x.ReasonDescription,
                    CheckInTimeOld = x.CheckInTimeOld,
                    CheckInTimeNew = x.CheckInTimeNew,
                    CheckOutTimeOld = x.CheckOutTimeOld,
                    CheckOutTimeNew = x.CheckOutTimeNew,
                    Status = x.Status,
                    RequestedBy = x.RequestedBy,
                    ProfileImageUrl = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.ProfileImageUrl)
                                        .FirstOrDefault(),
                    Username = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.FullName)
                                        .FirstOrDefault(),
                    Nip = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.Nip)
                                        .FirstOrDefault(),
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<CorrectionResponseDto>> GetMyCorrectionsByAttendanceIdAsync(Guid requestedBy, Guid attendanceId)
        {
            return await _context.AttendanceRevision
                .AsNoTracking()
                .Where(x => x.RequestedBy == requestedBy && x.AttendanceId == attendanceId)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CorrectionResponseDto
                {
                    Id = x.Id,
                    AttendanceId = x.AttendanceId,
                    Date = x.Date,
                    Type = x.Type,
                    ReasonCode = x.ReasonCode,
                    ReasonDescription = x.ReasonDescription,
                    CheckInTimeOld = x.CheckInTimeOld,
                    CheckInTimeNew = x.CheckInTimeNew,
                    CheckOutTimeOld = x.CheckOutTimeOld,
                    CheckOutTimeNew = x.CheckOutTimeNew,
                    Status = x.Status,
                    RequestedBy = x.RequestedBy,
                    ProfileImageUrl = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.ProfileImageUrl)
                                        .FirstOrDefault(),
                    Username = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.FullName)
                                        .FirstOrDefault(),
                    Nip = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.Nip)
                                        .FirstOrDefault(),
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<List<CorrectionResponseDto>> GetMyCorrectionsAsync(Guid requestedBy)
        {
            return await _context.AttendanceRevision
                .AsNoTracking()
                .Where(x => x.RequestedBy == requestedBy)
                .OrderByDescending(x => x.CreatedAt)
                .Select(x => new CorrectionResponseDto
                {
                    Id = x.Id,
                    AttendanceId = x.AttendanceId,
                    Date = x.Date,
                    Type = x.Type,
                    ReasonCode = x.ReasonCode,
                    ReasonDescription = x.ReasonDescription,
                    CheckInTimeOld = x.CheckInTimeOld,
                    CheckInTimeNew = x.CheckInTimeNew,
                    CheckOutTimeOld = x.CheckOutTimeOld,
                    CheckOutTimeNew = x.CheckOutTimeNew,
                    Status = x.Status,
                    RequestedBy = x.RequestedBy,
                    ProfileImageUrl = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.ProfileImageUrl)
                                        .FirstOrDefault(),
                    Username = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.FullName)
                                        .FirstOrDefault(),
                    Nip = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.Nip)
                                        .FirstOrDefault(),
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .ToListAsync();
        }

        public async Task<CorrectionResponseDto?> GetMyCorrectionByIdAsync(Guid requestedBy, Guid id)
        {
            return await _context.AttendanceRevision
                .AsNoTracking()
                .Where(x => x.RequestedBy == requestedBy && x.Id == id)
                .Select(x => new CorrectionResponseDto
                {
                    Id = x.Id,
                    AttendanceId = x.AttendanceId,
                    Date = x.Date,
                    Type = x.Type,
                    ReasonCode = x.ReasonCode,
                    ReasonDescription = x.ReasonDescription,
                    CheckInTimeOld = x.CheckInTimeOld,
                    CheckInTimeNew = x.CheckInTimeNew,
                    CheckOutTimeOld = x.CheckOutTimeOld,
                    CheckOutTimeNew = x.CheckOutTimeNew,
                    Status = x.Status,
                    RequestedBy = x.RequestedBy,
                    ProfileImageUrl = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.ProfileImageUrl)
                                        .FirstOrDefault(),
                    Username = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.FullName)
                                        .FirstOrDefault(),
                    Nip = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.Nip)
                                        .FirstOrDefault(),
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CorrectionResponseDto?> GetCorrectionByIdAsync(Guid id)
        {
            return await _context.AttendanceRevision
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new CorrectionResponseDto
                {
                    Id = x.Id,
                    AttendanceId = x.AttendanceId,
                    Date = x.Date,
                    Type = x.Type,
                    ReasonCode = x.ReasonCode,
                    ReasonDescription = x.ReasonDescription,
                    CheckInTimeOld = x.CheckInTimeOld,
                    CheckInTimeNew = x.CheckInTimeNew,
                    CheckOutTimeOld = x.CheckOutTimeOld,
                    CheckOutTimeNew = x.CheckOutTimeNew,
                    Status = x.Status,
                    RequestedBy = x.RequestedBy,
                    ProfileImageUrl = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.ProfileImageUrl)
                                        .FirstOrDefault(),
                    Username = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.FullName)
                                        .FirstOrDefault(),
                    Nip = _context.Users
                                        .Where(u => u.Guid == x.RequestedBy)
                                        .Select(u => u.Nip)
                                        .FirstOrDefault(),
                    ApprovedBy = x.ApprovedBy,
                    ApprovedAt = x.ApprovedAt,
                    CreatedAt = x.CreatedAt,
                    UpdatedAt = x.UpdatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<CorrectionResponseDto> ReviewCorrectionAsync(Guid reviewerUserId, Guid id, UpdateCorrectionDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Status))
                throw new BadRequestException("Status is required");

            var decision = dto.Status.Trim().ToUpperInvariant();
            if (decision is not ("APPROVED" or "REJECTED"))
                throw new BadRequestException("Status must be APPROVE or REJECT");

            var revision = await _context.AttendanceRevision
                .FirstOrDefaultAsync(x => x.Id == id);

            if (revision == null)
                throw new NotFoundException("Correction not found");

            if (string.Equals(revision.Status, "APPROVED", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Correction already approved");

            if (string.Equals(revision.Status, "REJECTED", StringComparison.OrdinalIgnoreCase))
                throw new BadRequestException("Correction already rejected");

            revision.ApprovedBy = reviewerUserId;
            revision.ApprovedAt = DateTime.UtcNow;

            if (decision == "REJECTED")
            {
                revision.Status = "REJECTED";
                await _context.SaveChangesAsync();

                return await GetCorrectionByIdAsync(id)
                    ?? throw new NotFoundException("Correction not found");
            }

            // APPROVE
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a => a.Guid == revision.AttendanceId);

            if (attendance == null)
                throw new NotFoundException("Attendance not found");

            revision.Status = "APPROVED";

            var type = revision.Type?.Trim().ToUpperInvariant();

            if (type is "MISSED_CHECK_IN" or "TECHNICAL_ISSUE_CHECK_IN")
            {
                attendance.CheckInTime = revision.CheckInTimeNew;

                var violations = await _context.AttendanceViolation
                    .Where(v => v.AttendanceId == attendance.Guid &&
                                (v.Type == AttendanceViolationType.NOT_CHECKED_IN || v.Type == AttendanceViolationType.LATE))
                    .ToListAsync();

                if (violations.Count > 0)
                    _context.AttendanceViolation.RemoveRange(violations);
            }
            else if (type is "MISSED_CHECK_OUT" or "TECHNICAL_ISSUE_CHECK_OUT")
            {
                attendance.CheckOutTime = revision.CheckOutTimeNew;

                var violations = await _context.AttendanceViolation
                    .Where(v => v.AttendanceId == attendance.Guid &&
                                (v.Type == AttendanceViolationType.NOT_CHECKED_OUT || v.Type == AttendanceViolationType.EARLY_DEPARTURE))
                    .ToListAsync();

                if (violations.Count > 0)
                    _context.AttendanceViolation.RemoveRange(violations);
            }
            else if (type is "LATE_ARRIVAL")
            {
                attendance.CheckInTime = revision.CheckInTimeNew;

                var violations = await _context.AttendanceViolation
                    .Where(v => v.AttendanceId == attendance.Guid && v.Type == AttendanceViolationType.LATE)
                    .ToListAsync();

                if (violations.Count > 0)
                    _context.AttendanceViolation.RemoveRange(violations);
            }

            // Persist pending deletes before re-checking remaining violations
            await _context.SaveChangesAsync();

            var remainingViolations = await _context.AttendanceViolation
                .AsNoTracking()
                .Where(v => v.AttendanceId == attendance.Guid)
                .AnyAsync();

            if (!remainingViolations)
            {
                // existing enum doesn't have REVISI; use PROBLEM per current model
                attendance.Status = WorkingStatus.REVISION;
            }

            // Recalculate WorkHours if check-in and check-out are complete
            if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
            {
                attendance.WorkHours = Math.Round(
                    (decimal)(attendance.CheckOutTime.Value - attendance.CheckInTime.Value).TotalHours, 2);
            }

            // Recalculate LateMinutes based on configured work start time and the (possibly updated) check-in
            if (attendance.CheckInTime.HasValue)
            {
                var timezoneId = await _settingService.GetAsync(presensi_kpu_batu_be.Common.Constants.GeneralSettingCodes.TIMEZONE);

                TimeZoneInfo tz;
                try
                {
                    tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
                }
                catch
                {
                    tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
                }

                var workingDay = await _timeProviderService.GetTodayWorkingInfoAsync();
                var workStart = TimeOnly.Parse(workingDay.WorkStart!);

                var checkInLocal = TimeZoneInfo.ConvertTimeFromUtc(attendance.CheckInTime.Value, tz);
                var workStartLocal = new DateTime(
                    attendance.Date.Year, attendance.Date.Month, attendance.Date.Day,
                    workStart.Hour, workStart.Minute, 0);

                attendance.LateMinutes = checkInLocal > workStartLocal
                    ? (int)(checkInLocal - workStartLocal).TotalMinutes
                    : 0;
            }

            await _context.SaveChangesAsync();

            return await GetCorrectionByIdAsync(id)
                ?? throw new NotFoundException("Correction not found");
        }
    }
}
