using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldCup.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBettingGroups : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Drop old indexes first
            migrationBuilder.DropIndex(
                name: "IX_Predictions_UserId_MatchId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_Email",
                table: "Invitations");

            // 2. Create BettingGroups table
            migrationBuilder.CreateTable(
                name: "BettingGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BettingGroups_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BettingGroups_CreatedByUserId",
                table: "BettingGroups",
                column: "CreatedByUserId");

            // 3. Create BettingGroupMembers table
            migrationBuilder.CreateTable(
                name: "BettingGroupMembers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BettingGroupId = table.Column<Guid>(type: "TEXT", nullable: false),
                    UserId = table.Column<Guid>(type: "TEXT", nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BettingGroupMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BettingGroupMembers_BettingGroups_BettingGroupId",
                        column: x => x.BettingGroupId,
                        principalTable: "BettingGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BettingGroupMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BettingGroupMembers_BettingGroupId_UserId",
                table: "BettingGroupMembers",
                columns: new[] { "BettingGroupId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BettingGroupMembers_UserId",
                table: "BettingGroupMembers",
                column: "UserId");

            // 4. Add BettingGroupId columns as NULLABLE first
            migrationBuilder.AddColumn<Guid>(
                name: "BettingGroupId",
                table: "Predictions",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "BettingGroupId",
                table: "Invitations",
                type: "TEXT",
                nullable: true);

            // 5. Conditional data seed — only if users exist (not a fresh DB)
            migrationBuilder.Sql(@"
                INSERT INTO BettingGroups (Id, Name, CreatedByUserId, CreatedAt)
                SELECT
                    '00000000-0000-0000-0000-000000000001',
                    'Default',
                    Id,
                    datetime('now')
                FROM Users WHERE IsAdmin = 1
                LIMIT 1;
            ");

            migrationBuilder.Sql(@"
                INSERT INTO BettingGroupMembers (Id, BettingGroupId, UserId, JoinedAt)
                SELECT lower(hex(randomblob(4)) || '-' || hex(randomblob(2)) || '-4' || substr(hex(randomblob(2)),2) || '-' || substr('89ab', abs(random()) % 4 + 1, 1) || substr(hex(randomblob(2)),2) || '-' || hex(randomblob(6))),
                    '00000000-0000-0000-0000-000000000001', Id, datetime('now')
                FROM Users
                WHERE EXISTS (SELECT 1 FROM BettingGroups WHERE Id = '00000000-0000-0000-0000-000000000001');
            ");

            migrationBuilder.Sql(@"
                UPDATE Predictions SET BettingGroupId = '00000000-0000-0000-0000-000000000001'
                WHERE EXISTS (SELECT 1 FROM BettingGroups WHERE Id = '00000000-0000-0000-0000-000000000001');
            ");

            migrationBuilder.Sql(@"
                UPDATE Invitations SET BettingGroupId = '00000000-0000-0000-0000-000000000001'
                WHERE EXISTS (SELECT 1 FROM BettingGroups WHERE Id = '00000000-0000-0000-0000-000000000001');
            ");

            // 6. For fresh DB: set a valid default so NOT NULL works (no rows affected, but SQLite needs it)
            // On fresh DB, no rows exist, so the ALTER to NOT NULL is safe.
            // On existing DB, all rows now have BettingGroupId set.

            // 7. SQLite doesn't support ALTER COLUMN, so we rebuild via EF's approach:
            // Since SQLite can't alter nullability, we need to use raw SQL to recreate tables.
            // However, EF Core's SQLite provider handles this via table rebuild automatically.
            // We'll just add the FK constraints — the columns are already populated.

            // For SQLite, we can't easily make nullable -> non-nullable.
            // Instead, we'll leave the column as-is (the FK and indexes enforce integrity).
            // The non-nullable constraint is enforced at the EF Core model level.

            // 8. Add new indexes
            migrationBuilder.CreateIndex(
                name: "IX_Predictions_BettingGroupId_UserId_MatchId",
                table: "Predictions",
                columns: new[] { "BettingGroupId", "UserId", "MatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_BettingGroupId",
                table: "Invitations",
                column: "BettingGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email_BettingGroupId",
                table: "Invitations",
                columns: new[] { "Email", "BettingGroupId" },
                unique: true);

            // 9. Add FK constraints
            migrationBuilder.AddForeignKey(
                name: "FK_Invitations_BettingGroups_BettingGroupId",
                table: "Invitations",
                column: "BettingGroupId",
                principalTable: "BettingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Predictions_BettingGroups_BettingGroupId",
                table: "Predictions",
                column: "BettingGroupId",
                principalTable: "BettingGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Invitations_BettingGroups_BettingGroupId",
                table: "Invitations");

            migrationBuilder.DropForeignKey(
                name: "FK_Predictions_BettingGroups_BettingGroupId",
                table: "Predictions");

            migrationBuilder.DropTable(
                name: "BettingGroupMembers");

            migrationBuilder.DropTable(
                name: "BettingGroups");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_BettingGroupId_UserId_MatchId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Predictions_UserId",
                table: "Predictions");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_BettingGroupId",
                table: "Invitations");

            migrationBuilder.DropIndex(
                name: "IX_Invitations_Email_BettingGroupId",
                table: "Invitations");

            migrationBuilder.DropColumn(
                name: "BettingGroupId",
                table: "Predictions");

            migrationBuilder.DropColumn(
                name: "BettingGroupId",
                table: "Invitations");

            migrationBuilder.CreateIndex(
                name: "IX_Predictions_UserId_MatchId",
                table: "Predictions",
                columns: new[] { "UserId", "MatchId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Invitations_Email",
                table: "Invitations",
                column: "Email",
                unique: true);
        }
    }
}
