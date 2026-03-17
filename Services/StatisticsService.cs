using HealthCoverage.Data;
using HealthCoverage.Models.db;
using HealthCoverage.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthCoverage.Services;

public class StatisticsService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;

    public StatisticsService(IDbContextFactory<ApplicationDbContext> dbFactory)
        => _dbFactory = dbFactory;

    // ─── Public API ──────────────────────────────────────────────────────────

    public async Task<List<int>> GetAvailableYearsAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.PrbImports
            .Select(i => i.Year)
            .Distinct()
            .OrderBy(y => y)
            .ToListAsync();
    }

    /// <summary>
    /// Case count table: monthly rows for selectedYear, annual summary rows for other years.
    /// </summary>
    public async Task<(List<CaseCountRow> Rows, List<int> Years)> GetCaseCountAsync(int selectedYear)
    {
        var data = await LoadDataAsync();
        var years = data.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
        var rows = new List<CaseCountRow>();

        foreach (var year in years)
        {
            var yearData = data.Where(x => x.Year == year).ToList();

            if (year == selectedYear)
            {
                // header row for the year
                rows.Add(new CaseCountRow
                {
                    Label    = ThaiYearLabel(year),
                    IsHeader = true,
                });

                // 12 monthly rows
                for (int m = 1; m <= 12; m++)
                {
                    var monthData = yearData.Where(x => x.Month == m).ToList();
                    if (monthData.Count == 0)
                    {
                        rows.Add(new CaseCountRow
                        {
                            Label   = ThaiMonthShort(m),
                            IsEmpty = true,
                        });
                    }
                    else
                    {
                        rows.Add(BuildCaseCountRow(ThaiMonthShort(m), monthData));
                    }
                }
            }
            else
            {
                // annual summary row
                var annualRow = BuildCaseCountRow(ThaiYearLabel(year), yearData);
                annualRow.IsHeader = true;
                rows.Add(annualRow);

                // เฉลี่ย/เดือน subrow
                int months = yearData.Select(x => x.Month).Distinct().Count();
                if (months > 0)
                {
                    rows.Add(new CaseCountRow
                    {
                        Label    = "เฉลี่ย/เดือน",
                        IsSubRow = true,
                        PrbOpd   = annualRow.PrbOpd   / months,
                        PrbIpd   = annualRow.PrbIpd   / months,
                        CashOpd  = annualRow.CashOpd  / months,
                        CashIpd  = annualRow.CashIpd  / months,
                        PaOpd    = annualRow.PaOpd    / months,
                        PaIpd    = annualRow.PaIpd    / months,
                        TotalOpd = annualRow.TotalOpd / months,
                        TotalIpd = annualRow.TotalIpd / months,
                    });
                }
            }
        }

        // Grand total row
        var totalRow = BuildCaseCountRow("รวม", data);
        totalRow.IsTotal = true;
        rows.Add(totalRow);

        // Grand average row (across all months)
        int totalMonths = data.Select(x => (x.Year, x.Month)).Distinct().Count();
        if (totalMonths > 0)
        {
            rows.Add(new CaseCountRow
            {
                Label    = "เฉลี่ย/เดือน",
                IsAvg    = true,
                PrbOpd   = totalRow.PrbOpd   / totalMonths,
                PrbIpd   = totalRow.PrbIpd   / totalMonths,
                CashOpd  = totalRow.CashOpd  / totalMonths,
                CashIpd  = totalRow.CashIpd  / totalMonths,
                PaOpd    = totalRow.PaOpd    / totalMonths,
                PaIpd    = totalRow.PaIpd    / totalMonths,
                TotalOpd = totalRow.TotalOpd / totalMonths,
                TotalIpd = totalRow.TotalIpd / totalMonths,
            });
        }

        return (rows, years);
    }

    /// <summary>
    /// Revenue table: monthly rows for selectedYear only (12 rows + total + avg/case).
    /// </summary>
    public async Task<(List<RevenueRow> Rows, List<int> Years)> GetRevenueMonthlyAsync(int selectedYear)
    {
        var data = await LoadDataAsync();
        var years = data.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
        var yearData = data.Where(x => x.Year == selectedYear).ToList();
        var rows = new List<RevenueRow>();

        // 12 monthly rows
        for (int m = 1; m <= 12; m++)
        {
            var monthData = yearData.Where(x => x.Month == m).ToList();
            if (monthData.Count == 0)
            {
                rows.Add(new RevenueRow { Label = ThaiMonthShort(m), IsEmpty = true });
            }
            else
            {
                rows.Add(BuildRevenueRow(ThaiMonthShort(m), monthData));
            }
        }

        // Total row
        var totalRow = BuildRevenueRow("รวม", yearData);
        totalRow.IsTotal = true;
        rows.Add(totalRow);

        // Avg per case row
        int totalCases = yearData.Count;
        if (totalCases > 0)
        {
            rows.Add(new RevenueRow
            {
                Label    = "เฉลี่ย/ราย",
                IsAvg    = true,
                PrbOpd   = SafeDiv(totalRow.PrbOpd,  CountOpdPrb(yearData)),
                PrbIpd   = SafeDiv(totalRow.PrbIpd,  CountIpdPrb(yearData)),
                CashOpd  = SafeDiv(totalRow.CashOpd, CountOpdCash(yearData)),
                CashIpd  = SafeDiv(totalRow.CashIpd, CountIpdCash(yearData)),
                PaOpd    = SafeDiv(totalRow.PaOpd,   CountOpdPa(yearData)),
                PaIpd    = SafeDiv(totalRow.PaIpd,   CountIpdPa(yearData)),
                TotalOpd = SafeDiv(totalRow.TotalOpd, yearData.Count(x => !IsIpd(x.Rec))),
                TotalIpd = SafeDiv(totalRow.TotalIpd, yearData.Count(x => IsIpd(x.Rec))),
            });
        }

        return (rows, years);
    }

    /// <summary>
    /// Average revenue per case, one row per year.
    /// </summary>
    public async Task<List<AvgRevenueRow>> GetAvgRevenuePerCaseAsync()
    {
        var data = await LoadDataAsync();
        var years = data.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
        var result = new List<AvgRevenueRow>();

        foreach (var year in years)
        {
            var yd = data.Where(x => x.Year == year).ToList();

            var opdPrb  = yd.Where(x => !IsIpd(x.Rec) && x.Rec.HospitalFee  > 0).ToList();
            var ipdPrb  = yd.Where(x =>  IsIpd(x.Rec) && x.Rec.HospitalFee  > 0).ToList();
            var opdCash = yd.Where(x => !IsIpd(x.Rec) && x.Rec.TreatmentCost > 0).ToList();
            var ipdCash = yd.Where(x =>  IsIpd(x.Rec) && x.Rec.TreatmentCost > 0).ToList();
            var opdPa   = yd.Where(x => !IsIpd(x.Rec) && x.Rec.FundAmount     > 0).ToList();
            var ipdPa   = yd.Where(x =>  IsIpd(x.Rec) && x.Rec.FundAmount     > 0).ToList();
            var opdAll  = yd.Where(x => !IsIpd(x.Rec)).ToList();
            var ipdAll  = yd.Where(x =>  IsIpd(x.Rec)).ToList();

            result.Add(new AvgRevenueRow
            {
                Year       = year,
                Label      = ThaiYearLabel(year),
                PrbAvgOpd  = SafeDiv(opdPrb.Sum(x  => x.Rec.HospitalFee),   opdPrb.Count),
                PrbAvgIpd  = SafeDiv(ipdPrb.Sum(x  => x.Rec.HospitalFee),   ipdPrb.Count),
                CashAvgOpd = SafeDiv(opdCash.Sum(x => x.Rec.TreatmentCost),  opdCash.Count),
                CashAvgIpd = SafeDiv(ipdCash.Sum(x => x.Rec.TreatmentCost),  ipdCash.Count),
                PaAvgOpd   = SafeDiv(opdPa.Sum(x   => x.Rec.FundAmount),     opdPa.Count),
                PaAvgIpd   = SafeDiv(ipdPa.Sum(x   => x.Rec.FundAmount),     ipdPa.Count),
                AvgOpd     = SafeDiv(opdAll.Sum(x  => x.Rec.PaymentAmount),   opdAll.Count),
                AvgIpd     = SafeDiv(ipdAll.Sum(x  => x.Rec.PaymentAmount),   ipdAll.Count),
            });
        }

        return result;
    }

    /// <summary>
    /// Total revenue per year with sub-rows for monthly average.
    /// </summary>
    public async Task<List<YearlyRevenueRow>> GetTotalRevenueByYearAsync()
    {
        var data = await LoadDataAsync();
        var years = data.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
        var rows = new List<YearlyRevenueRow>();

        decimal sumPrbOpd = 0, sumPrbIpd = 0;
        decimal sumCashOpd = 0, sumCashIpd = 0;
        decimal sumPaOpd = 0, sumPaIpd = 0;
        decimal sumTotalOpd = 0, sumTotalIpd = 0;

        foreach (var year in years)
        {
            var yd = data.Where(x => x.Year == year).ToList();
            int months = yd.Select(x => x.Month).Distinct().Count();

            var annualRow = BuildYearlyRevenueRow(ThaiYearLabel(year), yd);
            rows.Add(annualRow);

            sumPrbOpd   += annualRow.PrbOpd;
            sumPrbIpd   += annualRow.PrbIpd;
            sumCashOpd  += annualRow.CashOpd;
            sumCashIpd  += annualRow.CashIpd;
            sumPaOpd    += annualRow.PaOpd;
            sumPaIpd    += annualRow.PaIpd;
            sumTotalOpd += annualRow.TotalOpd;
            sumTotalIpd += annualRow.TotalIpd;

            if (months > 0)
            {
                rows.Add(new YearlyRevenueRow
                {
                    Label    = "เฉลี่ย/เดือน",
                    IsSubRow = true,
                    PrbOpd   = annualRow.PrbOpd   / months,
                    PrbIpd   = annualRow.PrbIpd   / months,
                    CashOpd  = annualRow.CashOpd  / months,
                    CashIpd  = annualRow.CashIpd  / months,
                    PaOpd    = annualRow.PaOpd    / months,
                    PaIpd    = annualRow.PaIpd    / months,
                    TotalOpd = annualRow.TotalOpd / months,
                    TotalIpd = annualRow.TotalIpd / months,
                });
            }
        }

        // Grand total
        rows.Add(new YearlyRevenueRow
        {
            Label    = "รวม",
            IsTotal  = true,
            PrbOpd   = sumPrbOpd,
            PrbIpd   = sumPrbIpd,
            CashOpd  = sumCashOpd,
            CashIpd  = sumCashIpd,
            PaOpd    = sumPaOpd,
            PaIpd    = sumPaIpd,
            TotalOpd = sumTotalOpd,
            TotalIpd = sumTotalIpd,
        });

        // Grand average per month
        int totalMonths = data.Select(x => (x.Year, x.Month)).Distinct().Count();
        if (totalMonths > 0)
        {
            rows.Add(new YearlyRevenueRow
            {
                Label    = "เฉลี่ย/เดือน",
                IsAvg    = true,
                PrbOpd   = sumPrbOpd   / totalMonths,
                PrbIpd   = sumPrbIpd   / totalMonths,
                CashOpd  = sumCashOpd  / totalMonths,
                CashIpd  = sumCashIpd  / totalMonths,
                PaOpd    = sumPaOpd    / totalMonths,
                PaIpd    = sumPaIpd    / totalMonths,
                TotalOpd = sumTotalOpd / totalMonths,
                TotalIpd = sumTotalIpd / totalMonths,
            });
        }

        return rows;
    }

    /// <summary>
    /// Referrer monthly breakdown: monthly rows for selectedYear, annual for others.
    /// </summary>
    public async Task<(List<ReferrerRow> Rows, List<int> Years)> GetReferrerMonthlyAsync(int selectedYear)
    {
        var data = await LoadDataAsync();
        var years = data.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
        var rows = new List<ReferrerRow>();

        foreach (var year in years)
        {
            var yearData = data.Where(x => x.Year == year).ToList();

            if (year == selectedYear)
            {
                rows.Add(new ReferrerRow
                {
                    Label    = ThaiYearLabel(year),
                    IsHeader = true,
                });

                for (int m = 1; m <= 12; m++)
                {
                    var monthData = yearData.Where(x => x.Month == m).ToList();
                    if (monthData.Count == 0)
                    {
                        rows.Add(new ReferrerRow { Label = ThaiMonthShort(m), IsEmpty = true });
                    }
                    else
                    {
                        rows.Add(BuildReferrerRow(ThaiMonthShort(m), monthData));
                    }
                }
            }
            else
            {
                var annualRow = BuildReferrerRow(ThaiYearLabel(year), yearData);
                annualRow.IsHeader = true;
                rows.Add(annualRow);

                int months = yearData.Select(x => x.Month).Distinct().Count();
                if (months > 0)
                {
                    rows.Add(new ReferrerRow
                    {
                        Label      = "เฉลี่ย/เดือน",
                        IsSubRow   = true,
                        RuamOpd    = annualRow.RuamOpd    / months,
                        RuamIpd    = annualRow.RuamIpd    / months,
                        PohOpd     = annualRow.PohOpd     / months,
                        PohIpd     = annualRow.PohIpd     / months,
                        VolOpd     = annualRow.VolOpd     / months,
                        VolIpd     = annualRow.VolIpd     / months,
                        RefOpd     = annualRow.RefOpd     / months,
                        RefIpd     = annualRow.RefIpd     / months,
                        OthersOpd  = annualRow.OthersOpd  / months,
                        OthersIpd  = annualRow.OthersIpd  / months,
                        PoliceOpd  = annualRow.PoliceOpd  / months,
                        PoliceIpd  = annualRow.PoliceIpd  / months,
                        TotalOpd   = annualRow.TotalOpd   / months,
                        TotalIpd   = annualRow.TotalIpd   / months,
                    });
                }
            }
        }

        // Grand total row
        var totalRow = BuildReferrerRow("รวม", data);
        totalRow.IsTotal = true;
        rows.Add(totalRow);

        // Grand average per month
        int totalMonths = data.Select(x => (x.Year, x.Month)).Distinct().Count();
        if (totalMonths > 0)
        {
            rows.Add(new ReferrerRow
            {
                Label      = "เฉลี่ย/เดือน",
                IsAvg      = true,
                RuamOpd    = totalRow.RuamOpd    / totalMonths,
                RuamIpd    = totalRow.RuamIpd    / totalMonths,
                PohOpd     = totalRow.PohOpd     / totalMonths,
                PohIpd     = totalRow.PohIpd     / totalMonths,
                VolOpd     = totalRow.VolOpd     / totalMonths,
                VolIpd     = totalRow.VolIpd     / totalMonths,
                RefOpd     = totalRow.RefOpd     / totalMonths,
                RefIpd     = totalRow.RefIpd     / totalMonths,
                OthersOpd  = totalRow.OthersOpd  / totalMonths,
                OthersIpd  = totalRow.OthersIpd  / totalMonths,
                PoliceOpd  = totalRow.PoliceOpd  / totalMonths,
                PoliceIpd  = totalRow.PoliceIpd  / totalMonths,
                TotalOpd   = totalRow.TotalOpd   / totalMonths,
                TotalIpd   = totalRow.TotalIpd   / totalMonths,
            });
        }

        return (rows, years);
    }

    /// <summary>
    /// Referrer yearly breakdown: one block per year with OPD/IPD/Total/Avg rows.
    /// </summary>
    public async Task<List<ReferrerYearBlock>> GetReferrerYearlyAsync()
    {
        var data = await LoadDataAsync();
        var years = data.Select(x => x.Year).Distinct().OrderBy(y => y).ToList();
        var result = new List<ReferrerYearBlock>();

        foreach (var year in years)
        {
            var yd = data.Where(x => x.Year == year).ToList();
            int months = yd.Select(x => x.Month).Distinct().Count();

            var opdData  = yd.Where(x => !IsIpd(x.Rec)).ToList();
            var ipdData  = yd.Where(x =>  IsIpd(x.Rec)).ToList();

            var opdRow   = BuildReferrerRowSingleStatus("OPD",   opdData);
            var ipdRow   = BuildReferrerRowSingleStatus("IPD",   ipdData);
            var totalRow = BuildReferrerRow("รวม", yd);
            totalRow.IsTotal = true;

            var avgRow = months > 0
                ? new ReferrerRow
                {
                    Label      = "เฉลี่ย/เดือน",
                    IsAvg      = true,
                    RuamOpd    = totalRow.RuamOpd    / months,
                    RuamIpd    = totalRow.RuamIpd    / months,
                    PohOpd     = totalRow.PohOpd     / months,
                    PohIpd     = totalRow.PohIpd     / months,
                    VolOpd     = totalRow.VolOpd     / months,
                    VolIpd     = totalRow.VolIpd     / months,
                    RefOpd     = totalRow.RefOpd     / months,
                    RefIpd     = totalRow.RefIpd     / months,
                    OthersOpd  = totalRow.OthersOpd  / months,
                    OthersIpd  = totalRow.OthersIpd  / months,
                    PoliceOpd  = totalRow.PoliceOpd  / months,
                    PoliceIpd  = totalRow.PoliceIpd  / months,
                    TotalOpd   = totalRow.TotalOpd   / months,
                    TotalIpd   = totalRow.TotalIpd   / months,
                }
                : new ReferrerRow { Label = "เฉลี่ย/เดือน", IsAvg = true };

            result.Add(new ReferrerYearBlock
            {
                Year      = year,
                YearLabel = ThaiYearLabel(year),
                Opd       = opdRow,
                Ipd       = ipdRow,
                Total     = totalRow,
                Avg       = avgRow,
            });
        }

        return result;
    }

    // ─── Internal data loader ────────────────────────────────────────────────

    private async Task<List<(PrbRecord Rec, int Year, int Month)>> LoadDataAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var imports = await db.PrbImports
            .OrderBy(i => i.Year).ThenBy(i => i.Month)
            .ToListAsync();

        var records = await db.PrbRecords.ToListAsync();

        var importDict = imports.ToDictionary(i => i.Id);

        return records
            .Where(r => importDict.ContainsKey(r.ImportId))
            .Select(r =>
            {
                var imp = importDict[r.ImportId];
                return (Rec: r, imp.Year, imp.Month);
            })
            .ToList();
    }

    // ─── Classification helpers ──────────────────────────────────────────────

    private static bool IsIpd(PrbRecord r)
        => r.Status?.Equals("IPD", StringComparison.OrdinalIgnoreCase) == true;

    private static string GetPayerType(PrbRecord r)
    {
        if (r.HospitalFee > 0)  return "prb";
        if (r.TreatmentCost > 0) return "cash";
        if (r.FundAmount > 0)   return "pa";
        return "other";
    }

    private static string GetReferrer(PrbRecord r)
    {
        var p = r.Provider ?? "";
        if (p.Contains("ร่วมก") || p.Contains("กตัญ"))  return "ruam";
        if (p.Contains("ปอเล็ก") || p.Contains("ป.เล็ก")) return "poh";
        if (p.Contains("อปพร"))                          return "vol";
        if (p.Contains("ตำรวจ") || !string.IsNullOrWhiteSpace(r.PoliceStation)) return "police";
        if (p.Contains("รีเฟอ") || p.Contains("ส่งต่อ")) return "ref";
        return "others";
    }

    // ─── Row builders ────────────────────────────────────────────────────────

    private static CaseCountRow BuildCaseCountRow(string label, List<(PrbRecord Rec, int Year, int Month)> data)
    {
        int prbOpd = 0, prbIpd = 0, cashOpd = 0, cashIpd = 0, paOpd = 0, paIpd = 0;
        int totalOpd = 0, totalIpd = 0;

        foreach (var (rec, _, _) in data)
        {
            bool ipd  = IsIpd(rec);
            var payer = GetPayerType(rec);
            if (ipd)  { totalIpd++;  if (payer == "prb") prbIpd++;  else if (payer == "cash") cashIpd++;  else if (payer == "pa") paIpd++;  }
            else      { totalOpd++;  if (payer == "prb") prbOpd++;  else if (payer == "cash") cashOpd++;  else if (payer == "pa") paOpd++;  }
        }

        return new CaseCountRow
        {
            Label    = label,
            PrbOpd   = prbOpd,
            PrbIpd   = prbIpd,
            CashOpd  = cashOpd,
            CashIpd  = cashIpd,
            PaOpd    = paOpd,
            PaIpd    = paIpd,
            TotalOpd = totalOpd,
            TotalIpd = totalIpd,
        };
    }

    private static RevenueRow BuildRevenueRow(string label, List<(PrbRecord Rec, int Year, int Month)> data)
    {
        decimal prbOpd = 0, prbIpd = 0, cashOpd = 0, cashIpd = 0, paOpd = 0, paIpd = 0;
        decimal totalOpd = 0, totalIpd = 0;

        foreach (var (rec, _, _) in data)
        {
            bool ipd = IsIpd(rec);
            if (ipd)
            {
                prbIpd   += rec.HospitalFee;
                cashIpd  += rec.TreatmentCost;
                paIpd    += rec.FundAmount;
                totalIpd += rec.PaymentAmount;
            }
            else
            {
                prbOpd   += rec.HospitalFee;
                cashOpd  += rec.TreatmentCost;
                paOpd    += rec.FundAmount;
                totalOpd += rec.PaymentAmount;
            }
        }

        return new RevenueRow
        {
            Label    = label,
            PrbOpd   = prbOpd,
            PrbIpd   = prbIpd,
            CashOpd  = cashOpd,
            CashIpd  = cashIpd,
            PaOpd    = paOpd,
            PaIpd    = paIpd,
            TotalOpd = totalOpd,
            TotalIpd = totalIpd,
        };
    }

    private static YearlyRevenueRow BuildYearlyRevenueRow(string label, List<(PrbRecord Rec, int Year, int Month)> data)
    {
        decimal prbOpd = 0, prbIpd = 0, cashOpd = 0, cashIpd = 0, paOpd = 0, paIpd = 0;
        decimal totalOpd = 0, totalIpd = 0;

        foreach (var (rec, _, _) in data)
        {
            bool ipd = IsIpd(rec);
            if (ipd)
            {
                prbIpd   += rec.HospitalFee;
                cashIpd  += rec.TreatmentCost;
                paIpd    += rec.FundAmount;
                totalIpd += rec.PaymentAmount;
            }
            else
            {
                prbOpd   += rec.HospitalFee;
                cashOpd  += rec.TreatmentCost;
                paOpd    += rec.FundAmount;
                totalOpd += rec.PaymentAmount;
            }
        }

        return new YearlyRevenueRow
        {
            Label    = label,
            PrbOpd   = prbOpd,
            PrbIpd   = prbIpd,
            CashOpd  = cashOpd,
            CashIpd  = cashIpd,
            PaOpd    = paOpd,
            PaIpd    = paIpd,
            TotalOpd = totalOpd,
            TotalIpd = totalIpd,
        };
    }

    private static ReferrerRow BuildReferrerRow(string label, List<(PrbRecord Rec, int Year, int Month)> data)
    {
        int ruamOpd = 0, ruamIpd = 0, pohOpd = 0, pohIpd = 0;
        int volOpd = 0, volIpd = 0, refOpd = 0, refIpd = 0;
        int othersOpd = 0, othersIpd = 0, policeOpd = 0, policeIpd = 0;
        int totalOpd = 0, totalIpd = 0;

        foreach (var (rec, _, _) in data)
        {
            bool ipd = IsIpd(rec);
            var ref_ = GetReferrer(rec);
            if (ipd)
            {
                totalIpd++;
                switch (ref_) { case "ruam": ruamIpd++;   break; case "poh":    pohIpd++;    break; case "vol":    volIpd++;    break;
                                case "ref":  refIpd++;    break; case "police": policeIpd++; break; default:       othersIpd++; break; }
            }
            else
            {
                totalOpd++;
                switch (ref_) { case "ruam": ruamOpd++;   break; case "poh":    pohOpd++;    break; case "vol":    volOpd++;    break;
                                case "ref":  refOpd++;    break; case "police": policeOpd++; break; default:       othersOpd++; break; }
            }
        }

        return new ReferrerRow
        {
            Label      = label,
            RuamOpd    = ruamOpd,   RuamIpd    = ruamIpd,
            PohOpd     = pohOpd,    PohIpd     = pohIpd,
            VolOpd     = volOpd,    VolIpd     = volIpd,
            RefOpd     = refOpd,    RefIpd     = refIpd,
            OthersOpd  = othersOpd, OthersIpd  = othersIpd,
            PoliceOpd  = policeOpd, PoliceIpd  = policeIpd,
            TotalOpd   = totalOpd,  TotalIpd   = totalIpd,
        };
    }

    /// <summary>Builds a referrer row counting only one status (OPD or IPD), putting counts in OPD columns.</summary>
    private static ReferrerRow BuildReferrerRowSingleStatus(string label, List<(PrbRecord Rec, int Year, int Month)> data)
    {
        int ruam = 0, poh = 0, vol = 0, ref_ = 0, others = 0, police = 0, total = 0;
        bool isIpdLabel = label.Equals("IPD", StringComparison.OrdinalIgnoreCase);

        foreach (var (rec, _, _) in data)
        {
            total++;
            var referrer = GetReferrer(rec);
            switch (referrer)
            {
                case "ruam":   ruam++;   break;
                case "poh":    poh++;    break;
                case "vol":    vol++;    break;
                case "ref":    ref_++;   break;
                case "police": police++; break;
                default:       others++; break;
            }
        }

        if (isIpdLabel)
        {
            return new ReferrerRow
            {
                Label     = label,
                RuamIpd   = ruam,   PohIpd    = poh,
                VolIpd    = vol,    RefIpd    = ref_,
                OthersIpd = others, PoliceIpd = police,
                TotalIpd  = total,
            };
        }
        else
        {
            return new ReferrerRow
            {
                Label     = label,
                RuamOpd   = ruam,   PohOpd    = poh,
                VolOpd    = vol,    RefOpd    = ref_,
                OthersOpd = others, PoliceOpd = police,
                TotalOpd  = total,
            };
        }
    }

    // ─── Count helpers for avg/case calculations ─────────────────────────────

    private static int CountOpdPrb(List<(PrbRecord Rec, int Year, int Month)> data)
        => data.Count(x => !IsIpd(x.Rec) && x.Rec.HospitalFee > 0);
    private static int CountIpdPrb(List<(PrbRecord Rec, int Year, int Month)> data)
        => data.Count(x =>  IsIpd(x.Rec) && x.Rec.HospitalFee > 0);
    private static int CountOpdCash(List<(PrbRecord Rec, int Year, int Month)> data)
        => data.Count(x => !IsIpd(x.Rec) && x.Rec.TreatmentCost > 0);
    private static int CountIpdCash(List<(PrbRecord Rec, int Year, int Month)> data)
        => data.Count(x =>  IsIpd(x.Rec) && x.Rec.TreatmentCost > 0);
    private static int CountOpdPa(List<(PrbRecord Rec, int Year, int Month)> data)
        => data.Count(x => !IsIpd(x.Rec) && x.Rec.FundAmount > 0);
    private static int CountIpdPa(List<(PrbRecord Rec, int Year, int Month)> data)
        => data.Count(x =>  IsIpd(x.Rec) && x.Rec.FundAmount > 0);

    // ─── Math helpers ────────────────────────────────────────────────────────

    private static decimal SafeDiv(decimal numerator, int denominator)
        => denominator == 0 ? 0m : Math.Round(numerator / denominator, 2);

    // ─── Thai date/label helpers ─────────────────────────────────────────────

    private static string ThaiYearLabel(int gregorianYear)
    {
        int be = gregorianYear + 543;
        return $"ปี {be % 100:D2}";
    }

    private static string ThaiMonthShort(int m) => m switch
    {
        1  => "ม.ค.",  2  => "ก.พ.",  3  => "มี.ค.",
        4  => "เม.ย.", 5  => "พ.ค.",  6  => "มิ.ย.",
        7  => "ก.ค.",  8  => "ส.ค.",  9  => "ก.ย.",
        10 => "ต.ค.",  11 => "พ.ย.",  12 => "ธ.ค.",
        _  => m.ToString()
    };

    public static string ThaiYearFull(int gregorianYear)
        => $"พ.ศ. {gregorianYear + 543}";
}
