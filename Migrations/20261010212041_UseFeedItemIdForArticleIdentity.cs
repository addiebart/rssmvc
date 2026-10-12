using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RssAsp.Migrations
{
    /// <inheritdoc />
    public partial class UseFeedItemIdForArticleIdentity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_FeedId_Url",
                table: "Articles");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Articles",
                type: "TEXT",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT");

            migrationBuilder.CreateIndex(
                name: "IX_Articles_FeedId_FeedItemId",
                table: "Articles",
                columns: new[] { "FeedId", "FeedItemId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Articles_FeedId_FeedItemId",
                table: "Articles");

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Articles",
                type: "TEXT",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Articles_FeedId_Url",
                table: "Articles",
                columns: new[] { "FeedId", "Url" },
                unique: true);
        }
    }
}
