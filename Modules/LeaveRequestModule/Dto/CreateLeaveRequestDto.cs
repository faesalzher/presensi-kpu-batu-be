namespace presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto
{
    public class CreateLeaveRequestDto
    {
        public Guid DepartmentId { get; set; }
        public LeaveRequestType Type { get; set; }

        public DateOnly StartDate { get; set; }
        public DateOnly EndDate { get; set; }

        public string? Reason { get; set; }
        public IFormFile Attachment { get; set; } = default!;
    }


}
