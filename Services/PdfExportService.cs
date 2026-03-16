using HealthCoverage.Models.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HealthCoverage.Services;

public class PdfExportService
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<PdfExportService> _logger;

    // Brand colours (hospital green)
    private static readonly string HeaderGreen = "#086435";
    private static readonly string LightGreen  = "#e8f5ee";
    private static readonly string AccentGreen = "#00a652";

    public PdfExportService(IWebHostEnvironment env, ILogger<PdfExportService> logger)
    {
        _env = env;
        _logger = logger;
        QuestPDF.Settings.License = LicenseType.Community;
    }

    // ── Public ───────────────────────────────────────────────────────────────

    /// <summary>สร้าง PDF Dashboard สรุปภาพรวมทุกเดือน</summary>
    public byte[] GenerateDashboardReport(DashboardViewModel vm)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "img", "logo-bangphai-3.png");

        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                SetupPage(page);
                page.Header().Element(c => RenderDashboardHeader(c, logoPath, vm));
                page.Content().Element(c => RenderDashboardContent(c, vm));
                page.Footer().Element(RenderFooter);
            });
        });

        return doc.GeneratePdf();
    }

    private static void RenderDashboardHeader(
        IContainer c, string logoPath, DashboardViewModel vm)
    {
        c.BorderBottom(1).BorderColor(HeaderGreen).PaddingBottom(6).Row(row =>
        {
            row.ConstantItem(70).AlignMiddle().Element(img =>
            {
                if (File.Exists(logoPath))
                    img.Image(logoPath).FitHeight();
                else
                    img.Text("🏥").FontSize(28);
            });

            row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(col =>
            {
                col.Item().Text("สรุปภาพรวมงาน พ.ร.บ.")
                    .FontSize(16).Bold().FontColor(HeaderGreen);
                col.Item().Text("ศูนย์ประสานสิทธิ โรงพยาบาลบางไผ่")
                    .FontSize(9).FontColor("#555555");
            });

            row.ConstantItem(130).AlignMiddle().AlignRight().Column(col =>
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
            col.Spacing(14);

            // ── KPI row ──────────────────────────────────────────────────
            col.Item().Row(row =>
            {
                row.Spacing(6);
                DashKpi(row, "เดือนที่นำเข้า",    $"{vm.TotalMonthsImported} เดือน");
                DashKpi(row, "ผู้ป่วยรวม",         $"{vm.TotalRecords:N0} ราย");
                DashKpi(row, "รายได้รวม",           $"{vm.TotalRevenue:N2} บาท", highlight: true);
                DashKpi(row, "ยอดพรบ.",             $"{vm.TotalPrb:N2} บาท");
                DashKpi(row, "ชำระสด",              $"{vm.TotalCash:N2} บาท");
                DashKpi(row, "ยอดใช้ PA",           $"{vm.TotalPa:N2} บาท");
            });

            // ── Monthly breakdown table ───────────────────────────────────
            col.Item().Text("ตารางสรุปรายเดือน")
                .FontSize(10).Bold().FontColor(HeaderGreen);

            col.Item().Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2);    // เดือน
                    cd.RelativeColumn(1);    // OPD
                    cd.RelativeColumn(1);    // IPD
                    cd.RelativeColumn(1);    // รวม
                    cd.RelativeColumn(1.8f); // พรบ.
                    cd.RelativeColumn(1.8f); // ชำระสด
                    cd.RelativeColumn(1.8f); // PA
                    cd.RelativeColumn(1.8f); // รายได้รวม
                });

                table.Header(h =>
                {
                    foreach (var hdr in new[]
                        { "เดือน", "OPD (ราย)", "IPD (ราย)", "รวม (ราย)",
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
                    void C(string v, bool right = false, bool bold = false)
                    {
                        var t = table.Cell().Background(bg).BorderColor("#dee2e6").Border(0.3f)
                            .Padding(3).AlignMiddle()
                            .Element(e => right ? e.AlignRight() : e.AlignCenter())
                            .Text(v).FontSize(8);
                        if (bold) t.Bold();
                    }

                    C($"{PrbReportService.ThaiMonthName(m.Month)} {m.Year + 543}", right: false);
                    C(m.OpdCount.ToString());
                    C(m.IpdCount.ToString());
                    C(m.TotalCount.ToString(), bold: true);
                    C(m.PrbAmount.ToString("N2"), right: true);
                    C(m.CashAmount.ToString("N2"), right: true);
                    C(m.PaAmount.ToString("N2"), right: true);
                    C(m.TotalRevenue.ToString("N2"), right: true, bold: true);
                    idx++;
                }

                // Grand total row
                string[] totals = {
                    "รวมทั้งหมด",
                    vm.MonthlyStats.Sum(m => m.OpdCount).ToString(),
                    vm.MonthlyStats.Sum(m => m.IpdCount).ToString(),
                    vm.TotalRecords.ToString(),
                    vm.TotalPrb.ToString("N2"),
                    vm.TotalCash.ToString("N2"),
                    vm.TotalPa.ToString("N2"),
                    vm.TotalRevenue.ToString("N2"),
                };
                bool first = true;
                foreach (var t in totals)
                {
                    table.Cell().Background(LightGreen).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3).AlignMiddle()
                        .Element(e => first ? e.AlignLeft() : e.AlignRight())
                        .Text(t).FontSize(8).Bold();
                    first = false;
                }
            });

            // ── Top companies table ───────────────────────────────────────
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
                        foreach (var hdr in new[] { "#", "บริษัทประกัน", "จำนวนราย", "ยอดรวม (บาท)", "สัดส่วน (%)" })
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
                            .Padding(3).AlignRight().Text(co.TotalAmount.ToString("N2")).FontSize(8);
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
                col.Item().Text(value).FontSize(9).Bold()
                    .FontColor(highlight ? HeaderGreen : Colors.Black);
            });
    }

    /// <summary>สร้าง PDF แล้วส่งกลับเป็น byte array</summary>
    public byte[] GenerateMonthlyReport(MonthlyReportViewModel report)
    {
        var logoPath = Path.Combine(_env.WebRootPath, "img", "logo-bangphai-3.png");

        var doc = Document.Create(container =>
        {
            // ── Page 1: Cover + Income Summary ───────────────────────────
            container.Page(page =>
            {
                SetupPage(page);

                page.Header().Element(c => RenderHeader(c, logoPath, report));
                page.Content().Element(c => RenderSummaryContent(c, report));
                page.Footer().Element(RenderFooter);
            });

            // ── Page 2: Full Detail Table ─────────────────────────────────
            if (report.Records.Count > 0)
            {
                container.Page(page =>
                {
                    SetupPage(page, landscape: true);

                    page.Header().Element(c => RenderDetailPageHeader(c, report));
                    page.Content().Element(c => RenderDetailTable(c, report));
                    page.Footer().Element(RenderFooter);
                });
            }
        });

        return doc.GeneratePdf();
    }

    // ── Page setup ───────────────────────────────────────────────────────────

    private static void SetupPage(PageDescriptor page, bool landscape = false)
    {
        if (landscape)
            page.Size(PageSizes.A4.Landscape());
        else
            page.Size(PageSizes.A4);

        page.Margin(1.5f, Unit.Centimetre);
        page.DefaultTextStyle(x => x.FontFamily("Tahoma").FontSize(9));
    }

    // ── Header (logo + title) ────────────────────────────────────────────────

    private static void RenderHeader(
        IContainer c, string logoPath, MonthlyReportViewModel report)
    {
        c.BorderBottom(1).BorderColor(HeaderGreen).PaddingBottom(6).Row(row =>
        {
            // Logo
            row.ConstantItem(70).AlignMiddle().Element(img =>
            {
                if (File.Exists(logoPath))
                    img.Image(logoPath).FitHeight();
                else
                    img.Text("🏥").FontSize(28);
            });

            row.RelativeItem().PaddingLeft(10).AlignMiddle().Column(col =>
            {
                col.Item().Text("รายงานข้อมูลงาน พ.ร.บ.")
                    .FontSize(16).Bold()
                    .FontColor(HeaderGreen);

                col.Item().Text(
                    $"ข้อมูลเดือน {PrbReportService.ThaiMonthName(report.Month)} " +
                    $"พ.ศ. {report.Year + 543}")
                    .FontSize(11).FontColor(AccentGreen);

                col.Item().Text("ศูนย์ประสานสิทธิ โรงพยาบาลบางไผ่")
                    .FontSize(9).FontColor("#555555");
            });

            row.ConstantItem(120).AlignMiddle().AlignRight().Column(col =>
            {
                col.Item().AlignRight().Text($"วันที่พิมพ์: {ThaiDateToday()}")
                    .FontSize(8).FontColor("#888888");
                col.Item().AlignRight().Text($"ไฟล์ต้นฉบับ: {report.FileName}")
                    .FontSize(7).FontColor("#aaaaaa");
            });
        });
    }

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

    // ── Page 1 content ───────────────────────────────────────────────────────

    private static void RenderSummaryContent(IContainer c, MonthlyReportViewModel report)
    {
        c.Column(col =>
        {
            col.Spacing(12);

            // KPI row
            col.Item().Element(e => RenderKpiRow(e, report));

            // Income summary table
            col.Item().Element(e => RenderIncomeSummaryTable(e, report));

            // Company breakdown
            if (report.ByCompany.Count > 0)
                col.Item().Element(e => RenderCompanyTable(e, report));
        });
    }

    // ── KPI cards ────────────────────────────────────────────────────────────

    private static void RenderKpiRow(IContainer c, MonthlyReportViewModel report)
    {
        c.Row(row =>
        {
            row.Spacing(8);
            KpiCard(row, "จำนวนทั้งหมด",     $"{report.GrandTotal.Count} ราย",
                $"OPD {report.OpdSummary.Count} / IPD {report.IpdSummary.Count}");
            KpiCard(row, "ยอดใช้สิทธิ พรบ.",
                report.GrandTotal.PrbAmount.ToString("N2"), "บาท");
            KpiCard(row, "ชำระสด + PA",
                (report.GrandTotal.CashAmount + report.GrandTotal.PaAmount).ToString("N2"), "บาท");
            KpiCard(row, "รายได้รวม",
                report.GrandTotal.TotalAmount.ToString("N2"), "บาท", highlight: true);
        });
    }

    private static void KpiCard(
        RowDescriptor row, string label, string value, string sub, bool highlight = false)
    {
        row.RelativeItem().Border(0.5f).BorderColor("#dee2e6").Padding(8)
            .Background(highlight ? LightGreen : Colors.White)
            .Column(col =>
            {
                col.Item().Text(label).FontSize(8).FontColor("#666666");
                col.Item().Text(value).FontSize(13).Bold()
                    .FontColor(highlight ? HeaderGreen : Colors.Black);
                col.Item().Text(sub).FontSize(7.5f).FontColor("#888888");
            });
    }

    // ── Income summary table ─────────────────────────────────────────────────

    private static void RenderIncomeSummaryTable(IContainer c, MonthlyReportViewModel report)
    {
        c.Column(col =>
        {
            col.Item().Text("ตารางสรุปรายได้อุบัติเหตุจราจร")
                .FontSize(10).Bold().FontColor(HeaderGreen);

            col.Item().PaddingTop(4).Table(table =>
            {
                // Columns
                table.ColumnsDefinition(cd =>
                {
                    cd.RelativeColumn(2.5f); // ประเภท
                    cd.RelativeColumn(1.2f); // จำนวนราย
                    cd.RelativeColumn(2);    // พรบ. OPD
                    cd.RelativeColumn(2);    // พรบ. IPD
                    cd.RelativeColumn(2);    // ชำระสด OPD
                    cd.RelativeColumn(2);    // ชำระสด IPD
                    cd.RelativeColumn(2);    // PA OPD
                    cd.RelativeColumn(2);    // PA IPD
                    cd.RelativeColumn(2);    // รวม OPD
                    cd.RelativeColumn(2);    // รวม IPD
                });

                // Header row 1
                void GreenHeader(string text, uint span = 1) =>
                    table.Header(h => { /* unused — QuestPDF handles header below */ });

                table.Header(h =>
                {
                    void Hdr(string txt, uint col = 1, uint row = 1, bool right = false) =>
                        h.Cell().RowSpan(row).ColumnSpan(col)
                            .Background(HeaderGreen).BorderColor(Colors.White).Border(0.5f)
                            .Padding(4).AlignMiddle()
                            .Element(e => right ? e.AlignRight() : e.AlignCenter())
                            .Text(txt).FontSize(8).Bold().FontColor(Colors.White);

                    Hdr("ประเภท", row: 2);
                    Hdr("จำนวน (ราย)", row: 2);
                    Hdr("ยอดใช้สิทธิ พรบ. (บาท)", col: 2);
                    Hdr("ชำระสด (บาท)", col: 2);
                    Hdr("ยอดใช้ PA (บาท)", col: 2);
                    Hdr("รายได้รวม (บาท)", col: 2);

                    // Sub-header row
                    foreach (var _ in Enumerable.Range(0, 4))
                    {
                        Hdr("OPD"); Hdr("IPD");
                    }
                });

                // Data rows
                void DataRow(ReportSummaryRow s, bool isTotal = false)
                {
                    string bg = isTotal ? LightGreen : Colors.White;

                    void Cell(string v, bool right = true, bool bold = false)
                    {
                        var txt = table.Cell().Background(bg).BorderColor("#dee2e6").Border(0.5f)
                            .Padding(3).AlignMiddle()
                            .Element(e => right ? e.AlignRight() : e.AlignCenter())
                            .Text(v).FontSize(8);
                        if (bold) txt.Bold();
                    }

                    Cell(s.Label, right: false, bold: isTotal);
                    Cell(s.Count.ToString("N0"), bold: isTotal);
                    Cell(s.PrbAmount.ToString("N2"));
                    Cell(isTotal ? report.IpdSummary.PrbAmount.ToString("N2")
                                  : (s.Label == "OPD" ? "" : s.PrbAmount.ToString("N2")));
                    Cell(s.CashAmount.ToString("N2"));
                    Cell(isTotal ? report.IpdSummary.CashAmount.ToString("N2")
                                  : (s.Label == "OPD" ? "" : s.CashAmount.ToString("N2")));
                    Cell(s.PaAmount.ToString("N2"));
                    Cell(isTotal ? report.IpdSummary.PaAmount.ToString("N2")
                                  : (s.Label == "OPD" ? "" : s.PaAmount.ToString("N2")));
                    Cell(s.TotalAmount.ToString("N2"), bold: isTotal);
                    Cell(isTotal ? report.IpdSummary.TotalAmount.ToString("N2")
                                  : (s.Label == "OPD" ? "" : s.TotalAmount.ToString("N2")),
                         bold: isTotal);
                }

                // OPD row
                void OpdIpdRow(ReportSummaryRow opd, ReportSummaryRow ipd)
                {
                    void Cell(string lbl, string v, bool bold = false, bool isGrand = false)
                    {
                        string bg2 = isGrand ? LightGreen : Colors.White;
                        var txt = table.Cell().Background(bg2).BorderColor("#dee2e6").Border(0.5f)
                            .Padding(3).AlignMiddle().AlignRight()
                            .Text(v).FontSize(8);
                        if (bold) txt.Bold();
                    }

                    // OPD
                    table.Cell().Background(Colors.White).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3).AlignMiddle().Text("OPD").FontSize(8);
                    table.Cell().Background(Colors.White).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3).AlignMiddle().AlignCenter().Text(opd.Count.ToString()).FontSize(8);
                    Cell("", opd.PrbAmount.ToString("N2"));
                    Cell("", "");
                    Cell("", opd.CashAmount.ToString("N2"));
                    Cell("", "");
                    Cell("", opd.PaAmount.ToString("N2"));
                    Cell("", "");
                    Cell("", opd.TotalAmount.ToString("N2"), bold: true);
                    Cell("", "");

                    // IPD
                    table.Cell().Background(Colors.White).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3).AlignMiddle().Text("IPD").FontSize(8);
                    table.Cell().Background(Colors.White).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3).AlignMiddle().AlignCenter().Text(ipd.Count.ToString()).FontSize(8);
                    Cell("", "");
                    Cell("", ipd.PrbAmount.ToString("N2"));
                    Cell("", "");
                    Cell("", ipd.CashAmount.ToString("N2"));
                    Cell("", "");
                    Cell("", ipd.PaAmount.ToString("N2"));
                    Cell("", "");
                    Cell("", ipd.TotalAmount.ToString("N2"), bold: true);
                }

                OpdIpdRow(report.OpdSummary, report.IpdSummary);

                // Grand total
                var g = report.GrandTotal;
                string[] totals = {
                    "รวมทั้งหมด",
                    g.Count.ToString(),
                    report.OpdSummary.PrbAmount.ToString("N2"),
                    report.IpdSummary.PrbAmount.ToString("N2"),
                    report.OpdSummary.CashAmount.ToString("N2"),
                    report.IpdSummary.CashAmount.ToString("N2"),
                    report.OpdSummary.PaAmount.ToString("N2"),
                    report.IpdSummary.PaAmount.ToString("N2"),
                    report.OpdSummary.TotalAmount.ToString("N2"),
                    report.IpdSummary.TotalAmount.ToString("N2"),
                };

                bool first = true;
                foreach (var t in totals)
                {
                    table.Cell().Background(LightGreen).BorderColor("#dee2e6").Border(0.5f)
                        .Padding(3).AlignMiddle()
                        .Element(e => first ? e.AlignLeft() : e.AlignRight())
                        .Text(t).FontSize(8).Bold();
                    first = false;
                }
            });
        });
    }

    // ── Company breakdown ────────────────────────────────────────────────────

    private static void RenderCompanyTable(IContainer c, MonthlyReportViewModel report)
    {
        c.Column(col =>
        {
            col.Item().Text("สรุปแยกตามบริษัทประกัน")
                .FontSize(10).Bold().FontColor(HeaderGreen);

            col.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(cd =>
                {
                    cd.ConstantColumn(24);    // #
                    cd.RelativeColumn(4);     // บริษัท
                    cd.RelativeColumn(1.5f);  // จำนวนราย
                    cd.RelativeColumn(2);     // ยอดรวม
                    cd.RelativeColumn(1);     // %
                });

                table.Header(h =>
                {
                    foreach (var txt in new[] { "#", "บริษัทประกัน", "จำนวนราย", "ยอดรวม (บาท)", "สัดส่วน (%)" })
                        h.Cell().Background(HeaderGreen).Border(0.5f).BorderColor(Colors.White)
                            .Padding(3).AlignCenter()
                            .Text(txt).FontSize(8).Bold().FontColor(Colors.White);
                });

                int rank = 1;
                foreach (var co in report.ByCompany)
                {
                    var pct = report.GrandTotal.TotalAmount > 0
                        ? (double)co.TotalAmount / (double)report.GrandTotal.TotalAmount * 100
                        : 0;
                    string bg = rank % 2 == 0 ? "#f9f9f9" : Colors.White;

                    table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                        .Padding(3).AlignCenter().Text(rank.ToString()).FontSize(8);
                    table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                        .Padding(3).Text(co.Company).FontSize(8);
                    table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                        .Padding(3).AlignCenter().Text(co.Count.ToString()).FontSize(8);
                    table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                        .Padding(3).AlignRight().Text(co.TotalAmount.ToString("N2")).FontSize(8);
                    table.Cell().Background(bg).Border(0.5f).BorderColor("#dee2e6")
                        .Padding(3).AlignRight().Text($"{pct:F1}%").FontSize(8);

                    rank++;
                }
            });
        });
    }

    // ── Detail table (page 2, landscape) ────────────────────────────────────

    private static void RenderDetailTable(IContainer c, MonthlyReportViewModel report)
    {
        c.Table(table =>
        {
            table.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(22);   // #
                cd.ConstantColumn(52);   // วันที่
                cd.RelativeColumn(3);    // ชื่อ
                cd.ConstantColumn(54);   // HN
                cd.ConstantColumn(28);   // ประเภท
                cd.RelativeColumn(2);    // บริษัท
                cd.RelativeColumn(1.5f); // พรบ.
                cd.RelativeColumn(1.5f); // ชำระสด
                cd.RelativeColumn(1.5f); // PA
                cd.RelativeColumn(1.5f); // รวม
                cd.RelativeColumn(1.5f); // สถานะ
            });

            // Header
            table.Header(h =>
            {
                foreach (var hdr in new[]
                    { "#", "วันที่", "ชื่อ-สกุล", "HN", "ประเภท",
                      "บริษัท", "พรบ. (บาท)", "ชำระสด (บาท)", "PA (บาท)", "รวม (บาท)", "หมายเหตุ" })
                {
                    h.Cell().Background(HeaderGreen).BorderColor(Colors.White).Border(0.5f)
                        .Padding(3).AlignCenter()
                        .Text(hdr).FontSize(7.5f).Bold().FontColor(Colors.White);
                }
            });

            // Data rows
            int idx = 1;
            foreach (var r in report.Records)
            {
                string rowBg = r.Status == "IPD" ? "#fff8e8"
                             : idx % 2 == 0 ? "#f9fafb"
                             : Colors.White;

                void DC(string v, bool right = false, bool bold = false)
                {
                    var txt = table.Cell().Background(rowBg).BorderColor("#dee2e6").Border(0.3f)
                           .Padding(2).AlignMiddle()
                           .Element(e => right ? e.AlignRight() : e.AlignLeft())
                           .Text(v).FontSize(7.5f);
                    if (bold) txt.Bold();
                }

                DC(idx.ToString(), right: true);
                DC(r.ServiceDateDisplay);
                DC(r.PatientName);
                DC(r.Hn);
                DC(r.Status, right: false, bold: r.Status == "IPD");
                DC(r.Company);
                DC(r.HospitalFee > 0   ? r.HospitalFee.ToString("N2")   : "—", right: true);
                DC(r.TreatmentCost > 0 ? r.TreatmentCost.ToString("N2") : "—", right: true);
                DC(r.FundAmount > 0    ? r.FundAmount.ToString("N2")    : "—", right: true);
                DC(r.PaymentAmount.ToString("N2"), right: true, bold: true);
                DC(r.StatusRemark ?? "");

                idx++;
            }

            // Footer totals
            string[] footerVals =
            {
                "", "รวม", $"{report.Records.Count} ราย", "", "",  "",
                report.Records.Sum(r => r.HospitalFee).ToString("N2"),
                report.Records.Sum(r => r.TreatmentCost).ToString("N2"),
                report.Records.Sum(r => r.FundAmount).ToString("N2"),
                report.Records.Sum(r => r.PaymentAmount).ToString("N2"),
                "",
            };

            bool firstCell = true;
            foreach (var fv in footerVals)
            {
                table.Cell().Background(LightGreen).BorderColor("#dee2e6").Border(0.5f)
                    .Padding(3).AlignMiddle()
                    .Element(e => firstCell ? e.AlignLeft() : e.AlignRight())
                    .Text(fv).FontSize(7.5f).Bold();
                firstCell = false;
            }
        });
    }

    // ── Footer ───────────────────────────────────────────────────────────────

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

    // ── Utility ──────────────────────────────────────────────────────────────

    private static string ThaiDateToday()
    {
        var now = DateTime.Now;
        return $"{now.Day:D2}/{now.Month:D2}/{now.Year + 543}";
    }
}
