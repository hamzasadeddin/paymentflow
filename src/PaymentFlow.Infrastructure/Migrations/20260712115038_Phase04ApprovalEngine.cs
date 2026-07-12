using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase04ApprovalEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Payments",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "RequiredApprovals",
                table: "Payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "CreatedByUserId",
                table: "Beneficiaries",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ApprovalDecisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SubjectType = table.Column<int>(type: "int", nullable: false),
                    SubjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ApproverUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ApproverEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Decision = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    DecidedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApprovalDecisions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApprovalDecisions_SubjectType_SubjectId",
                table: "ApprovalDecisions",
                columns: new[] { "SubjectType", "SubjectId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalDecisions");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "RequiredApprovals",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "CreatedByUserId",
                table: "Beneficiaries");
        }
    }
}
