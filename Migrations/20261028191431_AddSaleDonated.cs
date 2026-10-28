using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValoresReceitas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleDonated : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Donated",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Donated",
                table: "Sales");
        }
    }
}
