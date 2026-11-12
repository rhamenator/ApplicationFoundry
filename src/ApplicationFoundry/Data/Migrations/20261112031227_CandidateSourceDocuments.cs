using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationFoundry.Data.Migrations
{
    /// <inheritdoc />
    public partial class CandidateSourceDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateSourceDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 450, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    MediaType = table.Column<string>(type: "TEXT", maxLength: 120, nullable: false),
                    SourceSha256 = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    ExtractedText = table.Column<string>(type: "TEXT", maxLength: 200000, nullable: false),
                    ReviewNotes = table.Column<string>(type: "TEXT", maxLength: 1000, nullable: false),
                    ImportedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CandidateSourceDocuments", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CandidateSourceDocuments_UserId_ImportedAt",
                table: "CandidateSourceDocuments",
                columns: new[] { "UserId", "ImportedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateSourceDocuments");
        }
    }
}
