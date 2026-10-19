using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CodePath.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LectureDelivery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsTemplate",
                table: "classes",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "Mode",
                table: "classes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE classes c SET Mode = CASE WHEN EXISTS (SELECT 1 FROM class_teachers t WHERE t.ClassId = c.Id) THEN 0 ELSE 1 END");

            migrationBuilder.CreateTable(
                name: "learning_activity_days",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_activity_days", x => new { x.UserId, x.Date });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    OwnerId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    StorageKey = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MimeType = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PageCount = table.Column<int>(type: "int", nullable: true),
                    Width = table.Column<int>(type: "int", nullable: true),
                    Height = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_assets", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_badges",
                columns: table => new
                {
                    Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_badges", x => x.Code);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_block_progress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    BlockId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    CoveredRanges = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DurationSec = table.Column<double>(type: "double", nullable: false),
                    LastPositionSec = table.Column<double>(type: "double", nullable: true),
                    MaxPage = table.Column<int>(type: "int", nullable: true),
                    TotalPages = table.Column<int>(type: "int", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_block_progress", x => new { x.UserId, x.TopicId, x.BlockId });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_chapters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Title = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    SourceChapterId = table.Column<Guid>(type: "binary(16)", nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_chapters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_learning_chapters_classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_clone_receipts",
                columns: table => new
                {
                    TargetClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    RequestHash = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Result = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_clone_receipts", x => new { x.TargetClassId, x.Key });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ParentId = table.Column<Guid>(type: "binary(16)", nullable: true),
                    Body = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsPinned = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EditedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_comments", x => x.Id);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_extensions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    NewDueAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    Reason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    GrantedBy = table.Column<Guid>(type: "binary(16)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_extensions", x => new { x.UserId, x.TopicId });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_notes",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    BlockId = table.Column<Guid>(type: "binary(16)", nullable: true),
                    Body = table.Column<string>(type: "varchar(10000)", maxLength: 10000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_notes", x => new { x.UserId, x.TopicId });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_score_configs",
                columns: table => new
                {
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Configuration = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_score_configs", x => x.ClassId);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_scores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Source = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SourceRefId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    BasePoints = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    PenaltyPercent = table.Column<decimal>(type: "decimal(7,4)", precision: 7, scale: 4, nullable: false),
                    LateByMinutes = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: false),
                    EffectivePoints = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_scores", x => new { x.UserId, x.Source, x.SourceRefId });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_search",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Document = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_search", x => x.TopicId);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_user_badges",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    BadgeCode = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    AwardedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_user_badges", x => new { x.UserId, x.BadgeCode, x.ClassId });
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ChapterId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Title = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Slug = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Summary = table.Column<string>(type: "varchar(5000)", maxLength: 5000, nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    PublishAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UnlockRule = table.Column<int>(type: "int", nullable: false),
                    UnlockAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletionRule = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DraftContent = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishedContent = table.Column<string>(type: "json", nullable: false, collation: "utf8mb4_0900_ai_ci")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PublishedVersion = table.Column<int>(type: "int", nullable: false),
                    DueAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    SourceTopicId = table.Column<Guid>(type: "binary(16)", nullable: true),
                    SourcePublishedVersion = table.Column<int>(type: "int", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "binary(16)", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    RowVersion = table.Column<Guid>(type: "binary(16)", nullable: false),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_learning_topics_classes_ClassId",
                        column: x => x.ClassId,
                        principalTable: "classes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_learning_topics_learning_chapters_ChapterId",
                        column: x => x.ChapterId,
                        principalTable: "learning_chapters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_exercises",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ProblemId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    Points = table.Column<decimal>(type: "decimal(12,2)", precision: 12, scale: 2, nullable: false),
                    IsRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsDraft = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_exercises", x => new { x.TopicId, x.ProblemId });
                    table.ForeignKey(
                        name: "FK_learning_exercises_learning_topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "learning_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateTable(
                name: "learning_progress",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    TopicId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    ClassId = table.Column<Guid>(type: "binary(16)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedLate = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LastAccessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ExercisePassedCount = table.Column<int>(type: "int", nullable: false),
                    ExerciseTotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_learning_progress", x => new { x.UserId, x.TopicId });
                    table.ForeignKey(
                        name: "FK_learning_progress_learning_topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "learning_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4")
                .Annotation("Relational:Collation", "utf8mb4_0900_ai_ci");

            migrationBuilder.CreateIndex(
                name: "IX_learning_chapters_ClassId_Order",
                table: "learning_chapters",
                columns: new[] { "ClassId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_comments_TopicId_CreatedAt",
                table: "learning_comments",
                columns: new[] { "TopicId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_progress_TopicId",
                table: "learning_progress",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_progress_UserId_ClassId",
                table: "learning_progress",
                columns: new[] { "UserId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_scores_UserId_ClassId",
                table: "learning_scores",
                columns: new[] { "UserId", "ClassId" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_search_ClassId",
                table: "learning_search",
                column: "ClassId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_topics_ChapterId",
                table: "learning_topics",
                column: "ChapterId");

            migrationBuilder.CreateIndex(
                name: "IX_learning_topics_ClassId_ChapterId_Order",
                table: "learning_topics",
                columns: new[] { "ClassId", "ChapterId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_learning_topics_ClassId_Slug",
                table: "learning_topics",
                columns: new[] { "ClassId", "Slug" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "learning_activity_days");

            migrationBuilder.DropTable(
                name: "learning_assets");

            migrationBuilder.DropTable(
                name: "learning_badges");

            migrationBuilder.DropTable(
                name: "learning_block_progress");

            migrationBuilder.DropTable(
                name: "learning_clone_receipts");

            migrationBuilder.DropTable(
                name: "learning_comments");

            migrationBuilder.DropTable(
                name: "learning_exercises");

            migrationBuilder.DropTable(
                name: "learning_extensions");

            migrationBuilder.DropTable(
                name: "learning_notes");

            migrationBuilder.DropTable(
                name: "learning_progress");

            migrationBuilder.DropTable(
                name: "learning_score_configs");

            migrationBuilder.DropTable(
                name: "learning_scores");

            migrationBuilder.DropTable(
                name: "learning_search");

            migrationBuilder.DropTable(
                name: "learning_user_badges");

            migrationBuilder.DropTable(
                name: "learning_topics");

            migrationBuilder.DropTable(
                name: "learning_chapters");

            migrationBuilder.DropColumn(
                name: "IsTemplate",
                table: "classes");

            migrationBuilder.DropColumn(
                name: "Mode",
                table: "classes");
        }
    }
}
