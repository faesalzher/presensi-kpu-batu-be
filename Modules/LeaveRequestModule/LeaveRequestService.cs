using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.GoogleDriveModule;
using presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto;
using presensi_kpu_batu_be.Modules.FileMoudle.Dto;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _config;
    private readonly ITimeProviderService _timeProviderService;

    public LeaveRequestService(AppDbContext context, ITimeProviderService timeProviderService,
        IConfiguration config)
    {
        _config = config;
        _timeProviderService = timeProviderService; 
        _context = context;
    }

    public async Task<LeaveStatusResult> CheckUserLeaveStatusAsync(Guid userId, DateOnly date)
    {
        var leave = await _context.LeaveRequest
            .AsNoTracking().Where(l => l.UserId == userId &&
                l.Status == LeaveRequestStatus.APPROVED &&
                l.StartDate <= date &&
                l.EndDate >= date)
            .Select(l => new
            {
                l.Type
            })
            .FirstOrDefaultAsync();

        if (leave == null)
        {
            return new LeaveStatusResult
            {
                IsOnLeave = false
            };
        }

        return new LeaveStatusResult
        {
            IsOnLeave = true,
            LeaveType = leave.Type
        };
    }

    public async Task<List<Guid>> GetUserIdsOnLeaveAsync(DateOnly date)
    {
        return await _context.LeaveRequest
            .AsNoTracking()
            .Where(l =>
                l.Status == LeaveRequestStatus.APPROVED &&
                l.StartDate <= date &&
                l.EndDate >= date)
            .Select(l => l.UserId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<LeaveRequest> CreateAsync(
     Guid userId,
     CreateLeaveRequestDto dto)
    {
        // =====================
        // Validasi tanggal
        // =====================
        if (dto.StartDate > dto.EndDate)
            throw new BadRequestException(
                "Start date cannot be after end date");

        // =====================
        // Validasi hari libur
        // =====================
        var start = dto.StartDate;
        var end = dto.EndDate;

        var workingDates = new List<DateOnly>();

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (await _timeProviderService.IsWorkingDayAsync(date))
                workingDates.Add(date);
        }

        if (!workingDates.Any())
            throw new BadRequestException(
                $"Tanggal {start:yyyy-MM-dd} s.d {end:yyyy-MM-dd} tidak memiliki hari kerja");


        // =====================
        // Validasi absen sudah lengkap
        // =====================
        var completedAttendances = await _context.Attendance
            .Where(x =>
                x.UserId == userId &&
                x.Status == WorkingStatus.PRESENT &&
                workingDates.Contains(x.Date)
            )
            .Select(x => x.Date)
            .ToListAsync();

        if (completedAttendances.Any())
            throw new BadRequestException(
                "Pengajuan izin tidak boleh mencakup hari yang sudah presensi lengkap. " +
                "Silakan ajukan izin hanya untuk tanggal yang bermasalah.");

        // =====================
        // Validasi attachment
        // =====================
        if (dto.Attachment == null)
            throw new BadRequestException("Attachment is required");

        if (dto.Attachment.Length > 2_000_000)
            throw new BadRequestException("Max file size is 2MB");

        var allowedTypes = new[]
        {
        "application/pdf",
        "image/jpeg",
        "image/jpg",
        "image/png"
    };

        if (!allowedTypes.Contains(dto.Attachment.ContentType))
            throw new BadRequestException("Invalid file type");

        // =====================
        // Buat LeaveRequest
        // =====================
        var leave = new LeaveRequest
        {
            Guid = Guid.NewGuid(),
            UserId = userId,
            DepartmentId = dto.DepartmentId,
            Type = dto.Type,
            Status = LeaveRequestStatus.APPROVED,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            Reason = dto.Reason,
            CreatedAt = DateTime.UtcNow,
            UsrCrt = userId.ToString()
        };

        _context.LeaveRequest.Add(leave);

        // =====================
        // Upload file
        // =====================
        var (storedFileName, filePath) =
            await SaveLeaveAttachmentAsync(dto.Attachment);

        // =====================
        // Simpan file_metadata
        // =====================
        var fileMetadata = new FileMetadata
        {
            Guid = Guid.NewGuid(),
            FileName = storedFileName,
            OriginalName = dto.Attachment.FileName,
            MimeType = dto.Attachment.ContentType,
            Size = dto.Attachment.Length,
            Path = filePath,
            Category = FileCategory.PERMISSION,
            UserId = userId,
            RelatedId = leave.Guid,
            IsTemporary = false,
            CreatedAt = DateTime.UtcNow,
            UsrCrt = userId.ToString()
        };

        _context.FileMetadata.Add(fileMetadata);

        // =====================
        // Attendance adjustments for the leave period
        // - create attendance for future dates (or today)
        // - update existing attendances for past dates: set status according to leave type,
        //   clear times and set work hours to 0, remove violations
        // Note: map LeaveRequestType -> WorkingStatus based on available enum values.
        // =====================
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        static WorkingStatus MapLeaveTypeToWorkingStatus(LeaveRequestType t) => t switch
        {
            LeaveRequestType.SICK => WorkingStatus.SICK,          // no explicit SICK in WorkingStatus -> use ON_LEAVE
            LeaveRequestType.LEAVE => WorkingStatus.ON_LEAVE,
            LeaveRequestType.DL => WorkingStatus.OFFICIAL_TRAVEL,
            _ => WorkingStatus.ON_LEAVE
        };

        var mappedStatus = MapLeaveTypeToWorkingStatus(dto.Type);

        foreach (var date in workingDates)
        {
            // single attendance per date per user
            var attendance = await _context.Attendance
                .FirstOrDefaultAsync(a => a.UserId == userId && a.Date == date);

            if (attendance == null)
            {
                // create attendance for the leave date (future, today or past)
                attendance = new Attendance
                {
                    Guid = Guid.NewGuid(),
                    UserId = userId,
                    DepartmentId = dto.DepartmentId,
                    Date = date,
                    Status = mappedStatus,
                    CheckInTime = null,
                    CheckOutTime = null,
                    WorkHours = 0m,
                    LateMinutes = null
                };

                _context.Attendance.Add(attendance);

                // remove any violations referencing this attendance (unlikely since new guid),
                // but keep defensive: try to remove violations for same user/date (if any)
                var orphanViolations = await _context.AttendanceViolation
                    .Where(v => v.AttendanceId == attendance.Guid)
                    .ToListAsync();

                if (orphanViolations.Any())
                    _context.AttendanceViolation.RemoveRange(orphanViolations);
            }
            else
            {
                // existing attendance: remove violations and update to leave status + clear times/works
                var existingViolations = await _context.AttendanceViolation
                    .Where(v => v.AttendanceId == attendance.Guid)
                    .ToListAsync();

                if (existingViolations.Any())
                    _context.AttendanceViolation.RemoveRange(existingViolations);

                attendance.Status = mappedStatus;
                //attendance.CheckInTime = null;
                //attendance.CheckOutTime = null;
                attendance.WorkHours = 0m;
                attendance.LateMinutes = null;
                attendance.CheckInNotes = null;
                attendance.CheckOutNotes = null;
                attendance.CheckInPhotoId = null;
                attendance.CheckOutPhotoId = null;
            }
        }

        await _context.SaveChangesAsync();

        return leave;
    }



    private async Task<(string fileName, string fileUrl)> SaveLeaveAttachmentAsync(
    IFormFile file)
    {
        var rootPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "uploads",
            "leave",
            DateTime.UtcNow.Year.ToString(),
            DateTime.UtcNow.Month.ToString("D2")
        );

        if (!Directory.Exists(rootPath))
            Directory.CreateDirectory(rootPath);

        var extension = Path.GetExtension(file.FileName);
        var safeFileName = $"leave_{Guid.NewGuid()}{extension}";
        var fullPath = Path.Combine(rootPath, safeFileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        // URL publik (sesuaikan domain kamu)
        var fileUrl = $"{_config["App:BaseUrl"]}/uploads/leave/{DateTime.UtcNow.Year}/{DateTime.UtcNow.Month:D2}/{safeFileName}";

        return (safeFileName, fileUrl);
    }



    public async Task<List<LeaveRequestResponseDto>> GetMyLeaveRequests(Guid userId)
    {
            var data = await _context.LeaveRequest
                .Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            return data.Select(x => new LeaveRequestResponseDto
            {
                Guid = x.Guid,
                UserId = x.UserId,
                DepartmentId = x.DepartmentId,
                Type = x.Type.ToString(),
                Status = x.Status.ToString(),

                StartDate = x.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = x.EndDate.ToDateTime(TimeOnly.MinValue),

                Reason = x.Reason,

                // populate attachment from file_metadata (latest non-temporary PERMISSION)
                Attachment = _context.FileMetadata
                    .Where(f => f.RelatedId == x.Guid && f.Category == FileCategory.PERMISSION && !f.IsTemporary)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new FileMetadataDto
                    {
                        Guid = f.Guid,
                        FileName = f.FileName,
                        OriginalName = f.OriginalName,
                        MimeType = f.MimeType,
                        Size = f.Size,
                        Path = f.Path,
                        Category = f.Category
                    })
                    .FirstOrDefault()
            }).ToList();
        }

        // New: get single leave request by guid (scoped to requesting user)
        public async Task<LeaveRequestResponseDto?> GetByGuidAsync(Guid guid, Guid userId)
        {
            var result = await _context.LeaveRequest
                .Where(l => l.Guid == guid && l.UserId == userId)
                .Select(l => new LeaveRequestResponseDto
            {
                Guid = l.Guid,
                UserId = l.UserId,
                DepartmentId = l.DepartmentId,
                Type = l.Type.ToString(),
                Status = l.Status.ToString(),

                StartDate = l.StartDate.ToDateTime(TimeOnly.MinValue),
                EndDate = l.EndDate.ToDateTime(TimeOnly.MinValue),

                Reason = l.Reason,

                Attachment = _context.FileMetadata
                    .Where(f => f.RelatedId == l.Guid && f.Category == FileCategory.PERMISSION && !f.IsTemporary)
                    .OrderByDescending(f => f.CreatedAt)
                    .Select(f => new FileMetadataDto
                    {
                        Guid = f.Guid,
                        FileName = f.FileName,
                        OriginalName = f.OriginalName,
                        MimeType = f.MimeType,
                        Size = f.Size,
                        Path = f.Path,
                        Category = f.Category
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync();

        return result;
    }
}
