using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Modules.RevisionModule.Dto;

namespace presensi_kpu_batu_be.Modules.RevisionModule
{
    public interface IRevisionService
    {
        Task<AttendanceRevision> CreateCorrectionAsync(Guid requestedBy, CreateAttendanceCorrectionDto dto);
        Task<List<CorrectionResponseDto>> GetPendingCorrectionsAsync(Guid? departmentId);
        Task<List<CorrectionResponseDto>> GetMyCorrectionsByAttendanceIdAsync(Guid requestedBy, Guid attendanceId);
        Task<List<CorrectionResponseDto>> GetMyCorrectionsAsync(Guid requestedBy);
        Task<CorrectionResponseDto?> GetMyCorrectionByIdAsync(Guid requestedBy, Guid id);
        Task<CorrectionResponseDto?> GetCorrectionByIdAsync(Guid id);
        Task<CorrectionResponseDto> ReviewCorrectionAsync(Guid reviewerUserId, Guid id, UpdateCorrectionDto dto);
        Task<List<CorrectionResponseDto>> QueryCorrectionsAsync(QueryCorrectionsDto? query);
    }
}
