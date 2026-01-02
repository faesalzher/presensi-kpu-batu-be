namespace presensi_kpu_batu_be.Modules.User.Dto
{
    public class UserResponse
    {
        public Guid Guid { get; set; }

        public string FullName { get; set; } = default!;

        public string Email { get; set; } = default!;

        public string? Nip { get; set; }

        public string? PhoneNumber { get; set; }

        public string? ProfileImageUrl { get; set; }

        public string? Role { get; set; }

        // 🔥 ini yang FE butuhkan
        public Guid? DepartmentId { get; set; }
        public string? Department { get; set; }

        public string? Position { get; set; }

        public bool IsActive { get; set; }
    }
}
