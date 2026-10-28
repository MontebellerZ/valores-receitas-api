using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValoresReceitas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStandaloneProductions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsStandalone",
                table: "Productions",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsStandalone",
                table: "Productions");
        }
    }
}
