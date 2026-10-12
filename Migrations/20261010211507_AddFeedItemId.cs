using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RssAsp.Migrations
{
    /// <inheritdoc />
    public partial class AddFeedItemId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FeedItemId",
                table: "Articles",
                type: "TEXT",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FeedItemId",
                table: "Articles");
        }
    }
}
