using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace HealthCoverage.Models.db;

public class MenuPermission
{
    [Key]
    public int Id { get; set; }

    /// <summary>FK → AspNetUsers.Id</summary>
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public IdentityUser? User { get; set; }

    /// <summary>
    /// ชื่อเมนูที่ได้รับสิทธิ์ เช่น "import", "report", "dashboard"
    /// Admin เห็นทุกเมนูโดยอัตโนมัติ ไม่ต้องมีแถวใน MenuPermission
    /// </summary>
    [Required]
    [MaxLength(100)]
    public string MenuKey { get; set; } = null!;
}
