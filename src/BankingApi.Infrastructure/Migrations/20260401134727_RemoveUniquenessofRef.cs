using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BankingApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveUniquenessofRef : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Reference",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Reference",
                table: "Transactions",
                column: "Reference");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_Reference",
                table: "Transactions");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Reference",
                table: "Transactions",
                column: "Reference",
                unique: true);
        }
    }
}
