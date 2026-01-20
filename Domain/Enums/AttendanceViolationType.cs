namespace presensi_kpu_batu_be.Domain.Enums
{
    public enum AttendanceViolationType
    {
        LATE,               // 2.5%
        NOT_CHECKED_IN,     // 2.5%
        NOT_CHECKED_OUT,    // 2.5%
        ABSENT,         // 🔥 5%,
        EARLY_DEPARTURE
    }


    public enum ViolationSource
    {
        CHECK_IN,
        CHECK_OUT,
        SYSTEM
    }

}
