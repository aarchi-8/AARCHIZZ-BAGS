using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AARCHIZZ.Data.Migrations;

public partial class OwnerAccountFoundation : Migration
{
  protected override void Up(MigrationBuilder migrationBuilder)
  {
    migrationBuilder.CreateTable(
      name: "OwnerAccounts",
      columns: table => new
      {
        Id = table.Column<int>(type: "int", nullable: false).Annotation("SqlServer:Identity", "1, 1"),
        Username = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
        NormalizedUsername = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
        PasswordHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
        SecurityStamp = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
        SingletonMarker = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
        CreatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
        UpdatedUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
        LastLoginUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
      },
      constraints: table =>
      {
        table.PrimaryKey("PK_OwnerAccounts", x => x.Id);
        table.CheckConstraint("CK_OwnerAccounts_SingletonMarker", "[SingletonMarker] = 'OWNER'");
      });
    migrationBuilder.CreateIndex(name: "IX_OwnerAccounts_NormalizedUsername", table: "OwnerAccounts", column: "NormalizedUsername", unique: true);
    migrationBuilder.CreateIndex(name: "IX_OwnerAccounts_SingletonMarker", table: "OwnerAccounts", column: "SingletonMarker", unique: true);
  }

  protected override void Down(MigrationBuilder migrationBuilder) => migrationBuilder.DropTable(name: "OwnerAccounts");
}
