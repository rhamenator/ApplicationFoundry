using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationFoundry.Data.Migrations;

public partial class LegacyProfileImportBatch : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "LegacyProfileImportBatches",
            columns: table => new
            {
                Id = table.Column<int>(type: "INTEGER", nullable: false)
                    .Annotation("Sqlite:Autoincrement", true),
                UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                ProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                SourceSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                BeforeJson = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: false),
                AfterJson = table.Column<string>(type: "TEXT", maxLength: 30000, nullable: false),
                ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_LegacyProfileImportBatches", x => x.Id);
                table.ForeignKey(
                    name: "FK_LegacyProfileImportBatches_CandidateProfiles_ProfileId",
                    column: x => x.ProfileId,
                    principalTable: "CandidateProfiles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_LegacyProfileImportBatches_ProfileId",
            table: "LegacyProfileImportBatches",
            column: "ProfileId");
        migrationBuilder.CreateIndex(
            name: "IX_LegacyProfileImportBatches_UserId_ImportedAt",
            table: "LegacyProfileImportBatches",
            columns: new[] { "UserId", "ImportedAt" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "LegacyProfileImportBatches");
    }
}
