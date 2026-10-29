using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ValoresReceitas.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPaginationIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockLots_UserId_EntryOrder_Id",
                table: "StockLots",
                columns: new[] { "UserId", "EntryOrder", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_UserId_Date_Id",
                table: "Sales",
                columns: new[] { "UserId", "Date", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Sales_UserId_ProductionId_Date_Id",
                table: "Sales",
                columns: new[] { "UserId", "ProductionId", "Date", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Recipes_UserId_Name_Id",
                table: "Recipes",
                columns: new[] { "UserId", "Name", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Productions_UserId_Date_Id",
                table: "Productions",
                columns: new[] { "UserId", "Date", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_MarketItems_UserId_Name_Id",
                table: "MarketItems",
                columns: new[] { "UserId", "Name", "Id" },
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Ingredients_UserId_MarketItemId",
                table: "Ingredients",
                columns: new[] { "UserId", "MarketItemId" },
                filter: "[IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockLots_UserId_EntryOrder_Id",
                table: "StockLots");

            migrationBuilder.DropIndex(
                name: "IX_Sales_UserId_Date_Id",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Sales_UserId_ProductionId_Date_Id",
                table: "Sales");

            migrationBuilder.DropIndex(
                name: "IX_Recipes_UserId_Name_Id",
                table: "Recipes");

            migrationBuilder.DropIndex(
                name: "IX_Productions_UserId_Date_Id",
                table: "Productions");

            migrationBuilder.DropIndex(
                name: "IX_MarketItems_UserId_Name_Id",
                table: "MarketItems");

            migrationBuilder.DropIndex(
                name: "IX_Ingredients_UserId_MarketItemId",
                table: "Ingredients");
        }
    }
}
