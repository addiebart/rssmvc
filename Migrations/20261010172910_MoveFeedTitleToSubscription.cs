using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RssAsp.Migrations
{
    /// <inheritdoc />
    public partial class MoveFeedTitleToSubscription : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayTitle",
                table: "Subscriptions",
                type: "TEXT",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE Subscriptions
                SET DisplayTitle = (
                    SELECT Title
                    FROM Feeds
                    WHERE Feeds.Id = Subscriptions.FeedId
                );
                """);

            migrationBuilder.DropColumn(
                name: "Title",
                table: "Feeds");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "Feeds",
                type: "TEXT",
                maxLength: 250,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql("""
                UPDATE Feeds
                SET Title = (
                    SELECT DisplayTitle
                    FROM Subscriptions
                    WHERE Subscriptions.FeedId = Feeds.Id
                    LIMIT 1
                );
                """);

            migrationBuilder.DropColumn(
                name: "DisplayTitle",
                table: "Subscriptions");
        }
    }
}
