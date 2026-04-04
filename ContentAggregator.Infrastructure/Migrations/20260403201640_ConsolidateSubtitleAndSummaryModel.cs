using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentAggregator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ConsolidateSubtitleAndSummaryModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "SubtitlesEngSRT",
                table: "YoutubeContent",
                newName: "SubtitlesOrigSRT");

            migrationBuilder.RenameColumn(
                name: "VideoSummaryGeo",
                table: "YoutubeContent",
                newName: "VideoSummary");

            migrationBuilder.AddColumn<byte>(
                name: "SubtitleLanguage",
                table: "YoutubeContent",
                type: "smallint",
                nullable: false,
                defaultValue: (byte)0);

            migrationBuilder.AddColumn<string>(
                name: "YoutubeCommentText",
                table: "YoutubeContent",
                type: "text",
                nullable: true);

            migrationBuilder.Sql(
                """
                UPDATE "YoutubeContent"
                SET "VideoSummary" = "VideoSummaryEng"
                WHERE "VideoSummary" IS NULL AND "VideoSummaryEng" IS NOT NULL;
                """);

            migrationBuilder.Sql(
                """
                UPDATE "YoutubeContent"
                SET "YoutubeCommentText" = CASE
                    WHEN "VideoSummary" IS NULL THEN NULL
                    ELSE '00:00 - ' || "VideoSummary"
                END
                WHERE "YoutubeCommentText" IS NULL;
                """);

            migrationBuilder.DropColumn(
                name: "VideoSummaryEng",
                table: "YoutubeContent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "VideoSummaryEng",
                table: "YoutubeContent",
                type: "text",
                nullable: true);

            migrationBuilder.DropColumn(
                name: "SubtitleLanguage",
                table: "YoutubeContent");

            migrationBuilder.DropColumn(
                name: "YoutubeCommentText",
                table: "YoutubeContent");

            migrationBuilder.Sql(
                """
                UPDATE "YoutubeContent"
                SET "VideoSummaryEng" = "VideoSummary"
                WHERE "VideoSummaryEng" IS NULL AND "VideoSummary" IS NOT NULL;
                """);

            migrationBuilder.RenameColumn(
                name: "VideoSummary",
                table: "YoutubeContent",
                newName: "VideoSummaryGeo");

            migrationBuilder.RenameColumn(
                name: "SubtitlesOrigSRT",
                table: "YoutubeContent",
                newName: "SubtitlesEngSRT");
        }
    }
}
