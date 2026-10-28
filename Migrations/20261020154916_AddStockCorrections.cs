using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValoresReceitas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddStockCorrections : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "EntryDate",
                table: "StockConsumptions",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackagePrice",
                table: "StockConsumptions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PackageQty",
                table: "StockConsumptions",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrectedData",
                table: "StockAdjustments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreviousData",
                table: "StockAdjustments",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [StockConsumptions]
                SET [EntryDate] = (SELECT [EntryDate] FROM [StockLots] WHERE [Id] = [StockConsumptions].[StockLotId]),
                    [PackageQty] = (SELECT [PackageQty] FROM [StockLots] WHERE [Id] = [StockConsumptions].[StockLotId]),
                    [PackagePrice] = (SELECT [PackagePrice] FROM [StockLots] WHERE [Id] = [StockConsumptions].[StockLotId]);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EntryDate",
                table: "StockConsumptions");

            migrationBuilder.DropColumn(
                name: "PackagePrice",
                table: "StockConsumptions");

            migrationBuilder.DropColumn(
                name: "PackageQty",
                table: "StockConsumptions");

            migrationBuilder.DropColumn(
                name: "CorrectedData",
                table: "StockAdjustments");

            migrationBuilder.DropColumn(
                name: "PreviousData",
                table: "StockAdjustments");
        }
    }
}
