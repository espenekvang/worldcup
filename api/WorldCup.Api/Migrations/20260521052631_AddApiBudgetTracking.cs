using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldCup.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddApiBudgetTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApiCallLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CalledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Endpoint = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiCallLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PendingMatchFetches",
                columns: table => new
                {
                    MatchId = table.Column<int>(type: "INTEGER", nullable: false),
                    FirstScheduledAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    NextAttemptAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    AttemptCount = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingMatchFetches", x => x.MatchId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiCallLogs_CalledAt",
                table: "ApiCallLogs",
                column: "CalledAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingMatchFetches_NextAttemptAt",
                table: "PendingMatchFetches",
                column: "NextAttemptAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiCallLogs");

            migrationBuilder.DropTable(
                name: "PendingMatchFetches");
        }
    }
}
