using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldCup.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBettingGroupInviteLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BettingGroupInviteLinks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BettingGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Token = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsRevoked = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingGroupInviteLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BettingGroupInviteLinks_BettingGroups_BettingGroupId",
                        column: x => x.BettingGroupId,
                        principalTable: "BettingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BettingGroupInviteLinks_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BettingGroupInviteLinks_BettingGroupId",
                table: "BettingGroupInviteLinks",
                column: "BettingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_BettingGroupInviteLinks_CreatedByUserId",
                table: "BettingGroupInviteLinks",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_BettingGroupInviteLinks_Token",
                table: "BettingGroupInviteLinks",
                column: "Token",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BettingGroupInviteLinks");
        }
    }
}
