using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ApplicationFoundry.Data.Migrations
{
    /// <inheritdoc />
    public partial class FollowUpScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "NextFollowUpOn",
                table: "JobOpportunities",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NextFollowUpOn",
                table: "JobOpportunities");
        }
    }
}
