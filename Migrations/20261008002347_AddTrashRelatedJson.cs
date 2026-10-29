using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cashbook.Migrations
{
    /// <inheritdoc />
    public partial class AddTrashRelatedJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RelatedJson",
                table: "TrashItems",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RelatedJson",
                table: "TrashItems");
        }
    }
}
