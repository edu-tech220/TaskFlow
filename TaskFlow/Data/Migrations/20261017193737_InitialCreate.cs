using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Activities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MonthlyRestLimits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Year = table.Column<int>(type: "int", nullable: false),
                    Month = table.Column<int>(type: "int", nullable: false),
                    MaxRestDays = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MonthlyRestLimits", x => x.Id);
                    table.CheckConstraint("CK_MonthlyRestLimits_MaxRestDays", "[MaxRestDays] BETWEEN 0 AND 10");
                    table.CheckConstraint("CK_MonthlyRestLimits_Month", "[Month] BETWEEN 1 AND 12");
                });

            migrationBuilder.CreateTable(
                name: "RestDays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RestDays", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ActivitySchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    StartTime = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: false),
                    EndTime = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: false),
                    EffectiveFrom = table.Column<DateOnly>(type: "date", nullable: false),
                    EffectiveTo = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivitySchedules", x => x.Id);
                    table.CheckConstraint("CK_ActivitySchedules_EffectiveTo_After_EffectiveFrom", "[EffectiveTo] IS NULL OR [EffectiveTo] >= [EffectiveFrom]");
                    table.CheckConstraint("CK_ActivitySchedules_EndTime_After_StartTime", "[EndTime] > [StartTime]");
                    table.ForeignKey(
                        name: "FK_ActivitySchedules_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DailyRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ActivityId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ActualEndTime = table.Column<TimeOnly>(type: "time(0)", precision: 0, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DailyRecords", x => x.Id);
                    table.CheckConstraint("CK_DailyRecords_ActualEndTime_Only_When_Completed", "[ActualEndTime] IS NULL OR [Status] = 1");
                    table.CheckConstraint("CK_DailyRecords_Status", "[Status] IN (1, 2)");
                    table.ForeignKey(
                        name: "FK_DailyRecords_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ActivityScheduleDays",
                columns: table => new
                {
                    ActivityScheduleId = table.Column<int>(type: "int", nullable: false),
                    DayOfWeek = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityScheduleDays", x => new { x.ActivityScheduleId, x.DayOfWeek });
                    table.CheckConstraint("CK_ActivityScheduleDays_DayOfWeek", "[DayOfWeek] BETWEEN 0 AND 6");
                    table.ForeignKey(
                        name: "FK_ActivityScheduleDays_ActivitySchedules_ActivityScheduleId",
                        column: x => x.ActivityScheduleId,
                        principalTable: "ActivitySchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivitySchedules_ActivityId_EffectiveFrom",
                table: "ActivitySchedules",
                columns: new[] { "ActivityId", "EffectiveFrom" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyRecords_ActivityId_Date",
                table: "DailyRecords",
                columns: new[] { "ActivityId", "Date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DailyRecords_Date",
                table: "DailyRecords",
                column: "Date");

            migrationBuilder.CreateIndex(
                name: "IX_MonthlyRestLimits_Year_Month",
                table: "MonthlyRestLimits",
                columns: new[] { "Year", "Month" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RestDays_Date",
                table: "RestDays",
                column: "Date",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityScheduleDays");

            migrationBuilder.DropTable(
                name: "DailyRecords");

            migrationBuilder.DropTable(
                name: "MonthlyRestLimits");

            migrationBuilder.DropTable(
                name: "RestDays");

            migrationBuilder.DropTable(
                name: "ActivitySchedules");

            migrationBuilder.DropTable(
                name: "Activities");
        }
    }
}
