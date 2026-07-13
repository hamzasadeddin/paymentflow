using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PaymentFlow.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase06ComplianceReconciliation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComplianceCases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    RaisedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComplianceCases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationRuns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    StatementDateUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RunByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    MatchedCount = table.Column<int>(type: "int", nullable: false),
                    BreakCount = table.Column<int>(type: "int", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationBreaks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RunId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    PaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PaymentReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    StatementReference = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    LedgerAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    StatementAmount = table.Column<decimal>(type: "decimal(19,4)", nullable: true),
                    Currency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ResolvedByUserId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ResolvedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationBreaks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReconciliationBreaks_ReconciliationRuns_RunId",
                        column: x => x.RunId,
                        principalTable: "ReconciliationRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_PaymentId",
                table: "ComplianceCases",
                column: "PaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_ComplianceCases_Status",
                table: "ComplianceCases",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBreaks_RunId_Status",
                table: "ReconciliationBreaks",
                columns: new[] { "RunId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRuns_CreatedAtUtc",
                table: "ReconciliationRuns",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationRuns_RunReference",
                table: "ReconciliationRuns",
                column: "RunReference",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComplianceCases");

            migrationBuilder.DropTable(
                name: "ReconciliationBreaks");

            migrationBuilder.DropTable(
                name: "ReconciliationRuns");
        }
    }
}
