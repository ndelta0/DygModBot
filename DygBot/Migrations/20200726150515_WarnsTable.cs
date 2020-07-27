using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace DygBot.Migrations
{
    public partial class WarnsTable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Warns",
                columns: table => new
                {
                    Id = table.Column<int>(nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<ulong>(nullable: false),
                    GuildId = table.Column<ulong>(nullable: false),
                    WarnExpiration = table.Column<DateTime>(nullable: false),
                    Expired = table.Column<bool>(nullable: false),
                    Reason = table.Column<string>(nullable: true),
                    WhoWarned = table.Column<ulong>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warns", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Warns");
        }
    }
}
