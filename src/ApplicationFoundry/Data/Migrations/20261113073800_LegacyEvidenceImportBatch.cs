using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationFoundry.Data.Migrations;

public partial class LegacyEvidenceImportBatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LegacyEvidenceImportBatches",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SourceSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                ClaimCount = table.Column<int>(type: "INTEGER", nullable: false),
                ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LegacyEvidenceImportBatches", x => x.Id);
                table.ForeignKey(
                    name: "FK_LegacyEvidenceImportBatches_CandidateProfiles_ProfileId",
                    column: x => x.ProfileId,
                    principalTable: "CandidateProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.AddColumn<int>(
            name: "LegacyImportBatchId",
            table: "EvidenceItems",
            type: "INTEGER",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_LegacyEvidenceImportBatches_ProfileId",
            table: "LegacyEvidenceImportBatches",
            column: "ProfileId");
        migrationBuilder.CreateIndex(
            name: "IX_LegacyEvidenceImportBatches_UserId_ImportedAt",
            table: "LegacyEvidenceImportBatches",
            columns: new[] { "UserId", "ImportedAt" });
        migrationBuilder.CreateIndex(
            name: "IX_EvidenceItems_LegacyImportBatchId",
            table: "EvidenceItems",
            column: "LegacyImportBatchId");
        migrationBuilder.AddForeignKey(
            name: "FK_EvidenceItems_LegacyEvidenceImportBatches_LegacyImportBatchId",
            table: "EvidenceItems",
            column: "LegacyImportBatchId",
            principalTable: "LegacyEvidenceImportBatches",
            principalColumn: "Id",
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_EvidenceItems_LegacyEvidenceImportBatches_LegacyImportBatchId",
            table: "EvidenceItems");
        migrationBuilder.DropIndex(
            name: "IX_EvidenceItems_LegacyImportBatchId",
            table: "EvidenceItems");
        migrationBuilder.DropColumn(name: "LegacyImportBatchId", table: "EvidenceItems");
        migrationBuilder.DropTable(name: "LegacyEvidenceImportBatches");
    }
}
