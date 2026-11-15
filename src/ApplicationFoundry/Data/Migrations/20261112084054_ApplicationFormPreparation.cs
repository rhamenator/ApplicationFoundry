using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationFoundry.Data.Migrations
{
    /// <inheritdoc />
    public partial class ApplicationFormPreparation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApplicationFormPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    JobOpportunityId = table.Column<int>(type: "INTEGER", nullable: false),
                    Mode = table.Column<int>(type: "INTEGER", nullable: false),
                    Rules = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFormPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationFormPlans_JobOpportunities_JobOpportunityId",
                        column: x => x.JobOpportunityId,
                        principalTable: "JobOpportunities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApplicationFormAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ApplicationFormPlanId = table.Column<int>(type: "INTEGER", nullable: false),
                    FieldLabel = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Answer = table.Column<string>(type: "TEXT", maxLength: 4000, nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApplicationFormAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ApplicationFormAnswers_ApplicationFormPlans_ApplicationFormPlanId",
                        column: x => x.ApplicationFormPlanId,
                        principalTable: "ApplicationFormPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFormAnswers_ApplicationFormPlanId",
                table: "ApplicationFormAnswers",
                column: "ApplicationFormPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_ApplicationFormPlans_JobOpportunityId",
                table: "ApplicationFormPlans",
                column: "JobOpportunityId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApplicationFormAnswers");

            migrationBuilder.DropTable(
                name: "ApplicationFormPlans");
        }
    }
}
