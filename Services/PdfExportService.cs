using HealthCoverage.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthCoverage.Services;

public class PdfExportService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PdfExportService> _logger;

    private static readonly string HeaderGreen = "#086435";
    private static readonly string LightGreen  = "#e8f5ee";
    private static readonly string AccentGreen = "#00a652";

    public PdfExportService(IWebHostEnvironment env, ILogger<PdfExportService> logger)
    {
        _env    = env;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Public ───────────────────────────────────────────────────────────────

    public byte[] GenerateDashboardReport(DashboardViewModel vm)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "img", "LogoBangphai.png");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Tahoma").FontSize(9));

                page.Header().Element(c => RenderDashboardHeader(c, logoPath, vm));
                page.Content().Element(c => RenderDashboardContent(c, vm));
                page.Footer().Element(RenderFooter);
            });
        });

        return doc.GeneratePdf();
    }

    public byte[] GenerateMonthlyReport(MonthlyReportViewModel report)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "img", "LogoBangphai.png");

        var doc = Document.Create(container =>
        {
            // Page 1: Cover + Summary
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontFamily("Tahoma").FontSize(9));

                page.Header().Element(c => RenderMonthlyHeader(c, logoPath, report));
                page.Content().Element(c => RenderSummaryContent(c, report));
                page.Footer().Element(RenderFooter);
            });

            // Page 2: Detail table (landscape)
            if (report.Records.Count > 0)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.DefaultTextStyle(x => x.FontFamily("Tahoma").FontSize(9));

                    page.Header().Element(c => RenderDetailPageHeader(c, report));
                    page.Content().Element(c => RenderDetailTable(c, report));
                    page.Footer().Element(RenderFooter);
                });
            }
        });

        return doc.GeneratePdf();
    }

    // ── Dashboard ─────────────────────────────────────────────────────────────

    private static void RenderDashboardHeader(IContainer c, string logoPath, DashboardViewModel vm)
    {
        c.BorderBottom(1).BorderColor(HeaderGreen).PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(60).Element(img =>
            {
                if (File.Exists(logoPath))
                    img.Image(logoPath).FitHeight();
                else
                    img.Text("รพ.").FontSize(14).Bold().FontColor(HeaderGreen);
            });

            row.RelativeItem().PaddingLeft(10).Column(col =>
            {
                col.Item().Text("สรุปภาพรวมงาน พ.ร.บ.")
                    .FontSize(15).Bold().FontColor(HeaderGreen);
                col.Item().Text("ศูนย์ประสานสิทธิ โรงพยาบาลบางไผ่")
                    .FontSize(9).FontColor("#555555");
            });

            row.ConstantItem(150).Column(col =>
            {
                col.Item().AlignRight().Text($"วันที่พิมพ์: {ThaiDateToday()}")
                    .FontSize(8).FontColor("#888888");
                col.Item().AlignRight()
                    .Text($"ข้อมูล {vm.TotalMonthsImported} เดือน  |  {vm.TotalRecords:N0} ราย")
                    .FontSize(8).FontColor("#888888");
            });
        });
    }

    private static void RenderDashboardContent(IContainer c, DashboardViewModel vm)
    {
        c.Column(col =>
        {
            col.Spacing(12);

            // KPI row — 3 wide cards on landscape
            col.Item().Row(row =>
            {
                row.Spacing(6);
                DashKpi(row, "ผู้ป่วยรวม",   $"{vm.TotalRecords:N0} ราย");
                DashKpi(row, "รายได้รวม",     FormatBaht(vm.TotalRevenue), highlight: true);
                DashKpi(row, "ยอดพรบ.",       FormatBaht(vm.TotalPrb));
                DashKpi(row, "ชำระสด",        FormatBaht(vm.TotalCash));
                DashKpi(row, "ยอดใช้ PA",     FormatBaht(vm.TotalPa));
            });

            // Monthly breakdown table
            col.Item().Text("ตารางสรุปรายเดือน")
                .FontSize(10).Bold().FontColor(HeaderGreen);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2.2f);  // เดือน
                    cd.RelativeColumn(1);     // OPD
                    cd.RelativeColumn(1);     // IPD
                    cd.RelativeColumn(1);     // รวม
                    cd.RelativeColumn(1.8f);  // พรบ.
                    cd.RelativeColumn(1.8f);  // ชำระสด
                    cd.RelativeColumn(1.8f);  // PA
                    cd.RelativeColumn(1.8f);  // รายได้รวม
                });

                table.Header(h =>
                {
                    foreach (var hdr in new[]
                        { "เดือน", "OPD", "IPD", "รวม",
                          "ยอดพรบ. (บาท)", "ชำระสด (บาท)", "PA (บาท)", "รายได้รวม (บาท)" })
                    {
                        h.Cell().Background(HeaderGreen).BorderColor(Colors.White).Border(0.5f)
                            .Padding(4).AlignCenter()
                            .Text(hdr).FontSize(8).Bold().FontColor(Colors.White);
                    }
                });

                var sorted = vm.MonthlyStats.AsEnumerable().Reverse().ToList();
                int idx = 0;
                foreach (var m in sorted)
                {
                    string bg = idx % 2 == 0 ? Colors.White : "#f9f9f9";

                    void DC(string v, bool right = false, bool bold = false)
                    {
                        var t = table.Cell().Background(bg).BorderColor("#dee2e6").Border(0.3f)
                            .Padding(3)
                            .Element(e => right ? e.AlignRight() : e.AlignCenter())
                            .Text(v).FontSize(8);
                        if (bold) t.Bold();
                    }

                    DC($"{PrbReportService.ThaiMonthName(m.Month)} {m.Year + 543}", right: false);
                    DC(m.OpdCount.ToString());
                    DC(m.IpdCount.ToString());
                    DC(m.TotalCount.ToString(), bold: true);
                    DC(m.PrbAmount.ToString("N0"), right: true);
                    DC(m.CashAmount.ToString("N0"), right: true);
                    DC(m.PaAmount.ToString("N0"), right: true);
                    DC(m.TotalRevenue.ToString("N0"), right: true, bold: true);
                    idx++;
                }

                // Total row
                var totals = new[]
                {
                    ("รวมทั้งหมด", false, true),
                    (vm.MonthlyStats.Sum(m => m.OpdCount).ToString(), true, true),
                    (vm.MonthlyStats.Sum(m => m.IpdCount).ToString(), true, true),
                    (vm.TotalRecords.ToString(), true, true),
                    (vm.TotalPrb.ToString("N0"), true, true),
                    (vm.TotalCash.ToString("N0"), true, true),
                    (vm.TotalPa.ToString("N0"), true, true),
                    (vm.TotalRevenue.ToString("N0"), true, true),
                };
                foreach (var (v, right, bold) in totals)
                {
                    var t = table.Cell().Background(LightGreen).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3)
                        .Element(e => right ? e.AlignRight() : e.AlignLeft())
                        .Text(v).FontSize(8);
                    if (bold) t.Bold();
                }
            });

            // Top companies
            if (vm.TopCompanies.Count > 0)
            {
                col.Item().Text("สรุปแยกตามบริษัทประกัน (Top 8)")
                    .FontSize(10).Bold().FontColor(HeaderGreen);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(24);
                        cd.RelativeColumn(4);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        foreach (var hdr in new[] { "#", "บริษัทประกัน", "จำนวนราย", "ยอดรวม (บาท)", "%" })
                            h.Cell().Background(HeaderGreen).Border(0.5f).BorderColor(Colors.White)
                                .Padding(3).AlignCenter()
                                .Text(hdr).FontSize(8).Bold().FontColor(Colors.White);
                    });

                    int rank = 1;
                    foreach (var co in vm.TopCompanies)
                    {
                        var pct = vm.TotalRevenue > 0
                            ? (double)co.TotalAmount / (double)vm.TotalRevenue * 100 : 0;
                        string bg = rank % 2 == 0 ? "#f9f9f9" : Colors.White;

                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignCenter().Text(rank.ToString()).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).Text(co.Company).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignCenter().Text(co.Count.ToString()).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignRight().Text(co.TotalAmount.ToString("N0")).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignRight().Text($"{pct:F1}%").FontSize(8);
                        rank++;
                    }
                });
            }
        });
    }

    private static void DashKpi(RowDescriptor row, string label, string value, bool highlight = false)
    {
        row.RelativeItem()
            .Border(0.5f).BorderColor("#dee2e6")
            .Background(highlight ? LightGreen : Colors.White)
            .Padding(8).Column(col =>
            {
                col.Item().Text(label).FontSize(7.5f).FontColor("#666666");
                col.Item().Text(value).FontSize(10).Bold()
                    .FontColor(highlight ? HeaderGreen : Colors.Black);
            });
    }

    // ── Monthly report ────────────────────────────────────────────────────────

    private static void RenderMonthlyHeader(IContainer c, string logoPath, MonthlyReportViewModel report)
    {
        c.BorderBottom(1).BorderColor(HeaderGreen).PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(60).Element(img =>
            {
                if (File.Exists(logoPath))
                    img.Image(logoPath).FitHeight();
                else
                    img.Text("รพ.").FontSize(14).Bold().FontColor(HeaderGreen);
            });

            row.RelativeItem().PaddingLeft(10).Column(col =>
            {
                col.Item().Text("รายงานข้อมูลงาน พ.ร.บ.")
                    .FontSize(15).Bold().FontColor(HeaderGreen);
                col.Item().Text(
                    $"ข้อมูลเดือน {PrbReportService.ThaiMonthName(report.Month)} พ.ศ. {report.Year + 543}")
                    .FontSize(10).FontColor(AccentGreen);
                col.Item().Text("ศูนย์ประสานสิทธิ โรงพยาบาลบางไผ่")
                    .FontSize(9).FontColor("#555555");
            });

            row.ConstantItem(130).Column(col =>
            {
                col.Item().AlignRight().Text($"วันที่พิมพ์: {ThaiDateToday()}")
                    .FontSize(8).FontColor("#888888");
                col.Item().AlignRight().Text($"ไฟล์: {report.FileName}")
                    .FontSize(7).FontColor("#aaaaaa");
            });
        });
    }

    private static void RenderSummaryContent(IContainer c, MonthlyReportViewModel report)
    {
        c.Column(col =>
        {
            col.Spacing(12);

            // KPI cards — 4 per row on portrait
            col.Item().Row(row =>
            {
                row.Spacing(6);
                MonthKpi(row, "จำนวนทั้งหมด",
                    $"{report.GrandTotal.Count} ราย",
                    $"OPD {report.OpdSummary.Count} / IPD {report.IpdSummary.Count}");
                MonthKpi(row, "ยอดใช้สิทธิ พรบ.",
                    FormatBaht(report.GrandTotal.PrbAmount), "บาท");
                MonthKpi(row, "ชำระสด + PA",
                    FormatBaht(report.GrandTotal.CashAmount + report.GrandTotal.PaAmount), "บาท");
                MonthKpi(row, "รายได้รวม",
                    FormatBaht(report.GrandTotal.TotalAmount), "บาท", highlight: true);
            });

            // Income summary table (simple single-row header)
            col.Item().Text("ตารางสรุปรายได้อุบัติเหตุจราจร")
                .FontSize(10).Bold().FontColor(HeaderGreen);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2);    // ประเภท
                    cd.RelativeColumn(1.2f); // จำนวน
                    cd.RelativeColumn(1.8f); // พรบ.OPD
                    cd.RelativeColumn(1.8f); // พรบ.IPD
                    cd.RelativeColumn(1.8f); // สดOPD
                    cd.RelativeColumn(1.8f); // สดIPD
                    cd.RelativeColumn(1.8f); // PA OPD
                    cd.RelativeColumn(1.8f); // PA IPD
                    cd.RelativeColumn(1.8f); // รวม OPD
                    cd.RelativeColumn(1.8f); // รวม IPD
                });

                table.Header(h =>
                {
                    foreach (var hdr in new[]
                    {
                        "ประเภท", "จำนวน(ราย)",
                        "พรบ.OPD", "พรบ.IPD",
                        "สด OPD",  "สด IPD",
                        "PA OPD",  "PA IPD",
                        "รวม OPD", "รวม IPD",
                    })
                    {
                        h.Cell().Background(HeaderGreen).BorderColor(Colors.White).Border(0.5f)
                            .Padding(3).AlignCenter()
                            .Text(hdr).FontSize(7.5f).Bold().FontColor(Colors.White);
                    }
                });

                // OPD row
                TableRow(table, Colors.White, false,
                    "OPD",
                    report.OpdSummary.Count.ToString(),
                    report.OpdSummary.PrbAmount.ToString("N0"), "—",
                    report.OpdSummary.CashAmount.ToString("N0"), "—",
                    report.OpdSummary.PaAmount.ToString("N0"), "—",
                    report.OpdSummary.TotalAmount.ToString("N0"), "—");

                // IPD row
                TableRow(table, "#f9f9f9", false,
                    "IPD",
                    report.IpdSummary.Count.ToString(),
                    "—", report.IpdSummary.PrbAmount.ToString("N0"),
                    "—", report.IpdSummary.CashAmount.ToString("N0"),
                    "—", report.IpdSummary.PaAmount.ToString("N0"),
                    "—", report.IpdSummary.TotalAmount.ToString("N0"));

                // Grand total row
                TableRow(table, LightGreen, isTotal: true,
                    "รวมทั้งหมด",
                    report.GrandTotal.Count.ToString(),
                    report.OpdSummary.PrbAmount.ToString("N0"),
                    report.IpdSummary.PrbAmount.ToString("N0"),
                    report.OpdSummary.CashAmount.ToString("N0"),
                    report.IpdSummary.CashAmount.ToString("N0"),
                    report.OpdSummary.PaAmount.ToString("N0"),
                    report.IpdSummary.PaAmount.ToString("N0"),
                    report.OpdSummary.TotalAmount.ToString("N0"),
                    report.IpdSummary.TotalAmount.ToString("N0"));
            });

            // Company breakdown
            if (report.ByCompany.Count > 0)
            {
                col.Item().Text("สรุปแยกตามบริษัทประกัน")
                    .FontSize(10).Bold().FontColor(HeaderGreen);

                col.Item().PaddingTop(4).Table(table =>
                {
                    table.ColumnsDefinition(cd =>
                    {
                        cd.ConstantColumn(20);
                        cd.RelativeColumn(4);
                        cd.RelativeColumn(1.5f);
                        cd.RelativeColumn(2);
                        cd.RelativeColumn(1);
                    });

                    table.Header(h =>
                    {
                        foreach (var txt in new[] { "#", "บริษัทประกัน", "จำนวนราย", "ยอดรวม (บาท)", "%" })
                            h.Cell().Background(HeaderGreen).Border(0.5f).BorderColor(Colors.White)
                                .Padding(3).AlignCenter()
                                .Text(txt).FontSize(8).Bold().FontColor(Colors.White);
                    });

                    int rank = 1;
                    foreach (var co in report.ByCompany)
                    {
                        var pct = report.GrandTotal.TotalAmount > 0
                            ? (double)co.TotalAmount / (double)report.GrandTotal.TotalAmount * 100 : 0;
                        string bg = rank % 2 == 0 ? "#f9f9f9" : Colors.White;

                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignCenter().Text(rank.ToString()).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).Text(co.Company).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignCenter().Text(co.Count.ToString()).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignRight().Text(co.TotalAmount.ToString("N0")).FontSize(8);
                        table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                            .Padding(3).AlignRight().Text($"{pct:F1}%").FontSize(8);
                        rank++;
                    }
                });
            }
        });
    }

    private static void MonthKpi(RowDescriptor row, string label, string value, string sub, bool highlight = false)
    {
        row.RelativeItem().Border(0.5f).BorderColor("#dee2e6").Padding(8)
            .Background(highlight ? LightGreen : Colors.White)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(8).FontColor("#666666");
                col.Item().Text(value).FontSize(11).Bold()
                    .FontColor(highlight ? HeaderGreen : Colors.Black);
                col.Item().Text(sub).FontSize(7.5f).FontColor("#888888");
            });
    }

    // ── Detail table ──────────────────────────────────────────────────────────

    private static void RenderDetailPageHeader(IContainer c, MonthlyReportViewModel report)
    {
        c.BorderBottom(1).BorderColor(HeaderGreen).PaddingBottom(4).Row(row =>
        {
            row.RelativeItem().Text(
                $"รายละเอียดผู้ป่วย — เดือน {PrbReportService.ThaiMonthName(report.Month)} " +
                $"พ.ศ. {report.Year + 543}  ({report.Records.Count} ราย)")
                .FontSize(11).Bold().FontColor(HeaderGreen);

            row.ConstantItem(100).AlignRight().Text($"พิมพ์: {ThaiDateToday()}")
                .FontSize(8).FontColor("#888888");
        });
    }

    private static void RenderDetailTable(IContainer c, MonthlyReportViewModel report)
    {
        c.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(22);    // #
                cd.ConstantColumn(52);    // วันที่
                cd.RelativeColumn(3);     // ชื่อ
                cd.ConstantColumn(50);    // HN
                cd.ConstantColumn(26);    // ประเภท
                cd.RelativeColumn(2);     // บริษัท
                cd.RelativeColumn(1.5f);  // พรบ.
                cd.RelativeColumn(1.5f);  // ชำระสด
                cd.RelativeColumn(1.5f);  // PA
                cd.RelativeColumn(1.5f);  // รวม
                cd.RelativeColumn(1.5f);  // หมายเหตุ
            });

            table.Header(h =>
            {
                foreach (var hdr in new[]
                    { "#", "วันที่", "ชื่อ-สกุล", "HN", "ประเภท",
                      "บริษัท", "พรบ.(บาท)", "ชำระสด", "PA", "รวม(บาท)", "หมายเหตุ" })
                {
                    h.Cell().Background(HeaderGreen).BorderColor(Colors.White).Border(0.5f)
                        .Padding(3).AlignCenter()
                        .Text(hdr).FontSize(7.5f).Bold().FontColor(Colors.White);
                }
            });

            int idx = 1;
            foreach (var r in report.Records)
            {
                string rowBg = r.Status == "IPD" ? "#fff8e8"
                             : idx % 2 == 0 ? "#f9fafb"
                             : Colors.White;

                void DC(string v, bool right = false, bool bold = false)
                {
                    var txt = table.Cell().Background(rowBg).BorderColor("#dee2e6").Border(0.3f)
                           .Padding(2)
                           .Element(e => right ? e.AlignRight() : e.AlignLeft())
                           .Text(v).FontSize(7.5f);
                    if (bold) txt.Bold();
                }

                DC(idx.ToString(), right: true);
                DC(r.ServiceDateDisplay);
                DC(r.PatientName);
                DC(r.Hn);
                DC(r.Status, bold: r.Status == "IPD");
                DC(r.Company);
                DC(r.HospitalFee > 0   ? r.HospitalFee.ToString("N0")   : "—", right: true);
                DC(r.TreatmentCost > 0 ? r.TreatmentCost.ToString("N2") : "—", right: true);
                DC(r.FundAmount > 0    ? r.FundAmount.ToString("N0")    : "—", right: true);
                DC(r.PaymentAmount.ToString("N0"), right: true, bold: true);
                DC(r.StatusRemark ?? "");
                idx++;
            }

            // Footer
            var footerVals = new[]
            {
                ("", false), ("รวม", false), ($"{report.Records.Count} ราย", false),
                ("", false), ("", false), ("", false),
                (report.Records.Sum(r => r.HospitalFee).ToString("N0"), true),
                (report.Records.Sum(r => r.TreatmentCost).ToString("N0"), true),
                (report.Records.Sum(r => r.FundAmount).ToString("N0"), true),
                (report.Records.Sum(r => r.PaymentAmount).ToString("N0"), true),
                ("", false),
            };
            foreach (var (fv, right) in footerVals)
            {
                table.Cell().Background(LightGreen).BorderColor("#dee2e6").Border(0.5f)
                    .Padding(3)
                    .Element(e => right ? e.AlignRight() : e.AlignLeft())
                    .Text(fv).FontSize(7.5f).Bold();
            }
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void TableRow(TableDescriptor table, string bg, bool isTotal = false,
        params string[] values)
    {
        foreach (var v in values)
        {
            var t = table.Cell().Background(bg).BorderColor("#dee2e6").Border(0.5f)
                .Padding(3)
                .Element(e => IsNumeric(v) || v == "—" ? e.AlignRight() : e.AlignLeft())
                .Text(v).FontSize(8);
            if (isTotal) t.Bold();
        }
    }

    private static bool IsNumeric(string s) =>
        s.Length > 0 && (char.IsDigit(s[0]) || s[0] == '-');

    private static string FormatBaht(decimal v)
    {
        if (v >= 1_000_000)
            return $"{v / 1_000_000:N2} ล้าน";
        if (v >= 1_000)
            return $"{v / 1_000:N1} K";
        return v.ToString("N0");
    }

    private static void RenderFooter(IContainer c)
    {
        c.BorderTop(0.5f).BorderColor("#cccccc").PaddingTop(4)
            .Row(row =>
            {
                row.RelativeItem().Text("โรงพยาบาลบางไผ่  |  ศูนย์ประสานสิทธิ  |  พ.ร.บ.คุ้มครองผู้ประสบภัยจากรถ")
                    .FontSize(7.5f).FontColor("#888888");

                row.ConstantItem(60).AlignRight().Text(x =>
                {
                    x.Span("หน้า ").FontSize(7.5f).FontColor("#888888");
                    x.CurrentPageNumber().FontSize(7.5f).FontColor("#888888");
                    x.Span(" / ").FontSize(7.5f).FontColor("#888888");
                    x.TotalPages().FontSize(7.5f).FontColor("#888888");
                });
            });
    }

    private static string ThaiDateToday()
    {
        var now = DateTime.Now;
        return $"{now.Day:D2}/{now.Month:D2}/{now.Year + 543}";
    }
}
