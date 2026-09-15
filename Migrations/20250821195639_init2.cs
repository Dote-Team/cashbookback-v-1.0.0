using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cashbook.Migrations
{
    /// <inheritdoc />
    public partial class init2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionHistories_Transactions_TransactionId",
                table: "TransactionHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionHistories_Transactions_TransactionId",
                table: "TransactionHistories",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TransactionHistories_Transactions_TransactionId",
                table: "TransactionHistories");

            migrationBuilder.AddForeignKey(
                name: "FK_TransactionHistories_Transactions_TransactionId",
                table: "TransactionHistories",
                column: "TransactionId",
                principalTable: "Transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
