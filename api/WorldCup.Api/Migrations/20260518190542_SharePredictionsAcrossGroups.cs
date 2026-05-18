using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldCup.Api.Migrations
{
    /// <inheritdoc />
    public partial class SharePredictionsAcrossGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Bettinger gjøres globale per (UserId, MatchId) – samme tips skal gjelde i
            // alle ligaer en bruker er med i. Brukere som i dag har flere rader for samme
            // kamp (én per liga) må dedupliseres FØR vi legger på den nye unike indeksen.
            // Strategi: behold raden med nyeste UpdatedAt (Id som tie-breaker).
            migrationBuilder.Sql(@"
                DELETE FROM Predictions
                WHERE Id NOT IN (
                    SELECT Id FROM (
                        SELECT Id,
                               ROW_NUMBER() OVER (
                                   PARTITION BY UserId, MatchId
                                   ORDER BY UpdatedAt DESC, Id DESC
                               ) AS rn
                        FROM Predictions
                    )
                    WHERE rn = 1
                );
            ");

            migrationBuilder.DropForeignKey(
                name: "FK_Predictions_BettingGroups_BettingGroupId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_BettingGroupId_UserId_MatchId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "BettingGroupId",
                table: "Predictions");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId_MatchId",
                table: "Predictions",
                columns: new[] { "UserId", "MatchId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Predictions_UserId_MatchId",
                table: "Predictions");

            migrationBuilder.AddColumn<Guid>(
                name: "BettingGroupId",
                table: "Predictions",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_BettingGroupId_UserId_MatchId",
                table: "Predictions",
                columns: new[] { "BettingGroupId", "UserId", "MatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions",
                column: "UserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_BettingGroups_BettingGroupId",
                table: "Predictions",
                column: "BettingGroupId",
                principalTable: "BettingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
