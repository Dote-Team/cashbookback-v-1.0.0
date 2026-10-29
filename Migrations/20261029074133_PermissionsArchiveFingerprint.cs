using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cashbook.Migrations
{
    /// <inheritdoc />
    public partial class PermissionsArchiveFingerprint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DeactivatedByUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<Guid>(
                name: "ParentUserId",
                table: "Users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Transactions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Transactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "Transactions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Transactions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "PaymentMethods",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "PaymentMethods",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "PaymentMethods",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "PaymentMethods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "CustomFields",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "CustomFields",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "CustomFields",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "CustomFields",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Contacts",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Contacts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "Contacts",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Contacts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Categories",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Categories",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "Categories",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Categories",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Businesses",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Businesses",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "Businesses",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Businesses",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Books",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Books",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "Books",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Books",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "ArchiveReason",
                table: "Attachements",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ArchivedAt",
                table: "Attachements",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ArchivedByUserId",
                table: "Attachements",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "Attachements",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "UserFingerprints",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FingerIndex = table.Column<int>(type: "int", nullable: false),
                    TemplateEncrypted = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    Algorithm = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserFingerprints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserFingerprints_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserWalletPermissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BookId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Permission = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GrantedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    GrantedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserWalletPermissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserWalletPermissions_Books_BookId",
                        column: x => x.BookId,
                        principalTable: "Books",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserWalletPermissions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ParentUserId",
                table: "Users",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Transactions_Book_Running",
                table: "Transactions",
                columns: new[] { "BookId", "IsArchived", "Date" })
                .Annotation("SqlServer:Include", new[] { "Amount", "Currency", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_UserFingerprints_UserId_IsActive",
                table: "UserFingerprints",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserWalletPermissions_BookId",
                table: "UserWalletPermissions",
                column: "BookId");

            migrationBuilder.CreateIndex(
                name: "IX_UserWalletPermissions_UserId_BookId_Permission",
                table: "UserWalletPermissions",
                columns: new[] { "UserId", "BookId", "Permission" },
                unique: true);

            migrationBuilder.Sql(ConvertRolesToPermissionsSql);
        }

        /// <summary>
        /// ترحيل الأدوار القديمة إلى صلاحيات لكل خزنة، في دفعة واحدة:
        ///   owner / partner         ⟵ أدمن بكل الصلاحيات على كل خزائن المنشأة
        ///   viewer                  ⟵ مستخدم: عرض على كل خزائن المنشأة
        ///   admin / staff / dataoperator ⟵ مستخدم: عرض وإضافة ونسخ وحقول على خزائنه (BookIds)
        ///   privateviewer           ⟵ مستخدم: عرض ونسخ على خزائنه
        ///   portfolio_manager       ⟵ مستخدم: عرض ونسخ وسعر الصرف على خزائنه
        /// IsSuperAdmin = 1 ⟵ سوبر أدمن مخفي. والمستخدم يُنسب إلى مالك منشأته ليديره.
        /// </summary>
        private const string ConvertRolesToPermissionsSql = @"
DECLARE @map TABLE (Role nvarchar(50), Permission nvarchar(64), AllBooks bit);
INSERT INTO @map (Role, Permission, AllBooks)
SELECT r.Role, p.Permission, 1
FROM (VALUES ('owner'), ('partner')) r(Role)
CROSS JOIN (VALUES ('book.view'), ('book.edit'), ('book.archive'), ('transaction.view'), ('transaction.create'),
    ('transaction.edit'), ('transaction.archive'), ('transaction.duplicate'), ('transaction.export'), ('history.view'),
    ('category.manage'), ('contact.manage'), ('payment_method.manage'), ('custom_field.manage'),
    ('exchange_rate.manage'), ('backup.import'), ('archive.view'), ('users.manage')) p(Permission);
INSERT INTO @map (Role, Permission, AllBooks) VALUES
    ('viewer', 'book.view', 1), ('viewer', 'transaction.view', 1), ('viewer', 'history.view', 1),
    ('viewer', 'transaction.export', 1), ('viewer', 'archive.view', 1);
INSERT INTO @map (Role, Permission, AllBooks)
SELECT r.Role, p.Permission, 0
FROM (VALUES ('admin'), ('staff'), ('dataoperator')) r(Role)
CROSS JOIN (VALUES ('book.view'), ('transaction.view'), ('transaction.create'), ('transaction.duplicate'), ('custom_field.manage')) p(Permission);
INSERT INTO @map (Role, Permission, AllBooks) VALUES
    ('privateviewer', 'book.view', 0), ('privateviewer', 'transaction.view', 0), ('privateviewer', 'transaction.duplicate', 0),
    ('portfolio_manager', 'book.view', 0), ('portfolio_manager', 'transaction.duplicate', 0), ('portfolio_manager', 'exchange_rate.manage', 0);

INSERT INTO UserWalletPermissions (Id, UserId, BookId, Permission, GrantedByUserId, GrantedAt)
SELECT NEWID(), x.UserId, x.BookId, x.Permission, NULL, SYSUTCDATETIME()
FROM (
    SELECT bu.UserId, b.Id AS BookId, m.Permission
    FROM BusinessUsers bu
    JOIN @map m ON m.Role = LOWER(bu.Role) AND m.AllBooks = 1
    JOIN Books b ON b.BusinessId = bu.BusinessId
    UNION
    SELECT bu.UserId, b.Id, m.Permission
    FROM BusinessUsers bu
    JOIN @map m ON m.Role = LOWER(bu.Role) AND m.AllBooks = 0
    CROSS APPLY OPENJSON(CASE WHEN ISJSON(bu.BookIds) = 1 THEN bu.BookIds ELSE '[]' END) j
    JOIN Books b ON b.Id = TRY_CAST(j.value AS uniqueidentifier) AND b.BusinessId = bu.BusinessId
) x
WHERE NOT EXISTS (SELECT 1 FROM Users u WHERE u.Id = x.UserId AND u.IsSuperAdmin = 1);

UPDATE Users SET Level = 1, IsHidden = 1 WHERE IsSuperAdmin = 1;
UPDATE u SET Level = 2 FROM Users u
WHERE u.IsSuperAdmin = 0 AND EXISTS (SELECT 1 FROM BusinessUsers bu WHERE bu.UserId = u.Id AND LOWER(bu.Role) IN ('owner', 'partner'));

DECLARE @superAdmin uniqueidentifier = (SELECT TOP 1 Id FROM Users WHERE IsSuperAdmin = 1 ORDER BY CreatedAt);
UPDATE Users SET ParentUserId = @superAdmin WHERE Level = 2;
UPDATE u SET ParentUserId = (
    SELECT TOP 1 o.UserId
    FROM BusinessUsers mine
    JOIN BusinessUsers o ON o.BusinessId = mine.BusinessId AND LOWER(o.Role) IN ('owner', 'partner')
    WHERE mine.UserId = u.Id
    ORDER BY CASE LOWER(o.Role) WHEN 'owner' THEN 0 ELSE 1 END, o.Id)
FROM Users u WHERE u.Level = 3;

UPDATE bu SET
    Role = CASE u.Level WHEN 1 THEN 'superadmin' WHEN 2 THEN 'admin' ELSE 'user' END,
    BookIds = ISNULL((
        SELECT '[' + STRING_AGG('""' + LOWER(CAST(d.BookId AS nvarchar(36))) + '""', ',') + ']'
        FROM (SELECT DISTINCT p.BookId FROM UserWalletPermissions p JOIN Books b ON b.Id = p.BookId
              WHERE p.UserId = bu.UserId AND b.BusinessId = bu.BusinessId) d), '[]')
FROM BusinessUsers bu JOIN Users u ON u.Id = bu.UserId;
";

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserFingerprints");

            migrationBuilder.DropTable(
                name: "UserWalletPermissions");

            migrationBuilder.DropIndex(
                name: "IX_Users_ParentUserId",
                table: "Users");

            migrationBuilder.DropIndex(
                name: "IX_Transactions_Book_Running",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeactivatedByUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ParentUserId",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Transactions");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "PaymentMethods");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "CustomFields");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Contacts");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Categories");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Businesses");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Books");

            migrationBuilder.DropColumn(
                name: "ArchiveReason",
                table: "Attachements");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "Attachements");

            migrationBuilder.DropColumn(
                name: "ArchivedByUserId",
                table: "Attachements");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "Attachements");
        }
    }
}
