using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SST.StockImport.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDailyTaskExecutionTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UK_execution",
                table: "schedule_execution");

            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "schedule_execution_log",
                type: "JSON",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "json",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "success_count",
                table: "schedule_execution",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.UpdateData(
                table: "schedule_execution",
                keyColumn: "status",
                keyValue: null,
                column: "status",
                value: "");

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "schedule_execution",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_time",
                table: "schedule_execution",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<int>(
                name: "fail_count",
                table: "schedule_execution",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int",
                oldDefaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "error_message",
                table: "schedule_execution",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_time",
                table: "schedule_execution",
                type: "datetime(6)",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)");

            migrationBuilder.AlterColumn<int>(
                name: "duration_seconds",
                table: "schedule_execution",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "ai_training_log",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    execution_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    start_time = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    end_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    duration_minutes = table.Column<int>(type: "int", nullable: true),
                    message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    process_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_training_log", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "alertlog",
                columns: table => new
                {
                    Log_ID = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    CREATED = table.Column<DateTime>(type: "timestamp", nullable: false),
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AlertTitle = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    AlertType = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CurrPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    CurrVol = table.Column<int>(type: "int", nullable: true),
                    panVol = table.Column<int>(type: "int", nullable: true),
                    panTrans = table.Column<int>(type: "int", nullable: true),
                    panVolTransRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    diffPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    DiffRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    recommandBy = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recommandPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    priority = table.Column<int>(type: "int", nullable: true),
                    stockPriority = table.Column<int>(type: "int", nullable: true),
                    prePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    preVol = table.Column<int>(type: "int", nullable: true),
                    preTime = table.Column<DateTime>(type: "timestamp", nullable: true),
                    lastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    avg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    instantMass = table.Column<int>(type: "int", nullable: true),
                    messRise = table.Column<int>(type: "int", nullable: true),
                    messFall = table.Column<int>(type: "int", nullable: true),
                    instantRise = table.Column<int>(type: "int", nullable: true),
                    instantFall = table.Column<int>(type: "int", nullable: true),
                    InstRiseFallRate = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    panAmtDiff = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    panAmtRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    panAvgVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    panLastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    panAvg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    instantBuyVol = table.Column<int>(type: "int", nullable: true),
                    instantSellVol = table.Column<int>(type: "int", nullable: true),
                    instantIdx = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpenPriec = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    instantJumpKong = table.Column<int>(type: "int", nullable: true),
                    lastPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    lastVol = table.Column<int>(type: "int", nullable: true),
                    avg5Vol = table.Column<int>(type: "int", nullable: true),
                    panVol5CntPos = table.Column<int>(type: "int", nullable: true),
                    panVol5CntNeg = table.Column<int>(type: "int", nullable: true),
                    panVol5QuanPos = table.Column<int>(type: "int", nullable: true),
                    panVol5QuanNeg = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_alertlog", x => x.Log_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "buyin",
                columns: table => new
                {
                    BuyIn_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockType = table.Column<string>(type: "varchar(5)", maxLength: 5, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DataDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    BuyInPoint = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    BuyInCount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    stockLeftCount = table.Column<int>(type: "int", nullable: false),
                    StopLoss = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StopProfit = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    BuyInReason = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recommandBy = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Note = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    CREATED = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    lastPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lastVol = table.Column<int>(type: "int", nullable: false),
                    transVol = table.Column<int>(type: "int", nullable: false),
                    avgAmt5D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgVol5D = table.Column<int>(type: "int", nullable: false),
                    avgAmt10D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgAmt20D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgAmtSeason = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    onTimePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    onTimeVol = table.Column<int>(type: "int", nullable: false),
                    momentAVDVol = table.Column<int>(type: "int", nullable: false),
                    lastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    if20Hight = table.Column<int>(type: "int", nullable: false),
                    dailyNoPriceCnt = table.Column<int>(type: "int", nullable: false),
                    dailyNoVolCnt = table.Column<int>(type: "int", nullable: false),
                    dailyNoDataCnt = table.Column<int>(type: "int", nullable: true),
                    updated = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    myPredict = table.Column<int>(type: "int", nullable: false),
                    invPredict = table.Column<int>(type: "int", nullable: false),
                    instantMass = table.Column<int>(type: "int", nullable: false),
                    instantRise = table.Column<int>(type: "int", nullable: false),
                    instantFall = table.Column<int>(type: "int", nullable: false),
                    messRise = table.Column<int>(type: "int", nullable: false),
                    messFall = table.Column<int>(type: "int", nullable: false),
                    instantIdx = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    playerID = table.Column<int>(type: "int", nullable: false),
                    OpenPriec = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lastDate = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_buyin", x => x.BuyIn_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "daily_task_execution",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    execution_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    task_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    start_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    end_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    error_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    retry_count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_daily_task_execution", x => x.id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "goodinfo_failed_link_tracking",
                columns: table => new
                {
                    execution_date = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    failed_link_ids = table.Column<string>(type: "JSON", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    total_links = table.Column<int>(type: "int", nullable: false),
                    success_count = table.Column<int>(type: "int", nullable: false),
                    fail_count = table.Column<int>(type: "int", nullable: false),
                    total_retries = table.Column<int>(type: "int", nullable: false),
                    last_retry_time = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    last_retry_slot = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_goodinfo_failed_link_tracking", x => x.execution_date);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "investbase",
                columns: table => new
                {
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    stockName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recDate = table.Column<DateTime>(type: "date", nullable: true),
                    lastDate = table.Column<DateTime>(type: "date", nullable: true),
                    currPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    lastPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    lastVol = table.Column<int>(type: "int", nullable: true),
                    onTimePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    onTimeVol = table.Column<int>(type: "int", nullable: true),
                    OpenPriec = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Hprice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Lprice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    avgAmt5D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    avgVol5D = table.Column<int>(type: "int", nullable: true),
                    avgAmt10D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    avgAmt20D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    avgAmtSeason = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    momentAVDVol = table.Column<int>(type: "int", nullable: true),
                    transVol = table.Column<int>(type: "int", nullable: true),
                    reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Priority = table.Column<int>(type: "int", nullable: true),
                    lastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    avg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    dailyNoPriceCnt = table.Column<int>(type: "int", nullable: true),
                    dailyNoVolCnt = table.Column<int>(type: "int", nullable: true),
                    dailyNoDataCnt = table.Column<int>(type: "int", nullable: true),
                    myPredict = table.Column<int>(type: "int", nullable: true),
                    invPredict = table.Column<int>(type: "int", nullable: true),
                    stoploss = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    instantMass = table.Column<int>(type: "int", nullable: true),
                    instantRise = table.Column<int>(type: "int", nullable: true),
                    instantFall = table.Column<int>(type: "int", nullable: true),
                    messRise = table.Column<int>(type: "int", nullable: true),
                    messFall = table.Column<int>(type: "int", nullable: true),
                    instantIdx = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    panVol5Cnt = table.Column<int>(type: "int", nullable: true),
                    panVol10Cnt = table.Column<int>(type: "int", nullable: true),
                    panVol5Dict = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    panVol5CntPos = table.Column<int>(type: "int", nullable: true),
                    panVol5CntNeg = table.Column<int>(type: "int", nullable: true),
                    panVol5QuanPos = table.Column<int>(type: "int", nullable: true),
                    panVol5QuanNeg = table.Column<int>(type: "int", nullable: true),
                    vol0921 = table.Column<int>(type: "int", nullable: true),
                    priceDiffRate0921 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    instantBuyVol = table.Column<int>(type: "int", nullable: true),
                    instantSellVol = table.Column<int>(type: "int", nullable: true),
                    instantJumpKong = table.Column<int>(type: "int", nullable: true),
                    InstRiseFallRate = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    updated = table.Column<DateTime>(type: "timestamp", nullable: true)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investbase", x => x.StockID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "recommandstock",
                columns: table => new
                {
                    RecommandID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    reccDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    stockName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockType = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ByWho = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OpenPriec = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    currPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    recommandPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    mostUpdatedNotice = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CREATED = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    removeDate = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ifDeleted = table.Column<int>(type: "int", nullable: false),
                    lastPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lastVol = table.Column<int>(type: "int", nullable: false),
                    transVol = table.Column<int>(type: "int", nullable: false),
                    avgAmt5D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgVol5D = table.Column<int>(type: "int", nullable: false),
                    avgAmt10D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgAmt20D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgAmtSeason = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    onTimePrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    onTimeVol = table.Column<int>(type: "int", nullable: false),
                    momentAVDVol = table.Column<int>(type: "int", nullable: false),
                    lastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    if20Hight = table.Column<int>(type: "int", nullable: false),
                    dailyNoPriceCnt = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    dailyNoVolCnt = table.Column<int>(type: "int", nullable: false),
                    dailyNoDataCnt = table.Column<int>(type: "int", nullable: false),
                    updated = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.ComputedColumn),
                    myPredict = table.Column<int>(type: "int", nullable: false),
                    invPredict = table.Column<int>(type: "int", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    instantMass = table.Column<int>(type: "int", nullable: false),
                    instantRise = table.Column<int>(type: "int", nullable: false),
                    instantFall = table.Column<int>(type: "int", nullable: false),
                    messRise = table.Column<int>(type: "int", nullable: false),
                    messFall = table.Column<int>(type: "int", nullable: false),
                    instantIdx = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lastDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    stoploss = table.Column<decimal>(type: "decimal(10,0)", precision: 10, scale: 0, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recommandstock", x => x.RecommandID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "stock60days",
                columns: table => new
                {
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lastDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OpenPriec = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    EndPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    HPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Vol = table.Column<long>(type: "bigint", nullable: true),
                    MA5 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MA10 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MA14 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MA20 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MA35 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MA60 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    MV5 = table.Column<int>(type: "int", nullable: true),
                    MV10 = table.Column<int>(type: "int", nullable: true),
                    MV14 = table.Column<int>(type: "int", nullable: true),
                    MV20 = table.Column<int>(type: "int", nullable: false),
                    MV35 = table.Column<int>(type: "int", nullable: true),
                    MV60 = table.Column<int>(type: "int", nullable: true),
                    stable = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: false),
                    fluctuation = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: false),
                    droprate = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: false),
                    Pan3Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pan3Distance = table.Column<int>(type: "int", nullable: false),
                    stable3M = table.Column<int>(type: "int", nullable: false),
                    MaxPrice3M = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    MaxVol3M = table.Column<int>(type: "int", nullable: false),
                    intervalDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    intervalMaxVol = table.Column<int>(type: "int", nullable: false),
                    intervalMaxPrice = table.Column<decimal>(type: "decimal(10,0)", precision: 10, scale: 0, nullable: false),
                    jumpKong = table.Column<double>(type: "double", nullable: false),
                    contLittleRed = table.Column<sbyte>(type: "tinyint", nullable: false),
                    contLittleBlack = table.Column<int>(type: "int", nullable: false),
                    contLittleSoldier = table.Column<int>(type: "int", nullable: true),
                    middleVol = table.Column<int>(type: "int", nullable: false),
                    KD_RSV = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    KD_K = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    KD_D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    boolUp = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    boolMid = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    boolDown = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    boolkaikouDiffRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    turnoverRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    turnoverDiff = table.Column<int>(type: "int", nullable: false),
                    accLastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    accAvg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    accBoolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    accOpenRate = table.Column<sbyte>(type: "tinyint", nullable: false),
                    serialLow = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    panVol5CntPos = table.Column<int>(type: "int", nullable: false),
                    panVol5CntNeg = table.Column<int>(type: "int", nullable: false),
                    panVol5QuanPos = table.Column<int>(type: "int", nullable: false),
                    panVol5QuanNeg = table.Column<int>(type: "int", nullable: false),
                    panVol50CntPos = table.Column<sbyte>(type: "tinyint", nullable: false),
                    panVol50CntNeg = table.Column<sbyte>(type: "tinyint", nullable: false),
                    panvolScore = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stock60days", x => new { x.StockID, x.StockDate });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "tradedata",
                columns: table => new
                {
                    trade_ID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    TransDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    lastDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StockName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockType = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StockDiff = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    StockDiffRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    kShadow = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    upShadow = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    downShadow = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    OpenPriec = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    HPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    LPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    Vol = table.Column<long>(type: "bigint", nullable: true),
                    transVol = table.Column<int>(type: "int", nullable: false),
                    avgAmt5D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avgVol5D = table.Column<int>(type: "int", nullable: false),
                    avg5VolPerTrans = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lastPrice = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    lastVol = table.Column<int>(type: "int", nullable: false),
                    avgPanVol = table.Column<int>(type: "int", nullable: false),
                    lastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    avg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    volMonthMax = table.Column<int>(type: "int", nullable: false),
                    recNote = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LegalPersonNote = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    InvestAmt = table.Column<int>(type: "int", nullable: true),
                    foreigneAmt = table.Column<int>(type: "int", nullable: true),
                    farenSerialAmt = table.Column<int>(type: "int", nullable: true),
                    farenSerialDays = table.Column<int>(type: "int", nullable: true),
                    InvestSerealDays = table.Column<int>(type: "int", nullable: true),
                    foreigneSerealDays = table.Column<int>(type: "int", nullable: true),
                    forgneSwitch = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    invwstSwitch = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PrgForcaet = table.Column<int>(type: "int", nullable: true),
                    boolinPosition = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    boolDirection = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    boolUpDeviation = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    boolMidDeviation = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    boolDownDeviation = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    boolKaikou = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    boolKaikouCnt = table.Column<int>(type: "int", nullable: false),
                    MANote = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MA5 = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MA10 = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MA20 = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MASeason = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MAHalfYear = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MAYear = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MADirection = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DIF = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MACD = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    OSC = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MACDNote = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    rongziDiff = table.Column<int>(type: "int", nullable: false),
                    rongziRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    rongzi = table.Column<int>(type: "int", nullable: false),
                    rongziNote = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lastRongziDiff = table.Column<int>(type: "int", nullable: false),
                    lastRongquanDiff = table.Column<int>(type: "int", nullable: false),
                    rongquanNote = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ronquan = table.Column<int>(type: "int", nullable: false),
                    rongquanDiff = table.Column<int>(type: "int", nullable: false),
                    rongquanRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    quanziRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    bigVol = table.Column<int>(type: "int", nullable: false),
                    weekVol = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    instantMass = table.Column<int>(type: "int", nullable: false),
                    instantRise = table.Column<int>(type: "int", nullable: false),
                    instantFall = table.Column<int>(type: "int", nullable: false),
                    messRise = table.Column<int>(type: "int", nullable: false),
                    messFall = table.Column<int>(type: "int", nullable: false),
                    grossProfit = table.Column<int>(type: "int", nullable: false),
                    Profitability = table.Column<int>(type: "int", nullable: false),
                    financialReport = table.Column<int>(type: "int", nullable: false),
                    EPS = table.Column<int>(type: "int", nullable: false),
                    TipPrice = table.Column<int>(type: "int", nullable: false),
                    turnoverRate = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: false),
                    turnoverDiff = table.Column<int>(type: "int", nullable: false),
                    LowShadow5 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    LowHigh5 = table.Column<decimal>(type: "decimal(10,1)", precision: 10, scale: 1, nullable: false),
                    EndShadow5 = table.Column<int>(type: "int", nullable: false),
                    jumpKong = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    boxTop = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    boxBottom = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    longtermNote = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PriceGate = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    transDirection = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    deffectiveKong = table.Column<int>(type: "int", nullable: false),
                    activeKong3D = table.Column<int>(type: "int", nullable: false),
                    waveRate = table.Column<decimal>(type: "decimal(10,0)", precision: 10, scale: 0, nullable: false),
                    MVNote = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MVData = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MVDirection = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Pan3Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    xgPredict2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    xgTrainPredt2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    frstPredict2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    frstTrainPredt2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    nuralTrain2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    nuralPredict2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    xgOntimeTrain = table.Column<sbyte>(type: "tinyint", nullable: false),
                    xgOntimePredict = table.Column<sbyte>(type: "tinyint", nullable: false),
                    frstOntimeTrain = table.Column<sbyte>(type: "tinyint", nullable: false),
                    frstOntimePredict = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    neuralOntimeTrain = table.Column<sbyte>(type: "tinyint", nullable: false),
                    neuralOntimePredict = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60XgTrain2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60XgPrd2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60XgTrainOntime = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60XgPrdOntime = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60FrstTrain2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60FrstPrd2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60FrstTrainOntime = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60FrstPrdOntime = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60NuralTrain2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60NuralPrd2d = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60NuralTrainOntime = table.Column<sbyte>(type: "tinyint", nullable: false),
                    s60NuralPrdOntime = table.Column<sbyte>(type: "tinyint", nullable: false),
                    KD_RSV = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    KD_K = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    KD_D = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    accLastVolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    accAvg5VolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    accBoolRate = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    messStart = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    vol0921 = table.Column<int>(type: "int", nullable: false),
                    priceDiffRate0921 = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    panVol5CntPos = table.Column<int>(type: "int", nullable: false),
                    panVol5CntNeg = table.Column<int>(type: "int", nullable: false),
                    panVol5QuanPos = table.Column<int>(type: "int", nullable: false),
                    panVol5QuanNeg = table.Column<int>(type: "int", nullable: false),
                    pDiff = table.Column<sbyte>(type: "tinyint", nullable: false),
                    panVol50CntPos = table.Column<sbyte>(type: "tinyint", nullable: false),
                    panVol50CntNeg = table.Column<sbyte>(type: "tinyint", nullable: false),
                    panvolScore = table.Column<int>(type: "int", nullable: false),
                    noPVCDays = table.Column<int>(type: "int", nullable: false),
                    serialLowDiffRate = table.Column<int>(type: "int", nullable: false),
                    serialNoPanDiffQuant = table.Column<int>(type: "int", nullable: false),
                    serialLowPSV = table.Column<int>(type: "int", nullable: false),
                    PVCsumInDays = table.Column<int>(type: "int", nullable: false),
                    lowavg5 = table.Column<int>(type: "int", nullable: false),
                    stateStr = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tradedata", x => x.trade_ID);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "weekall",
                columns: table => new
                {
                    StockID = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StockName = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    StockType = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    lastDate = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    OpenPriec = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    EndPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    HPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    LPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    Vol = table.Column<long>(type: "bigint", nullable: true),
                    transVol = table.Column<int>(type: "int", nullable: false),
                    rongziDiff = table.Column<int>(type: "int", nullable: false),
                    rongquanDiff = table.Column<int>(type: "int", nullable: false),
                    tradMaxVol = table.Column<int>(type: "int", nullable: false),
                    tradMinVol = table.Column<int>(type: "int", nullable: false),
                    tradMediumVol = table.Column<int>(type: "int", nullable: false),
                    tradMediumPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    tradMaxPrice = table.Column<decimal>(type: "decimal(65,30)", nullable: false),
                    tradMinPrice = table.Column<double>(type: "double", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weekall", x => new { x.StockID, x.StockDate });
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ai_training_log_execution_date",
                table: "ai_training_log",
                column: "execution_date");

            migrationBuilder.CreateIndex(
                name: "IX_alertlog_StockID_CREATED",
                table: "alertlog",
                columns: new[] { "StockID", "CREATED" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_goodinfo_failed_link_tracking_execution_date",
                table: "goodinfo_failed_link_tracking",
                column: "execution_date");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_training_log");

            migrationBuilder.DropTable(
                name: "alertlog");

            migrationBuilder.DropTable(
                name: "buyin");

            migrationBuilder.DropTable(
                name: "daily_task_execution");

            migrationBuilder.DropTable(
                name: "goodinfo_failed_link_tracking");

            migrationBuilder.DropTable(
                name: "investbase");

            migrationBuilder.DropTable(
                name: "recommandstock");

            migrationBuilder.DropTable(
                name: "stock60days");

            migrationBuilder.DropTable(
                name: "tradedata");

            migrationBuilder.DropTable(
                name: "weekall");

            migrationBuilder.AlterColumn<string>(
                name: "details",
                table: "schedule_execution_log",
                type: "json",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "JSON",
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<int>(
                name: "success_count",
                table: "schedule_execution",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "schedule_execution",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "start_time",
                table: "schedule_execution",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "fail_count",
                table: "schedule_execution",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "error_message",
                table: "schedule_execution",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AlterColumn<DateTime>(
                name: "end_time",
                table: "schedule_execution",
                type: "datetime(6)",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime(6)",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "duration_seconds",
                table: "schedule_execution",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "UK_execution",
                table: "schedule_execution",
                columns: new[] { "execution_date", "schedule_slot" },
                unique: true);
        }
    }
}
