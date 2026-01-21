namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto
{
    public class AttendanceQueryParams
    {
        public string? StartDate { get; set; }   // yyyy-MM-dd
        public string? EndDate { get; set; }
        public Guid? UserId { get; set; }
        public Guid? DepartmentId { get; set; }
        public WorkingStatus? Status { get; set; }
    }

}
