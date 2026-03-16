using ClosedXML.Excel;
using HealthCoverage.Data;
using HealthCoverage.Models.db;
using HealthCoverage.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace HealthCoverage.Services;

public class ExcelImportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _dbFactory;
    private readonly ILogger<ExcelImportService> _logger;

    public ExcelImportService(
        IDbContextFactory<ApplicationDbContext> dbFactory,
        ILogger<ExcelImportService> logger)
    {
        _dbFactory = dbFactory;
        _logger = logger;
    }

    // ─── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// ตรวจสอบว่าไฟล์ Excel สำหรับเดือน/ปีนั้น import ไปแล้วหรือยัง
    /// </summary>
    public async Task<ImportResultViewModel> PreviewAsync(Stream fileStream, string fileName)
    {
        var result = new ImportResultViewModel();

        try
        {
            var rows = ParseRows(fileStream, result);
            if (!result.Success) return result;

            FillSummary(result, rows);

            await using var db = await _dbFactory.CreateDbContextAsync();
            result.IsDuplicate = await db.PrbImports
                .AnyAsync(i => i.Year == result.Year && i.Month == result.Month);

            result.Success = true;
            result.Message = result.IsDuplicate
                ? $"เดือน {ThaiMonthName(result.Month)} {result.Year + 543} เคย import ไปแล้ว ({result.RecordCount} แถว)"
                : $"พร้อม import {result.RecordCount} แถว (OPD {result.OpdCount}, IPD {result.IpdCount})";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Preview failed for {FileName}", fileName);
            result.Success = false;
            result.Message = $"อ่านไฟล์ไม่ได้: {ex.Message}";
        }

        return result;
    }

    /// <summary>
    /// Import จริง — บันทึกลง DB
    /// </summary>
    public async Task<ImportResultViewModel> ImportAsync(
        Stream fileStream,
        string fileName,
        string? importedByUserId)
    {
        var result = new ImportResultViewModel();

        try
        {
            var records = ParseRows(fileStream, result);
            if (!result.Success) return result;

            FillSummary(result, records);

            await using var db = await _dbFactory.CreateDbContextAsync();

            // ── ตรวจ duplicate ───────────────────────────────────────────────
            bool duplicate = await db.PrbImports
                .AnyAsync(i => i.Year == result.Year && i.Month == result.Month);

            if (duplicate)
            {
                result.Success = false;
                result.IsDuplicate = true;
                result.Message = $"เดือน {ThaiMonthName(result.Month)} {result.Year + 543} เคย import ไปแล้ว กรุณาลบข้อมูลเก่าก่อนถ้าต้องการ import ใหม่";
                return result;
            }

            // ── บันทึก PrbImport header ──────────────────────────────────────
            var importEntity = new PrbImport
            {
                Year          = result.Year,
                Month         = result.Month,
                FileName      = fileName,
                RecordCount   = result.RecordCount,
                ImportedAt    = DateTime.UtcNow,
                ImportedById  = importedByUserId,
            };
            db.PrbImports.Add(importEntity);
            await db.SaveChangesAsync(); // ต้องการ Id ก่อนเพิ่ม records

            // ── bulk insert records ──────────────────────────────────────────
            foreach (var rec in records)
                rec.ImportId = importEntity.Id;

            db.PrbRecords.AddRange(records);
            await db.SaveChangesAsync();

            result.Success = true;
            result.Message = $"Import สำเร็จ {result.RecordCount} แถว (OPD {result.OpdCount} รายการ, IPD {result.IpdCount} รายการ)";

            _logger.LogInformation(
                "Imported {Count} records for {Month}/{Year} by user {User}",
                result.RecordCount, result.Month, result.Year, importedByUserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed for {FileName}", fileName);
            result.Success = false;
            result.Message = $"Import ผิดพลาด: {ex.Message}";
        }

        return result;
    }

    // ─── Parsing ───────────────────────────────────────────────────────────────

    private List<PrbRecord> ParseRows(Stream stream, ImportResultViewModel result)
    {
        var records = new List<PrbRecord>();

        using var wb = new XLWorkbook(stream);
        var ws = wb.Worksheet(1);

        // หาแถวสุดท้ายที่มีข้อมูล (คอลัมน์ A เป็นตัวเลข)
        int lastRow = ws.LastRowUsed()?.RowNumber() ?? 0;

        // row 1 = blank title, row 2 = header → เริ่มที่ row 3
        for (int r = 3; r <= lastRow; r++)
        {
            var row = ws.Row(r);

            // คอลัมน์ A ต้องเป็นตัวเลข (ลำดับที่) — ถ้าไม่ใช่ คือแถว summary ท้ายตาราง
            var cellA = row.Cell(1);
            if (cellA.IsEmpty() || !int.TryParse(cellA.GetString().Trim(), out int rowNo))
                continue;

            // ── วันที่ (คอลัมน์ B) ──────────────────────────────────────────
            DateTime? serviceDate = null;
            var dateStr = row.Cell(2).GetString().Trim();
            if (!string.IsNullOrEmpty(dateStr))
            {
                serviceDate = ParseBuddhistDate(dateStr);
                if (serviceDate == null)
                    result.Warnings.Add($"แถว {r}: วันที่ \"{dateStr}\" parse ไม่ได้ — ข้ามวันที่");
            }

            var rec = new PrbRecord
            {
                RowNo         = rowNo,
                ServiceDate   = serviceDate,
                PatientName   = row.Cell(3).GetString().Trim().NullIfEmpty(),
                Hn            = row.Cell(4).GetString().Trim().NullIfEmpty(),
                Status        = row.Cell(5).GetString().Trim().NullIfEmpty(),
                Company       = row.Cell(6).GetString().Trim().NullIfEmpty(),
                HospitalFee   = ParseDecimal(row.Cell(7)),
                LifeInsurance = ParseDecimal(row.Cell(8)),
                TreatmentCost = ParseDecimal(row.Cell(9)),
                ColJ          = ParseDecimal(row.Cell(10)),
                FundAmount    = ParseDecimal(row.Cell(11)),
                PaymentAmount = ParseDecimal(row.Cell(12)),
                Provider      = row.Cell(13).GetString().Trim().NullIfEmpty(),
                StatusRemark  = row.Cell(14).GetString().Trim().NullIfEmpty(),
                PoliceStation = row.Cell(15).GetString().Trim().NullIfEmpty(),
                Remarks       = row.Cell(16).GetString().Trim().NullIfEmpty(),
            };

            records.Add(rec);
        }

        if (records.Count == 0)
        {
            result.Success = false;
            result.Message = "ไม่พบข้อมูลในไฟล์ (ตรวจสอบว่าข้อมูลเริ่มที่แถว 3 คอลัมน์ A เป็นลำดับที่)";
            return records;
        }

        result.Success = true;
        return records;
    }

    private static void FillSummary(ImportResultViewModel result, List<PrbRecord> records)
    {
        // หา Year/Month จาก ServiceDate ของแถวแรกที่มีวันที่
        var firstDate = records.FirstOrDefault(r => r.ServiceDate.HasValue)?.ServiceDate;
        if (firstDate.HasValue)
        {
            result.Year  = firstDate.Value.Year;
            result.Month = firstDate.Value.Month;
        }

        result.RecordCount = records.Count;

        foreach (var rec in records)
        {
            bool isIpd = string.Equals(rec.Status, "IPD", StringComparison.OrdinalIgnoreCase);
            if (isIpd)
            {
                result.IpdCount++;
                result.IpdTotal += rec.PaymentAmount;
            }
            else
            {
                result.OpdCount++;
                result.OpdTotal += rec.PaymentAmount;
            }
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// แปลงวันที่ DD/MM/YY (พ.ศ.) → DateTime (ค.ศ.)
    /// ตัวอย่าง "01/01/68" → 2025-01-01
    /// </summary>
    private static DateTime? ParseBuddhistDate(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        // รองรับทั้ง "dd/MM/yy" และ "dd/MM/yyyy"
        var parts = s.Split('/');
        if (parts.Length != 3) return null;

        if (!int.TryParse(parts[0], out int day))   return null;
        if (!int.TryParse(parts[1], out int month)) return null;
        if (!int.TryParse(parts[2], out int yearRaw)) return null;

        // แปลงปี พ.ศ. → ค.ศ.
        int buddhistYear = yearRaw < 100 ? 2500 + yearRaw : yearRaw;
        int gregorianYear = buddhistYear - 543;

        try
        {
            return new DateTime(gregorianYear, month, day, 0, 0, 0, DateTimeKind.Utc);
        }
        catch
        {
            return null;
        }
    }

    private static decimal ParseDecimal(IXLCell cell)
    {
        if (cell.IsEmpty()) return 0m;

        // ClosedXML อ่านเป็น double ได้ตรงที่สุดสำหรับ numeric cells
        try { return (decimal)cell.GetDouble(); }
        catch { }

        var s = cell.GetString().Trim().Replace(",", "");
        return decimal.TryParse(s, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out decimal v) ? v : 0m;
    }

    private static string ThaiMonthName(int month) => month switch
    {
        1  => "มกราคม",
        2  => "กุมภาพันธ์",
        3  => "มีนาคม",
        4  => "เมษายน",
        5  => "พฤษภาคม",
        6  => "มิถุนายน",
        7  => "กรกฎาคม",
        8  => "สิงหาคม",
        9  => "กันยายน",
        10 => "ตุลาคม",
        11 => "พฤศจิกายน",
        12 => "ธันวาคม",
        _  => month.ToString()
    };
}

// ─── Extension ─────────────────────────────────────────────────────────────────
file static class StringExtensions
{
    public static string? NullIfEmpty(this string? s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
