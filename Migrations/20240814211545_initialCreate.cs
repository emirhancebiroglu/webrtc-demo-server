using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebRTCWebSocketServer.Migrations
{
    /// <inheritdoc />
    public partial class initialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CallRecordings",
                columns: table => new
                {
                    CallId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CallRecordings", x => x.CallId);
                });

            migrationBuilder.CreateTable(
                name: "RecordingFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FilePath = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CallId = table.Column<string>(type: "nvarchar(450)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecordingFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecordingFiles_CallRecordings_CallId",
                        column: x => x.CallId,
                        principalTable: "CallRecordings",
                        principalColumn: "CallId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RecordingFiles_CallId",
                table: "RecordingFiles",
                column: "CallId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RecordingFiles");

            migrationBuilder.DropTable(
                name: "CallRecordings");
        }
    }
}
