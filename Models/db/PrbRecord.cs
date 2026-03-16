using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthCoverage.Models.db;

public class PrbRecord
{
    [Key]
    public long Id { get; set; }

    public int ImportId { get; set; }

    [ForeignKey(nameof(ImportId))]
    public PrbImport? Import { get; set; }

    /// <summary>ลำดับที่ในไฟล์ Excel (คอลัมน์ A)</summary>
    public int RowNo { get; set; }

    /// <summary>วันรับบริการ — แปลงจาก DD/MM/YY (พ.ศ.) เป็น ค.ศ. แล้ว (คอลัมน์ B)</summary>
    public DateTime? ServiceDate { get; set; }

    /// <summary>ชื่อ-สกุล ผู้ป่วย (คอลัมน์ C)</summary>
    [MaxLength(300)]
    public string? PatientName { get; set; }

    /// <summary>HN (คอลัมน์ D)</summary>
    [MaxLength(50)]
    public string? Hn { get; set; }

    /// <summary>สถานะ: OPD / IPD (คอลัมน์ E)</summary>
    [MaxLength(10)]
    public string? Status { get; set; }

    /// <summary>บริษัทประกัน (คอลัมน์ F)</summary>
    [MaxLength(255)]
    public string? Company { get; set; }

    /// <summary>รพ. — จำนวนที่บริษัทจ่าย (คอลัมน์ G)</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal HospitalFee { get; set; }

    /// <summary>ประกันชีวิต (คอลัมน์ H)</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal LifeInsurance { get; set; }

    /// <summary>ค่ารักษา (คอลัมน์ I)</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal TreatmentCost { get; set; }

    /// <summary>คอลัมน์ J</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal ColJ { get; set; }

    /// <summary>กองทุน (คอลัมน์ K)</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal FundAmount { get; set; }

    /// <summary>จ่าย — ยอดรวมที่ได้รับ (คอลัมน์ L)</summary>
    [Column(TypeName = "numeric(12,2)")]
    public decimal PaymentAmount { get; set; }

    /// <summary>สถานที่ให้บริการ / รพ. (คอลัมน์ M)</summary>
    [MaxLength(255)]
    public string? Provider { get; set; }

    /// <summary>หมายเหตุ 1 เช่น สำเร็จ / ค้าง (คอลัมน์ N)</summary>
    [MaxLength(100)]
    public string? StatusRemark { get; set; }

    /// <summary>สน. (คอลัมน์ O)</summary>
    [MaxLength(100)]
    public string? PoliceStation { get; set; }

    /// <summary>หมายเหตุ 2 (คอลัมน์ P)</summary>
    [MaxLength(500)]
    public string? Remarks { get; set; }
}
