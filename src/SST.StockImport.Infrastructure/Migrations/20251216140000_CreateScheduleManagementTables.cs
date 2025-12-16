using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SST.StockImport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CreateScheduleManagementTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 創建 schedule_execution 表
            migrationBuilder.CreateTable(
                name: "schedule_execution",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    execution_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    schedule_slot = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    task_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    task_chain = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    end_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    result_message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_message = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    success_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    fail_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    duration_seconds = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_execution", x => x.id);
                    table.UniqueConstraint("UK_execution", x => new { x.execution_date, x.schedule_slot });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // 創建 schedule_execution_log 表
            migrationBuilder.CreateTable(
                name: "schedule_execution_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    execution_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    schedule_slot = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    task_chain = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    operation = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    operation_time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    details = table.Column<string>(type: "json", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_schedule_execution_log", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            // 創建索引
            migrationBuilder.CreateIndex(
                name: "IX_schedule_execution_log_execution_date",
                table: "schedule_execution_log",
                column: "execution_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "schedule_execution_log");
            migrationBuilder.DropTable(name: "schedule_execution");
        }
    }
}
