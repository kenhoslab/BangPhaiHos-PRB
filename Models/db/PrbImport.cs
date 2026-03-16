using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace HealthCoverage.Models.db;

public class PrbImport
{
    [Key]
    public int Id { get; set; }

    /// <summary>ปี ค.ศ.</summary>
    public int Year { get; set; }

    /// <summary>เดือน 1-12</summary>
    public int Month { get; set; }

    [MaxLength(255)]
    public string? FileName { get; set; }

    public int RecordCount { get; set; }

    public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

    /// <summary>FK → AspNetUsers.Id</summary>
    [MaxLength(450)]
    public string? ImportedById { get; set; }

    [ForeignKey(nameof(ImportedById))]
    public IdentityUser? ImportedBy { get; set; }

    public ICollection<PrbRecord> Records { get; set; } = new List<PrbRecord>();
}
