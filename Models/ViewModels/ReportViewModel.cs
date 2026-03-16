namespace HealthCoverage.Models.ViewModels;

// ── ตัวสรุปยอดของแต่ละกลุ่ม (OPD / IPD / รวม) ──────────────────────────────
public class ReportSummaryRow
{
    public string Label { get; set; } = "";
    public int Count { get; set; }

    /// <summary>ยอดใช้สิทธิ พรบ. (คอลัมน์ G)</summary>
    public decimal PrbAmount { get; set; }

    /// <summary>ชำระสด (คอลัมน์ I)</summary>
    public decimal CashAmount { get; set; }

    /// <summary>ยอดใช้ PA / กองทุน (คอลัมน์ K)</summary>
    public decimal PaAmount { get; set; }

    /// <summary>จ่ายรวม (คอลัมน์ L)</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>ประกันชีวิต (คอลัมน์ H)</summary>
    public decimal LifeInsurance { get; set; }

    /// <summary>คอลัมน์ J</summary>
    public decimal ColJ { get; set; }
}

// ── รายละเอียดต่อแถว Excel (ใช้ใน detail table) ─────────────────────────────
public class ReportRecordRow
{
    public int RowNo { get; set; }
    public string ServiceDateDisplay { get; set; } = "";
    public string PatientName { get; set; } = "";
    public string Hn { get; set; } = "";
    public string Status { get; set; } = "";
    public string Company { get; set; } = "";
    public decimal HospitalFee { get; set; }
    public decimal LifeInsurance { get; set; }
    public decimal TreatmentCost { get; set; }
    public decimal ColJ { get; set; }
    public decimal FundAmount { get; set; }
    public decimal PaymentAmount { get; set; }
    public string? Provider { get; set; }
    public string? StatusRemark { get; set; }
    public string? PoliceStation { get; set; }
    public string? Remarks { get; set; }
}

// ── ยอดแยกตามบริษัท ─────────────────────────────────────────────────────────
public class CompanyBreakdown
{
    public string Company { get; set; } = "";
    public int Count { get; set; }
    public decimal TotalAmount { get; set; }
}

// ── ViewModel หลักที่ส่งให้ Razor page ──────────────────────────────────────
public class MonthlyReportViewModel
{
    public int Year { get; set; }
    public int Month { get; set; }
    public string FileName { get; set; } = "";

    public ReportSummaryRow OpdSummary { get; set; } = new() { Label = "OPD" };
    public ReportSummaryRow IpdSummary { get; set; } = new() { Label = "IPD" };
    public ReportSummaryRow GrandTotal { get; set; } = new() { Label = "รวม" };

    public List<ReportRecordRow> Records { get; set; } = new();
    public List<CompanyBreakdown> ByCompany { get; set; } = new();

    // เดือน/ปีที่มีข้อมูล (สำหรับ dropdown)
    public List<(int Year, int Month)> AvailableMonths { get; set; } = new();
}
