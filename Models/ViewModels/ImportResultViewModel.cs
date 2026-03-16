namespace HealthCoverage.Models.ViewModels;

public class ImportResultViewModel
{
    public bool Success { get; set; }

    /// <summary>ข้อความสรุปผล</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>ปี ค.ศ. ที่ import</summary>
    public int Year { get; set; }

    /// <summary>เดือน 1-12 ที่ import</summary>
    public int Month { get; set; }

    public int RecordCount { get; set; }
    public int OpdCount { get; set; }
    public int IpdCount { get; set; }

    public decimal OpdTotal { get; set; }
    public decimal IpdTotal { get; set; }
    public decimal GrandTotal => OpdTotal + IpdTotal;

    /// <summary>true = เดือนนี้ import ไปแล้ว</summary>
    public bool IsDuplicate { get; set; }

    /// <summary>แถวที่ parse ไม่ได้ (เช่น format วันที่ผิด)</summary>
    public List<string> Warnings { get; set; } = new();
}
