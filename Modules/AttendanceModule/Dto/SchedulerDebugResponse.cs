namespace presensi_kpu_batu_be.Modules.AttendanceModule.Dto
{
    public class SchedulerDebugResponse
    {
        public string Scheduler { get; set; } = default!;
        public DateOnly Date { get; set; }
        public DateTime ExecutedAtUtc { get; set; }

        public int AttendanceCreated { get; set; }
        public int AttendanceUpdated { get; set; }

        public int ViolationsAdded { get; set; }
        public int ViolationsRemoved { get; set; }

        public List<Guid> AffectedUserIds { get; set; } = new();
        
        // 🔥 TAMBAH FIELD INI
        public string? DebugMessage { get; set; }
    }

}
