using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cashbook.Migrations
{
    /// <inheritdoc />
    public partial class AddUsernameToUsers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            // ===== ترحيل البيانات =====
            // 1) توليد اسم مستخدم لكل حساب قائم من الجزء الواقع قبل @ في البريد الإلكتروني.
            migrationBuilder.Sql(@"
                UPDATE [Users]
                SET [Username] = LEFT([Email], CHARINDEX('@', [Email] + '@') - 1)
                WHERE [Username] IS NULL OR LTRIM(RTRIM([Username])) = '';");

            // 2) معالجة الأسماء الفارغة أو المتكررة حتى ينجح الفهرس الفريد
            //    (المكرر يأخذ لاحقة من معرّفه، والفارغ يأخذ معرّفه الكامل).
            migrationBuilder.Sql(@"
                ;WITH dedup AS (
                    SELECT [Id],
                           [Username],
                           ROW_NUMBER() OVER (PARTITION BY LOWER([Username]) ORDER BY [CreatedAt], [Id]) AS rn
                    FROM [Users]
                )
                UPDATE u
                SET [Username] = CASE
                        WHEN LTRIM(RTRIM(dedup.[Username])) = ''
                            THEN 'user_' + LEFT(REPLACE(CONVERT(nvarchar(36), u.[Id]), '-', ''), 12)
                        WHEN dedup.rn = 1
                            THEN dedup.[Username]
                        ELSE dedup.[Username] + '_' + LEFT(REPLACE(CONVERT(nvarchar(36), u.[Id]), '-', ''), 6)
                    END
                FROM [Users] u
                INNER JOIN dedup ON u.[Id] = dedup.[Id];");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_Email",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Users_Username",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Username",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "Email",
                table: "Users",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(256)",
                oldMaxLength: 256);
        }
    }
}
