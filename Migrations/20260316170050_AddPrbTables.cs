using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HealthCoverage.Migrations
{
    /// <inheritdoc />
    public partial class AddPrbTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MenuPermission",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    MenuKey = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MenuPermission", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MenuPermission_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrbImport",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    RecordCount = table.Column<int>(type: "integer", nullable: false),
                    ImportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    ImportedById = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrbImport", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrbImport_AspNetUsers_ImportedById",
                        column: x => x.ImportedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PrbRecord",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImportId = table.Column<int>(type: "integer", nullable: false),
                    RowNo = table.Column<int>(type: "integer", nullable: false),
                    ServiceDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PatientName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Hn = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    Company = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    HospitalFee = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    LifeInsurance = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    TreatmentCost = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    ColJ = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    FundAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    PaymentAmount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    Provider = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    StatusRemark = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PoliceStation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Remarks = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrbRecord", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrbRecord_PrbImport_ImportId",
                        column: x => x.ImportId,
                        principalTable: "PrbImport",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MenuPermission_UserMenu",
                table: "MenuPermission",
                columns: new[] { "UserId", "MenuKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrbImport_ImportedById",
                table: "PrbImport",
                column: "ImportedById");

            migrationBuilder.CreateIndex(
                name: "IX_PrbImport_YearMonth",
                table: "PrbImport",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PrbRecord_ImportId",
                table: "PrbRecord",
                column: "ImportId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MenuPermission");

            migrationBuilder.DropTable(
                name: "PrbRecord");

            migrationBuilder.DropTable(
                name: "PrbImport");
        }
    }
}
