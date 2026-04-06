using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SST.StockImport.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyTaskExecutionTable_Clean : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 创建 daily_task_execution 表
            migrationBuilder.Sql(@"
                CREATE TABLE IF NOT EXISTS `daily_task_execution` (
                    `id` int NOT NULL AUTO_INCREMENT,
                    `execution_date` datetime(6) NOT NULL,
                    `task_type` varchar(50) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                    `status` varchar(20) CHARACTER SET utf8mb4 NOT NULL DEFAULT '',
                    `start_time` datetime(6) NULL,
                    `end_time` datetime(6) NULL,
                    `error_message` varchar(1000) CHARACTER SET utf8mb4 NULL,
                    `retry_count` int NOT NULL DEFAULT 0,
                    `created_at` datetime(6) NOT NULL,
                    `updated_at` datetime(6) NOT NULL,
                    PRIMARY KEY (`id`),
                    INDEX `IX_execution_date` (`execution_date`),
                    INDEX `IX_status` (`status`)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 删除表
            migrationBuilder.Sql("DROP TABLE IF EXISTS `daily_task_execution`;");
        }
    }
}
