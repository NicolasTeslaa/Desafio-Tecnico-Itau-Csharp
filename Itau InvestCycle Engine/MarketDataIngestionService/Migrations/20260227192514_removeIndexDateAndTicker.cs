using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketDataIngestionService.Migrations
{
    /// <inheritdoc />
    public partial class removeIndexDateAndTicker : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_cotacoes_DataPregao_Ticker",
                table: "cotacoes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_cotacoes_DataPregao_Ticker",
                table: "cotacoes",
                columns: new[] { "DataPregao", "Ticker" },
                unique: true);
        }
    }
}
