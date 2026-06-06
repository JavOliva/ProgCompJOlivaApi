using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProgCompJOlivaApi.Migrations
{
    /// <inheritdoc />
    public partial class AddCodeforcesGyms : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "codeforces_gyms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GymContestId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    FetchMethod = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Enabled = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_codeforces_gyms", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_codeforces_gyms_GymContestId",
                table: "codeforces_gyms",
                column: "GymContestId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "codeforces_gyms");
        }
    }
}
