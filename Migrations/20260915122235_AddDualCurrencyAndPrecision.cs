using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cashbook.Migrations
{
    /// <inheritdoc />
    public partial class AddDualCurrencyAndPrecision : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Transactions_BookId",
                table: "Transactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Transactions",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "Transactions",
                type: "nvarchar(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "IQD");

            migrationBuilder.AlterColumn<decimal>(
                name: "To",
                table: "TransactionHistories",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "From",
                table: "TransactionHistories",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "TransactionHistories",
                type: "decimal(18,3)",
                precision: 18,
                scale: 3,
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BookId_Currency",
                table: "Transactions",
                columns: new[] { "BookId", "Currency" });

            // ===== ترحيل البيانات =====
            // 1) توحيد نوع الحركة إلى القيمة المعيارية (cash in / cash out) للقيم المعروفة فقط.
            //    لا يتغيّر أي سلوك لأن كل الاستعلامات القديمة كانت تقارن النوع بشكل غير حساس لحالة الأحرف.
            migrationBuilder.Sql(@"
                UPDATE [Transactions]
                SET [Type] = LOWER(LTRIM(RTRIM([Type])))
                WHERE LOWER(LTRIM(RTRIM([Type]))) IN ('cash in', 'cash out');");

            migrationBuilder.Sql(@"
                UPDATE [TransactionHistories]
                SET [Type] = LOWER(LTRIM(RTRIM([Type])))
                WHERE LOWER(LTRIM(RTRIM([Type]))) IN ('cash in', 'cash out');");

            // 2) ضمان عدم إدخال عملة غير مدعومة على مستوى قاعدة البيانات.
            migrationBuilder.Sql(@"
                ALTER TABLE [Transactions] ADD CONSTRAINT [CK_Transactions_Currency]
                CHECK ([Currency] IN ('USD', 'IQD'));");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE [Transactions] DROP CONSTRAINT [CK_Transactions_Currency];");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_BookId_Currency",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "Transactions");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "Transactions",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.AlterColumn<decimal>(
                name: "To",
                table: "TransactionHistories",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "From",
                table: "TransactionHistories",
                type: "decimal(18,2)",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "TransactionHistories",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,3)",
                oldPrecision: 18,
                oldScale: 3);

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_BookId",
                table: "Transactions",
                column: "BookId");
        }
    }
}
