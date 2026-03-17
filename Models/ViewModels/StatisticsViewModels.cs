namespace HealthCoverage.Models.ViewModels;

public class CaseCountRow
{
    public string Label { get; set; } = "";
    public bool IsHeader { get; set; }
    public bool IsSubRow { get; set; }
    public bool IsTotal { get; set; }
    public bool IsAvg { get; set; }
    public bool IsEmpty { get; set; }

    public int PrbOpd { get; set; }
    public int PrbIpd { get; set; }
    public int CashOpd { get; set; }
    public int CashIpd { get; set; }
    public int PaOpd { get; set; }
    public int PaIpd { get; set; }
    public int TotalOpd { get; set; }
    public int TotalIpd { get; set; }
    public int GrandTotal => TotalOpd + TotalIpd;
}

public class RevenueRow
{
    public string Label { get; set; } = "";
    public bool IsTotal { get; set; }
    public bool IsAvg { get; set; }
    public bool IsEmpty { get; set; }

    public decimal PrbOpd { get; set; }
    public decimal PrbIpd { get; set; }
    public decimal CashOpd { get; set; }
    public decimal CashIpd { get; set; }
    public decimal PaOpd { get; set; }
    public decimal PaIpd { get; set; }
    public decimal TotalOpd { get; set; }
    public decimal TotalIpd { get; set; }
    public decimal GrandTotal => TotalOpd + TotalIpd;
}

public class AvgRevenueRow
{
    public string Label { get; set; } = "";
    public int Year { get; set; }

    public decimal PrbAvgOpd { get; set; }
    public decimal PrbAvgIpd { get; set; }
    public decimal CashAvgOpd { get; set; }
    public decimal CashAvgIpd { get; set; }
    public decimal PaAvgOpd { get; set; }
    public decimal PaAvgIpd { get; set; }
    public decimal AvgOpd { get; set; }
    public decimal AvgIpd { get; set; }
}

public class YearlyRevenueRow
{
    public string Label { get; set; } = "";
    public bool IsSubRow { get; set; }
    public bool IsTotal { get; set; }
    public bool IsAvg { get; set; }

    public decimal PrbOpd { get; set; }
    public decimal PrbIpd { get; set; }
    public decimal CashOpd { get; set; }
    public decimal CashIpd { get; set; }
    public decimal PaOpd { get; set; }
    public decimal PaIpd { get; set; }
    public decimal TotalOpd { get; set; }
    public decimal TotalIpd { get; set; }
    public decimal GrandTotal => TotalOpd + TotalIpd;
}

public class ReferrerRow
{
    public string Label { get; set; } = "";
    public bool IsHeader { get; set; }
    public bool IsSubRow { get; set; }
    public bool IsTotal { get; set; }
    public bool IsAvg { get; set; }
    public bool IsEmpty { get; set; }

    public int RuamOpd { get; set; }
    public int RuamIpd { get; set; }
    public int PohOpd { get; set; }
    public int PohIpd { get; set; }
    public int VolOpd { get; set; }
    public int VolIpd { get; set; }
    public int RefOpd { get; set; }
    public int RefIpd { get; set; }
    public int OthersOpd { get; set; }
    public int OthersIpd { get; set; }
    public int PoliceOpd { get; set; }
    public int PoliceIpd { get; set; }
    public int TotalOpd { get; set; }
    public int TotalIpd { get; set; }
}

public class ReferrerYearBlock
{
    public int Year { get; set; }
    public string YearLabel { get; set; } = "";
    public ReferrerRow Opd { get; set; } = new();
    public ReferrerRow Ipd { get; set; } = new();
    public ReferrerRow Total { get; set; } = new();
    public ReferrerRow Avg { get; set; } = new();
}
