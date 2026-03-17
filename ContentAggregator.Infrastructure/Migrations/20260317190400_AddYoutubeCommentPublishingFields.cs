using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ContentAggregator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddYoutubeCommentPublishingFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastProcessingError",
                table: "YoutubeContent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "YoutubeCommentId",
                table: "YoutubeContent",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "YoutubeCommentPosted",
                table: "YoutubeContent",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "YoutubeCommentPostedAt",
                table: "YoutubeContent",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastProcessingError",
                table: "YoutubeContent");

            migrationBuilder.DropColumn(
                name: "YoutubeCommentId",
                table: "YoutubeContent");

            migrationBuilder.DropColumn(
                name: "YoutubeCommentPosted",
                table: "YoutubeContent");

            migrationBuilder.DropColumn(
                name: "YoutubeCommentPostedAt",
                table: "YoutubeContent");
        }
    }
}
