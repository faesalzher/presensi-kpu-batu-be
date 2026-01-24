using Application.Common.Exceptions;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.GoogleDriveModule;
using presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto;

public class LeaveRequestService : ILeaveRequestService
{
    private readonly AppDbContext _context;
    private readonly IGoogleDriveService _googleDrive;
    private readonly IConfiguration _config;

    public LeaveRequestService(AppDbContext context,
        IGoogleDriveService googleDrive,
        IConfiguration config)
    {
        _context = context;
        _googleDrive = googleDrive;
        _config = config;
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
        // Validasi bisnis
        // =====================
        if (dto.StartDate > dto.EndDate)
            throw new BadRequestException(
                "Start date cannot be after end date");

        if (dto.Attachment == null)
            throw new BadRequestException(
                "Attachment is required");

        // =====================
        // Upload ke Google Drive
        // =====================
        string? attachmentId = null;
        string? attachmentUrl = null;

        if (dto.Attachment != null)
        {
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

            var folderId = _config["GoogleDrive:LeaveFolderId"];

            //var (fileId, webViewLink) =
            //    await _googleDrive.UploadAsync(dto.Attachment);

            //attachmentId = fileId;
            //attachmentUrl = webViewLink;
        }

        // =====================
        // Simpan ke DB
        // =====================
        var leave = new LeaveRequest
        {
            Guid = Guid.NewGuid(),
            UserId = userId,
            DepartmentId = dto.DepartmentId,
            Type = dto.Type,
            Status = LeaveRequestStatus.PENDING,
            StartDate = DateOnly.FromDateTime(dto.StartDate),
            EndDate = DateOnly.FromDateTime(dto.EndDate),
            AttachmentId = attachmentId,
            AttachmentUrl = attachmentUrl,
            CreatedAt = DateTime.UtcNow
        };

        _context.LeaveRequest.Add(leave);
        await _context.SaveChangesAsync();

        return leave;
    }

}
