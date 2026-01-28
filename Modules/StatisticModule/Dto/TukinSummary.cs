using System;
using System.Collections.Generic;

namespace presensi_kpu_batu_be.Modules.StatisticModule.Dto
{
    public class TukinViolationDto
    {
        public DateTime Date { get; set; }
        public string Type { get; set; } = default!;
        public string TypeLabel { get; set; } = default!;
        public decimal Percent { get; set; }
        public decimal TukinBaseAmount { get; set; }
        public decimal NominalDeduction { get; set; }
    }

    public class TukinSummary
    {
        public string Month { get; set; } = default!;
        public int? Grade { get; set; }
        public decimal TukinBruto { get; set; }
        public decimal TotalDeduction { get; set; }
        public decimal TukinReceived { get; set; }
        public List<TukinViolationDto> Violations { get; set; } = new();
    }
}
