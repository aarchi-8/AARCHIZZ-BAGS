using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AARCHIZZ.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductStyleFinishAndModelCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Finish",
                table: "Products",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Pattern",
                table: "Products",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StyleCode",
                table: "Products",
                type: "nvarchar(120)",
                maxLength: 120,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Finish",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "Pattern",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "StyleCode",
                table: "Products");
        }
    }
}
