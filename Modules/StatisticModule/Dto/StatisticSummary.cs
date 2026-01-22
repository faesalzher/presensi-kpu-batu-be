using presensi_kpu_batu_be.Modules.AttendanceModule;

namespace presensi_kpu_batu_be.Modules.StatisticModule.Dto
{
    public class StatisticSummary
    {
        public int TotalDays { get; set; }

        public int Present { get; set; }
        public int Absent { get; set; }
        public int Problem { get; set; }          // late + early + incomplete
        public int RemoteWorking { get; set; }
        public int OnLeave { get; set; }
        public int OfficialTravel { get; set; }

        public double TotalWorkHours { get; set; }
        public double AverageWorkHours { get; set; }

        public int TotalAttendances { get; set; }

        public List<AttendanceResponse> Records { get; set; } = new();
    }

}
