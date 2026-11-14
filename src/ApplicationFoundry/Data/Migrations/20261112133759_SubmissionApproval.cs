using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationFoundry.Data.Migrations
{
    /// <inheritdoc />
    public partial class SubmissionApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SubmissionApprovalFingerprint",
                table: "ApplicationFormPlans",
                type: "TEXT",
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "SubmissionApprovedAt",
                table: "ApplicationFormPlans",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SubmissionApprovalFingerprint",
                table: "ApplicationFormPlans");

            migrationBuilder.DropColumn(
                name: "SubmissionApprovedAt",
                table: "ApplicationFormPlans");
        }
    }
}
