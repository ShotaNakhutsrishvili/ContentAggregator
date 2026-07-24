using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ContentAggregator.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStructuredContentRevisions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "YoutubeContentRevision",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YoutubeContentId = table.Column<int>(type: "integer", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Language = table.Column<byte>(type: "smallint", nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: false),
                    TranscriptChecksum = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GeneratorModel = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PromptVersion = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    ReviewState = table.Column<byte>(type: "smallint", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    ReviewedBy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeContentRevision", x => x.Id);
                    table.UniqueConstraint("AK_YoutubeContentRevision_YoutubeContentId_Id", x => new { x.YoutubeContentId, x.Id });
                    table.CheckConstraint("CK_YoutubeContentRevision_ApprovedReview", "\"ReviewState\" <> 3 OR (\"ReviewedAt\" IS NOT NULL AND \"ReviewedBy\" IS NOT NULL AND length(btrim(\"ReviewedBy\")) > 0)");
                    table.CheckConstraint("CK_YoutubeContentRevision_ReviewState", "\"ReviewState\" BETWEEN 0 AND 4");
                    table.CheckConstraint("CK_YoutubeContentRevision_TranscriptChecksum", "length(btrim(\"TranscriptChecksum\")) > 0");
                    table.CheckConstraint("CK_YoutubeContentRevision_Version", "\"Version\" > 0");
                    table.ForeignKey(
                        name: "FK_YoutubeContentRevision_YoutubeContent_YoutubeContentId",
                        column: x => x.YoutubeContentId,
                        principalTable: "YoutubeContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeContentPublication",
                columns: table => new
                {
                    YoutubeContentId = table.Column<int>(type: "integer", nullable: false),
                    PublishedRevisionId = table.Column<int>(type: "integer", nullable: true),
                    State = table.Column<byte>(type: "smallint", nullable: false),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawnAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    WithdrawalReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeContentPublication", x => x.YoutubeContentId);
                    table.CheckConstraint("CK_YoutubeContentPublication_State", "(\"State\" = 0 AND \"PublishedRevisionId\" IS NULL AND \"PublishedAt\" IS NULL AND \"WithdrawnAt\" IS NULL AND \"WithdrawalReason\" IS NULL) OR (\"State\" = 1 AND \"PublishedRevisionId\" IS NOT NULL AND \"PublishedAt\" IS NOT NULL AND \"WithdrawnAt\" IS NULL AND \"WithdrawalReason\" IS NULL) OR (\"State\" = 2 AND \"PublishedRevisionId\" IS NOT NULL AND \"PublishedAt\" IS NOT NULL AND \"WithdrawnAt\" IS NOT NULL AND \"WithdrawalReason\" IS NOT NULL AND length(btrim(\"WithdrawalReason\")) > 0)");
                    table.ForeignKey(
                        name: "FK_YoutubeContentPublication_YoutubeContentRevision_YoutubeCon~",
                        columns: x => new { x.YoutubeContentId, x.PublishedRevisionId },
                        principalTable: "YoutubeContentRevision",
                        principalColumns: new[] { "YoutubeContentId", "Id" });
                    table.ForeignKey(
                        name: "FK_YoutubeContentPublication_YoutubeContent_YoutubeContentId",
                        column: x => x.YoutubeContentId,
                        principalTable: "YoutubeContent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "YoutubeContentSection",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    YoutubeContentRevisionId = table.Column<int>(type: "integer", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    StartSeconds = table.Column<int>(type: "integer", nullable: false),
                    EndSeconds = table.Column<int>(type: "integer", nullable: true),
                    Heading = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Summary = table.Column<string>(type: "text", nullable: true),
                    Source = table.Column<byte>(type: "smallint", nullable: false),
                    Confidence = table.Column<decimal>(type: "numeric(5,4)", precision: 5, scale: 4, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_YoutubeContentSection", x => x.Id);
                    table.CheckConstraint("CK_YoutubeContentSection_Confidence", "\"Confidence\" IS NULL OR \"Confidence\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_YoutubeContentSection_EndSeconds", "\"EndSeconds\" IS NULL OR \"EndSeconds\" > \"StartSeconds\"");
                    table.CheckConstraint("CK_YoutubeContentSection_Heading", "length(btrim(\"Heading\")) > 0");
                    table.CheckConstraint("CK_YoutubeContentSection_Ordinal", "\"Ordinal\" >= 0");
                    table.CheckConstraint("CK_YoutubeContentSection_Source", "\"Source\" BETWEEN 0 AND 1");
                    table.CheckConstraint("CK_YoutubeContentSection_StartSeconds", "\"StartSeconds\" >= 0");
                    table.ForeignKey(
                        name: "FK_YoutubeContentSection_YoutubeContentRevision_YoutubeContent~",
                        column: x => x.YoutubeContentRevisionId,
                        principalTable: "YoutubeContentRevision",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeContentPublication_YoutubeContentId_PublishedRevisio~",
                table: "YoutubeContentPublication",
                columns: new[] { "YoutubeContentId", "PublishedRevisionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeContentRevision_YoutubeContentId_Version",
                table: "YoutubeContentRevision",
                columns: new[] { "YoutubeContentId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_YoutubeContentSection_YoutubeContentRevisionId_Ordinal",
                table: "YoutubeContentSection",
                columns: new[] { "YoutubeContentRevisionId", "Ordinal" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "YoutubeContentPublication");

            migrationBuilder.DropTable(
                name: "YoutubeContentSection");

            migrationBuilder.DropTable(
                name: "YoutubeContentRevision");
        }
    }
}
