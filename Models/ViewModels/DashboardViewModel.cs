namespace HealthCoverage.Models.ViewModels;

public class DashboardViewModel
{
    // ── KPI ──────────────────────────────────────────────────────────────────
    public int    TotalMonthsImported { get; set; }
    public int    TotalRecords        { get; set; }
    public decimal TotalRevenue       { get; set; }
    public decimal TotalPrb           { get; set; }
    public decimal TotalCash          { get; set; }
    public decimal TotalPa            { get; set; }

    // ── Latest month summary ─────────────────────────────────────────────────
    public int    LatestYear          { get; set; }
    public int    LatestMonth         { get; set; }
    public int    LatestOpdCount      { get; set; }
    public int    LatestIpdCount      { get; set; }
    public decimal LatestRevenue      { get; set; }

    // ── Monthly series (for bar/line chart) — sorted oldest → newest ─────────
    public List<MonthlyStat> MonthlyStats { get; set; } = new();

    // ── Company pie (top N) ──────────────────────────────────────────────────
    public List<CompanyBreakdown> TopCompanies { get; set; } = new();

    // ── OPD vs IPD monthly (stacked bar) ────────────────────────────────────
    public List<MonthlyStat> OpdIpdStats { get; set; } = new();
}

public class MonthlyStat
{
    public int     Year          { get; set; }
    public int     Month         { get; set; }
    public string  Label         { get; set; } = "";  // e.g. "ม.ค. 68"
    public int     OpdCount      { get; set; }
    public int     IpdCount      { get; set; }
    public int     TotalCount    { get; set; }
    public decimal OpdRevenue    { get; set; }
    public decimal IpdRevenue    { get; set; }
    public decimal TotalRevenue  { get; set; }
    public decimal PrbAmount     { get; set; }
    public decimal CashAmount    { get; set; }
    public decimal PaAmount      { get; set; }
}
