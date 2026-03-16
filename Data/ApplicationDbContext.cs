using HealthCoverage.Models.db;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace HealthCoverage.Data
{
    public partial class ApplicationDbContext : IdentityDbContext<IdentityUser, IdentityRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        public virtual DbSet<RegisterLab> RegisterLabs { get; set; }
        public virtual DbSet<PrbImport> PrbImports { get; set; }
        public virtual DbSet<PrbRecord> PrbRecords { get; set; }
        public virtual DbSet<MenuPermission> MenuPermissions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<RegisterLab>(entity =>
			{
				// กำหนด Id เป็น Primary Key (ตาม PostgreSQL)
				entity.HasKey(e => e.Id).HasName("id_index");

				entity.ToTable("RegisterLab");

				// Indexes (ตาม PostgreSQL)
				entity.HasIndex(e => e.IdentityCard, "Register_new_2_idx13");
				entity.HasIndex(e => e.FullName, "Register_new_2_idx14");

				// Property configurations
				entity.Property(e => e.Id)
					.HasColumnName("Id")
					.ValueGeneratedOnAdd(); // สำหรับ GENERATED ALWAYS AS IDENTITY

				entity.Property(e => e.LabNumber)
					.HasMaxLength(20)
					.IsRequired();

				entity.Property(e => e.IdentityCard)
					.HasMaxLength(20);

				entity.Property(e => e.Hn)
					.HasMaxLength(30)
					.HasColumnName("Hn");

				entity.Property(e => e.FullName)
					.HasMaxLength(300);

				entity.Property(e => e.Sex)
					.HasMaxLength(10);

				entity.Property(e => e.AgeStr)
					.HasMaxLength(50);

				entity.Property(e => e.BirthDate)
					.HasMaxLength(10);

				entity.Property(e => e.Doctor)
					.HasMaxLength(255);

				entity.Property(e => e.RegisterDate)
					.HasMaxLength(15); 

				entity.Property(e => e.ResultData)
					.HasColumnType("text"); 
			});

	            // PrbImport — ป้องกัน import ซ้ำในเดือนเดียวกัน
            modelBuilder.Entity<PrbImport>(entity =>
            {
                entity.ToTable("PrbImport");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.Year, e.Month }).IsUnique().HasDatabaseName("IX_PrbImport_YearMonth");
                entity.Property(e => e.ImportedAt).HasDefaultValueSql("now()");
            });

            // PrbRecord — แต่ละแถวจากไฟล์ Excel
            modelBuilder.Entity<PrbRecord>(entity =>
            {
                entity.ToTable("PrbRecord");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ImportId).HasDatabaseName("IX_PrbRecord_ImportId");
                entity.HasOne(e => e.Import)
                      .WithMany(i => i.Records)
                      .HasForeignKey(e => e.ImportId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // MenuPermission — unique ต่อ user+menu
            modelBuilder.Entity<MenuPermission>(entity =>
            {
                entity.ToTable("MenuPermission");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => new { e.UserId, e.MenuKey }).IsUnique().HasDatabaseName("IX_MenuPermission_UserMenu");
            });

		OnModelCreatingPartial(modelBuilder);
        }

        partial void OnModelCreatingPartial(ModelBuilder modelBuilder);

    }
}
