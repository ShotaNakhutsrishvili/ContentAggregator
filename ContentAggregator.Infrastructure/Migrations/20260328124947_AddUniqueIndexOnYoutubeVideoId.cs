using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentAggregator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueIndexOnYoutubeVideoId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_YoutubeContent_VideoId",
                table: "YoutubeContent",
                column: "VideoId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_YoutubeContent_VideoId",
                table: "YoutubeContent");
        }
    }
}
