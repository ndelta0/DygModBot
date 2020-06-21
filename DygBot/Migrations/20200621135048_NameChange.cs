using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DygBot.Migrations
{
    public partial class NameChange : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HourlyStats");

            migrationBuilder.CreateTable(
                name: "DetailStat",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    DateTime = table.Column<DateTime>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    Members = table.Column<int>(nullable: false),
                    Online = table.Column<int>(nullable: false),
                    Bans = table.Column<int>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DetailStat", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DetailStat");

            migrationBuilder.CreateTable(
                name: "HourlyStats",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Bans = table.Column<int>(type: "int", nullable: false),
                    DateTime = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    GuildId = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Members = table.Column<int>(type: "int", nullable: false),
                    Online = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HourlyStats", x => x.Id);
                });
        }
    }
}
