using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WorldCup.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPredictionPoints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Points",
                table: "Predictions",
                type: "INTEGER",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Points",
                table: "Predictions");
        }
    }
}
