using HealthCoverage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HealthCoverage.Controllers;

[ApiController]
[Route("report")]
[Authorize]
public class ReportController : ControllerBase
{
    private readonly PrbReportService _reportService;
    private readonly PdfExportService _pdfService;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        PrbReportService reportService,
        PdfExportService pdfService,
        ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _pdfService    = pdfService;
        _logger        = logger;
    }

    /// <summary>
    /// GET /report/monthly/pdf?year=2025&amp;month=1
    /// ส่งคืนไฟล์ PDF สำหรับดาวน์โหลด
    /// </summary>
    [HttpGet("monthly/pdf")]
    public async Task<IActionResult> MonthlyPdf([FromQuery] int year, [FromQuery] int month)
    {
        if (year < 2000 || year > 2100 || month < 1 || month > 12)
            return BadRequest("Invalid year or month.");

        var report = await _reportService.GetMonthlyReportAsync(year, month);
        if (report is null)
            return NotFound($"ไม่พบข้อมูลเดือน {month}/{year}");

        try
        {
            var pdfBytes = _pdfService.GenerateMonthlyReport(report);

            var thaiMonth = PrbReportService.ThaiMonthShort(month);
            var buddhistYear = year + 543;
            var fileName = $"พรบ_{thaiMonth}_{buddhistYear}.pdf";
            // encode filename for Content-Disposition
            var encodedName = Uri.EscapeDataString(fileName);

            return File(
                pdfBytes,
                "application/pdf",
                fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PDF generation failed for {Year}/{Month}", year, month);
            return StatusCode(500, "สร้าง PDF ไม่สำเร็จ กรุณาลองใหม่");
        }
    }

    /// <summary>GET /report/dashboard/pdf — สรุปภาพรวมทุกเดือน</summary>
    [HttpGet("dashboard/pdf")]
    public async Task<IActionResult> DashboardPdf()
    {
        try
        {
            var vm = await _reportService.GetDashboardAsync();
            if (vm.TotalMonthsImported == 0)
                return NotFound("ยังไม่มีข้อมูล");

            var pdfBytes = _pdfService.GenerateDashboardReport(vm);
            var today    = DateTime.Now;
            var fileName = $"พรบ_Dashboard_{today.Year + 543}{today.Month:D2}{today.Day:D2}.pdf";

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Dashboard PDF generation failed");
            return StatusCode(500, "สร้าง PDF ไม่สำเร็จ กรุณาลองใหม่");
        }
    }
}
