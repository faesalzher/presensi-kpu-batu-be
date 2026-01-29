namespace presensi_kpu_batu_be.Modules.AttendanceModule
{
    public class AttendanceResponse
    {
        public Guid Guid { get; set; }

        // User
        public Guid UserId { get; set; }
        public string? UserName { get; set; }   // optional (kalau join user)

        // Department
        public Guid? DepartmentId { get; set; }
        public string? DepartmentName { get; set; }

        // Date
        public DateOnly Date { get; set; }
        //public Boolean hasCheckedIn { get; set; }
        public Boolean isForgotCheckIn { get; set; }
        public Boolean isForgotCheckOut { get; set; }

        // Check In
        public DateTime? CheckInTime { get; set; }
        public string? CheckInLocation { get; set; }
        public Guid? CheckInPhotoId { get; set; }
        public string? CheckInNotes { get; set; }

        // Check Out
        public DateTime? CheckOutTime { get; set; }
        public string? CheckOutLocation { get; set; }
        public Guid? CheckOutPhotoId { get; set; }
        public string? CheckOutNotes { get; set; }

        // Work
        public decimal? WorkHours { get; set; }
        public string? Status { get; set; } = default!;
        public string? ViolationNotes { get; set; }

        public int? LateMinutes { get; set; }
    }
}
