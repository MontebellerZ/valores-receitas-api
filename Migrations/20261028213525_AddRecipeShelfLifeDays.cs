using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValoresReceitas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddRecipeShelfLifeDays : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ShelfLifeDays",
                table: "Recipes",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShelfLifeDays",
                table: "Recipes");
        }
    }
}
