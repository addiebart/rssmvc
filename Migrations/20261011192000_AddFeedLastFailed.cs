using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RssAsp.Migrations
{
    public partial class AddFeedLastFailed : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastFailed",
                table: "Feeds",
                type: "TEXT",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastFailed",
                table: "Feeds");
        }
    }
}
