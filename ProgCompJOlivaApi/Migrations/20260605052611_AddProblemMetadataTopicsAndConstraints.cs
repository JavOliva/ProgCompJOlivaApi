using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProgCompJOlivaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddProblemMetadataTopicsAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contest_problems_problems_ProblemId",
                table: "contest_problems");

            migrationBuilder.DropForeignKey(
                name: "FK_training_contests_contests_ContestId",
                table: "training_contests");

            migrationBuilder.DropIndex(
                name: "IX_user_problem_statuses_UserId",
                table: "user_problem_statuses");

            migrationBuilder.DropIndex(
                name: "IX_training_contests_TrainingId",
                table: "training_contests");

            migrationBuilder.DropIndex(
                name: "IX_contest_problems_ContestId",
                table: "contest_problems");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "trainings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                table: "trainings",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<List<string>>(
                name: "Keywords",
                table: "problems",
                type: "text[]",
                nullable: false,
                defaultValueSql: "ARRAY[]::text[]");

            migrationBuilder.AddColumn<string>(
                name: "StatementPath",
                table: "problems",
                type: "text",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "contests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.CreateTable(
                name: "topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_topics", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TrainingUser",
                columns: table => new
                {
                    TrainingsId = table.Column<Guid>(type: "uuid", nullable: false),
                    UsersId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingUser", x => new { x.TrainingsId, x.UsersId });
                    table.ForeignKey(
                        name: "FK_TrainingUser_trainings_TrainingsId",
                        column: x => x.TrainingsId,
                        principalTable: "trainings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TrainingUser_users_UsersId",
                        column: x => x.UsersId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "problem_topics",
                columns: table => new
                {
                    ProblemsId = table.Column<Guid>(type: "uuid", nullable: false),
                    TopicsId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_problem_topics", x => new { x.ProblemsId, x.TopicsId });
                    table.ForeignKey(
                        name: "FK_problem_topics_problems_ProblemsId",
                        column: x => x.ProblemsId,
                        principalTable: "problems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_problem_topics_topics_TopicsId",
                        column: x => x.TopicsId,
                        principalTable: "topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_problem_statuses_UserId_ProblemId",
                table: "user_problem_statuses",
                columns: new[] { "UserId", "ProblemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainings_Name",
                table: "trainings",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_trainings_Slug",
                table: "trainings",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_training_contests_TrainingId_ContestId",
                table: "training_contests",
                columns: new[] { "TrainingId", "ContestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_problems_Difficulty",
                table: "problems",
                column: "Difficulty");

            migrationBuilder.CreateIndex(
                name: "IX_problems_ExternalId",
                table: "problems",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_problems_Judge",
                table: "problems",
                column: "Judge");

            migrationBuilder.CreateIndex(
                name: "IX_contests_Name",
                table: "contests",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_contest_problems_ContestId_ProblemId",
                table: "contest_problems",
                columns: new[] { "ContestId", "ProblemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_problem_topics_TopicsId",
                table: "problem_topics",
                column: "TopicsId");

            migrationBuilder.CreateIndex(
                name: "IX_topics_Name",
                table: "topics",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TrainingUser_UsersId",
                table: "TrainingUser",
                column: "UsersId");

            migrationBuilder.AddForeignKey(
                name: "FK_contest_problems_problems_ProblemId",
                table: "contest_problems",
                column: "ProblemId",
                principalTable: "problems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_training_contests_contests_ContestId",
                table: "training_contests",
                column: "ContestId",
                principalTable: "contests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_contest_problems_problems_ProblemId",
                table: "contest_problems");

            migrationBuilder.DropForeignKey(
                name: "FK_training_contests_contests_ContestId",
                table: "training_contests");

            migrationBuilder.DropTable(
                name: "problem_topics");

            migrationBuilder.DropTable(
                name: "TrainingUser");

            migrationBuilder.DropTable(
                name: "topics");

            migrationBuilder.DropIndex(
                name: "IX_user_problem_statuses_UserId_ProblemId",
                table: "user_problem_statuses");

            migrationBuilder.DropIndex(
                name: "IX_trainings_Name",
                table: "trainings");

            migrationBuilder.DropIndex(
                name: "IX_trainings_Slug",
                table: "trainings");

            migrationBuilder.DropIndex(
                name: "IX_training_contests_TrainingId_ContestId",
                table: "training_contests");

            migrationBuilder.DropIndex(
                name: "IX_problems_Difficulty",
                table: "problems");

            migrationBuilder.DropIndex(
                name: "IX_problems_ExternalId",
                table: "problems");

            migrationBuilder.DropIndex(
                name: "IX_problems_Judge",
                table: "problems");

            migrationBuilder.DropIndex(
                name: "IX_contests_Name",
                table: "contests");

            migrationBuilder.DropIndex(
                name: "IX_contest_problems_ContestId_ProblemId",
                table: "contest_problems");

            migrationBuilder.DropColumn(
                name: "Slug",
                table: "trainings");

            migrationBuilder.DropColumn(
                name: "Keywords",
                table: "problems");

            migrationBuilder.DropColumn(
                name: "StatementPath",
                table: "problems");

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "trainings",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<string>(
                name: "Name",
                table: "contests",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.CreateIndex(
                name: "IX_user_problem_statuses_UserId",
                table: "user_problem_statuses",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_training_contests_TrainingId",
                table: "training_contests",
                column: "TrainingId");

            migrationBuilder.CreateIndex(
                name: "IX_contest_problems_ContestId",
                table: "contest_problems",
                column: "ContestId");

            migrationBuilder.AddForeignKey(
                name: "FK_contest_problems_problems_ProblemId",
                table: "contest_problems",
                column: "ProblemId",
                principalTable: "problems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_training_contests_contests_ContestId",
                table: "training_contests",
                column: "ContestId",
                principalTable: "contests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
