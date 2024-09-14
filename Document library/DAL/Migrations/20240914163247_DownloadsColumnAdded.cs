using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Document_library.DAL.Migrations
{
    /// <inheritdoc />
    public partial class DownloadsColumnAdded : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Downloads",
                table: "Documents",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Downloads",
                table: "Documents");
        }
    }
}
