using HealthCoverage.Data;
using HealthCoverage.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthCoverage.Services;

public class PrbReportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public PrbReportService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    // ─── เดือน/ปีที่มีข้อมูล ────────────────────────────────────────────────

    public async Task<List<(int Year, int Month)>> GetAvailableMonthsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.PrbImports
            .OrderByDescending(i => i.Year).ThenByDescending(i => i.Month)
            .Select(i => new { i.Year, i.Month })
            .ToListAsync()
            .ContinueWith(t => t.Result.Select(x => (x.Year, x.Month)).ToList());
    }

    // ─── รายงานรายเดือน ─────────────────────────────────────────────────────

    public async Task<MonthlyReportViewModel?> GetMonthlyReportAsync(int year, int month)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var import = await db.PrbImports
            .FirstOrDefaultAsync(i => i.Year == year && i.Month == month);

        if (import is null) return null;

        var records = await db.PrbRecords
            .Where(r => r.ImportId == import.Id)
            .OrderBy(r => r.ServiceDate).ThenBy(r => r.RowNo)
            .ToListAsync();

        // ── แปลงเป็น display rows ─────────────────────────────────────────
        var displayRows = records.Select(r => new ReportRecordRow
        {
            RowNo         = r.RowNo,
            ServiceDateDisplay = FormatThaiDate(r.ServiceDate),
            PatientName   = r.PatientName ?? "",
            Hn            = r.Hn ?? "",
            Status        = r.Status ?? "",
            Company       = r.Company ?? "",
            HospitalFee   = r.HospitalFee,
            LifeInsurance = r.LifeInsurance,
            TreatmentCost = r.TreatmentCost,
            ColJ          = r.ColJ,
            FundAmount    = r.FundAmount,
            PaymentAmount = r.PaymentAmount,
            Provider      = r.Provider,
            StatusRemark  = r.StatusRemark,
            PoliceStation = r.PoliceStation,
            Remarks       = r.Remarks,
        }).ToList();

        // ── สรุป OPD / IPD ────────────────────────────────────────────────
        var opd = records.Where(r =>
            !string.Equals(r.Status, "IPD", StringComparison.OrdinalIgnoreCase)).ToList();
        var ipd = records.Where(r =>
             string.Equals(r.Status, "IPD", StringComparison.OrdinalIgnoreCase)).ToList();

        var opdSummary = BuildSummary("OPD", opd);
        var ipdSummary = BuildSummary("IPD", ipd);

        var grand = new ReportSummaryRow
        {
            Label         = "รวมทั้งหมด",
            Count         = opdSummary.Count + ipdSummary.Count,
            PrbAmount     = opdSummary.PrbAmount     + ipdSummary.PrbAmount,
            CashAmount    = opdSummary.CashAmount    + ipdSummary.CashAmount,
            PaAmount      = opdSummary.PaAmount      + ipdSummary.PaAmount,
            TotalAmount   = opdSummary.TotalAmount   + ipdSummary.TotalAmount,
            LifeInsurance = opdSummary.LifeInsurance + ipdSummary.LifeInsurance,
            ColJ          = opdSummary.ColJ          + ipdSummary.ColJ,
        };

        // ── แยกตามบริษัท (top 10 + อื่นๆ) ────────────────────────────────
        var byCompany = records
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Company) ? "(ไม่ระบุ)" : r.Company)
            .Select(g => new CompanyBreakdown
            {
                Company     = g.Key,
                Count       = g.Count(),
                TotalAmount = g.Sum(r => r.PaymentAmount),
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(15)
            .ToList();

        // ── Available months ──────────────────────────────────────────────
        var available = await db.PrbImports
            .OrderByDescending(i => i.Year).ThenByDescending(i => i.Month)
            .Select(i => new { i.Year, i.Month })
            .ToListAsync();

        return new MonthlyReportViewModel
        {
            Year         = year,
            Month        = month,
            FileName     = import.FileName ?? "",
            OpdSummary   = opdSummary,
            IpdSummary   = ipdSummary,
            GrandTotal   = grand,
            Records      = displayRows,
            ByCompany    = byCompany,
            AvailableMonths = available.Select(x => (x.Year, x.Month)).ToList(),
        };
    }

    // ─── Dashboard ───────────────────────────────────────────────────────────

    public async Task<DashboardViewModel> GetDashboardAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var imports = await db.PrbImports
            .OrderBy(i => i.Year).ThenBy(i => i.Month)
            .ToListAsync();

        if (imports.Count == 0) return new DashboardViewModel();

        // ── Monthly aggregates (single DB query) ─────────────────────────────
        var monthlyAgg = await db.PrbRecords
            .GroupBy(r => r.ImportId)
            .Select(g => new
            {
                ImportId     = g.Key,
                OpdCount     = g.Count(r => r.Status != "IPD"),
                IpdCount     = g.Count(r => r.Status == "IPD"),
                OpdRevenue   = g.Where(r => r.Status != "IPD").Sum(r => r.PaymentAmount),
                IpdRevenue   = g.Where(r => r.Status == "IPD").Sum(r => r.PaymentAmount),
                PrbAmount    = g.Sum(r => r.HospitalFee),
                CashAmount   = g.Sum(r => r.TreatmentCost),
                PaAmount     = g.Sum(r => r.FundAmount),
                TotalRevenue = g.Sum(r => r.PaymentAmount),
            })
            .ToListAsync();

        // ── Company totals across all months ─────────────────────────────────
        var companyAgg = await db.PrbRecords
            .GroupBy(r => r.Company == null || r.Company == "" ? "(ไม่ระบุ)" : r.Company)
            .Select(g => new CompanyBreakdown
            {
                Company     = g.Key,
                Count       = g.Count(),
                TotalAmount = g.Sum(r => r.PaymentAmount),
            })
            .OrderByDescending(c => c.TotalAmount)
            .Take(8)
            .ToListAsync();

        // Join aggregates with import metadata
        var aggDict = monthlyAgg.ToDictionary(a => a.ImportId);

        var monthlyStats = imports.Select(imp =>
        {
            aggDict.TryGetValue(imp.Id, out var agg);
            return new MonthlyStat
            {
                Year        = imp.Year,
                Month       = imp.Month,
                Label       = $"{ThaiMonthShort(imp.Month)} {(imp.Year + 543) % 100:D2}",
                OpdCount    = agg?.OpdCount    ?? 0,
                IpdCount    = agg?.IpdCount    ?? 0,
                TotalCount  = (agg?.OpdCount ?? 0) + (agg?.IpdCount ?? 0),
                OpdRevenue  = agg?.OpdRevenue  ?? 0,
                IpdRevenue  = agg?.IpdRevenue  ?? 0,
                TotalRevenue = agg?.TotalRevenue ?? 0,
                PrbAmount   = agg?.PrbAmount   ?? 0,
                CashAmount  = agg?.CashAmount  ?? 0,
                PaAmount    = agg?.PaAmount    ?? 0,
            };
        }).ToList();

        var latest = monthlyStats.Last();

        return new DashboardViewModel
        {
            TotalMonthsImported = imports.Count,
            TotalRecords        = monthlyStats.Sum(m => m.TotalCount),
            TotalRevenue        = monthlyStats.Sum(m => m.TotalRevenue),
            TotalPrb            = monthlyStats.Sum(m => m.PrbAmount),
            TotalCash           = monthlyStats.Sum(m => m.CashAmount),
            TotalPa             = monthlyStats.Sum(m => m.PaAmount),
            LatestYear          = latest.Year,
            LatestMonth         = latest.Month,
            LatestOpdCount      = latest.OpdCount,
            LatestIpdCount      = latest.IpdCount,
            LatestRevenue       = latest.TotalRevenue,
            MonthlyStats        = monthlyStats,
            OpdIpdStats         = monthlyStats,
            TopCompanies        = companyAgg,
        };
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static ReportSummaryRow BuildSummary(string label, List<Models.db.PrbRecord> rows)
        => new()
        {
            Label         = label,
            Count         = rows.Count,
            PrbAmount     = rows.Sum(r => r.HospitalFee),
            CashAmount    = rows.Sum(r => r.TreatmentCost),
            PaAmount      = rows.Sum(r => r.FundAmount),
            TotalAmount   = rows.Sum(r => r.PaymentAmount),
            LifeInsurance = rows.Sum(r => r.LifeInsurance),
            ColJ          = rows.Sum(r => r.ColJ),
        };

    /// <summary>แปลง DateTime → "dd/MM/yyyy" แบบปี พ.ศ.</summary>
    public static string FormatThaiDate(DateTime? dt)
    {
        if (dt is null) return "";
        var be = dt.Value.Year + 543;
        return $"{dt.Value.Day:D2}/{dt.Value.Month:D2}/{be}";
    }

    public static string ThaiMonthName(int m) => m switch
    {
        1  => "มกราคม",   2  => "กุมภาพันธ์", 3  => "มีนาคม",
        4  => "เมษายน",   5  => "พฤษภาคม",    6  => "มิถุนายน",
        7  => "กรกฎาคม",  8  => "สิงหาคม",    9  => "กันยายน",
        10 => "ตุลาคม",  11  => "พฤศจิกายน",  12 => "ธันวาคม",
        _  => m.ToString()
    };

    public static string ThaiMonthShort(int m) => m switch
    {
        1  => "ม.ค.", 2  => "ก.พ.", 3  => "มี.ค.",
        4  => "เม.ย.", 5 => "พ.ค.", 6  => "มิ.ย.",
        7  => "ก.ค.", 8  => "ส.ค.", 9  => "ก.ย.",
        10 => "ต.ค.", 11 => "พ.ย.", 12 => "ธ.ค.",
        _  => m.ToString()
    };
}
