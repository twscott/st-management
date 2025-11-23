# Data Model: 每日股票交易數據匯入系統

**Date**: 2025-11-23  
**Feature**: 001-daily-data-import  
**Purpose**: Define entities, relationships, and database schema

## Entity Relationship Diagram

```
┌─────────────────┐          ┌─────────────────┐
│   ImportJob     │1        *│   AlertLog      │
│                 ├──────────>│                 │
│ - JobId (PK)    │  records  │ - Id (PK)       │
│ - Market        │           │ - JobId (FK)    │
│ - Status        │           │ - AlertType     │
│ - StartTime     │           │ - Message       │
│ - EndTime       │           └─────────────────┘
│ - ExecutorType  │
│ - ExecutorId    │
└────────┬────────┘
         │ creates
         │ 1
         │
         │ *
┌────────▼────────┐          ┌─────────────────┐
│   TradeData     │          │  Stock60Days    │
│                 │          │                 │
│ - StockCode (PK)│          │ - StockCode (PK)│
│ - TradeDate (PK)│          │ - LastUpdate    │
│ - Market        │          │ - Avg5Price     │
│ - OpenPrice     │◄─────────┤ - Avg5Volume    │
│ - ClosePrice    │ triggers │ - Avg20Price    │
│ - HighPrice     │ update   │ - Avg60Price    │
│ - LowPrice      │          │ - Avg60Volume   │
│ - Volume        │          └─────────────────┘
│ - TradeCount    │
└─────────────────┘
```

## Entity Definitions

### 1. TradeData（交易數據）

**Purpose**: 儲存每檔股票每日的交易資訊

**Table Name**: `tradedata`（現有資料表，維持相容性）

**Schema**:
```sql
CREATE TABLE IF NOT EXISTS tradedata (
    stock_code      VARCHAR(10)     NOT NULL COMMENT '股票代碼',
    trade_date      DATE            NOT NULL COMMENT '交易日期',
    market          VARCHAR(20)     NOT NULL COMMENT '市場類別: TSE/OTC/EMERGING',
    open_price      DECIMAL(10,2)   NOT NULL COMMENT '開盤價',
    close_price     DECIMAL(10,2)   NOT NULL COMMENT '收盤價',
    high_price      DECIMAL(10,2)   NOT NULL COMMENT '最高價',
    low_price       DECIMAL(10,2)   NOT NULL COMMENT '最低價',
    volume          BIGINT          NOT NULL COMMENT '成交量（股）',
    trade_count     INT             NULL     COMMENT '成交筆數',
    created_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    updated_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    PRIMARY KEY (stock_code, trade_date),
    INDEX idx_market_date (market, trade_date),
    INDEX idx_trade_date (trade_date DESC),
    
    CHECK (high_price >= open_price),
    CHECK (high_price >= close_price),
    CHECK (high_price >= low_price),
    CHECK (low_price <= open_price),
    CHECK (low_price <= close_price),
    CHECK (volume >= 0),
    CHECK (trade_count >= 0 OR trade_count IS NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='每日股票交易數據';
```

**C# Entity**:
```csharp
public class TradeData
{
    [Key, Column("stock_code", Order = 0)]
    [StringLength(10)]
    public string StockCode { get; set; } = string.Empty;

    [Key, Column("trade_date", Order = 1)]
    public DateTime TradeDate { get; set; }

    [Required]
    [Column("market")]
    [StringLength(20)]
    public string Market { get; set; } = string.Empty; // TSE, OTC, EMERGING

    [Required]
    [Column("open_price")]
    [Precision(10, 2)]
    public decimal OpenPrice { get; set; }

    [Required]
    [Column("close_price")]
    [Precision(10, 2)]
    public decimal ClosePrice { get; set; }

    [Required]
    [Column("high_price")]
    [Precision(10, 2)]
    public decimal HighPrice { get; set; }

    [Required]
    [Column("low_price")]
    [Precision(10, 2)]
    public decimal LowPrice { get; set; }

    [Required]
    [Column("volume")]
    public long Volume { get; set; }

    [Column("trade_count")]
    public int? TradeCount { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

**Business Rules**:
- 複合主鍵：`(stock_code, trade_date)` 確保唯一性
- 價格邏輯驗證：`high_price >= open/close/low_price`, `low_price <= open/close/high_price`
- 市場類別限制：僅允許 `TSE`, `OTC`, `EMERGING`
- 重複數據處理：UPSERT 邏輯（存在則更新）

---

### 2. Stock60Days（60日統計）

**Purpose**: 儲存每檔股票的移動平均統計指標

**Table Name**: `stock60days`（現有資料表，維持相容性）

**Schema**:
```sql
CREATE TABLE IF NOT EXISTS stock60days (
    stock_code      VARCHAR(10)     NOT NULL COMMENT '股票代碼',
    last_update     DATE            NOT NULL COMMENT '最後更新日期',
    avg_5_price     DECIMAL(10,2)   NULL     COMMENT '5日均價',
    avg_5_volume    BIGINT          NULL     COMMENT '5日均量',
    avg_20_price    DECIMAL(10,2)   NULL     COMMENT '20日均價',
    avg_60_price    DECIMAL(10,2)   NULL     COMMENT '60日均價',
    avg_60_volume   BIGINT          NULL     COMMENT '60日均量',
    updated_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    PRIMARY KEY (stock_code),
    INDEX idx_last_update (last_update DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='股票60日移動統計';
```

**C# Entity**:
```csharp
public class Stock60Days
{
    [Key]
    [Column("stock_code")]
    [StringLength(10)]
    public string StockCode { get; set; } = string.Empty;

    [Required]
    [Column("last_update")]
    public DateTime LastUpdate { get; set; }

    [Column("avg_5_price")]
    [Precision(10, 2)]
    public decimal? Avg5Price { get; set; }

    [Column("avg_5_volume")]
    public long? Avg5Volume { get; set; }

    [Column("avg_20_price")]
    [Precision(10, 2)]
    public decimal? Avg20Price { get; set; }

    [Column("avg_60_price")]
    [Precision(10, 2)]
    public decimal? Avg60Price { get; set; }

    [Column("avg_60_volume")]
    public long? Avg60Volume { get; set; }

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

**Update Logic**:
```csharp
public async Task UpdateStatisticsAsync(string stockCode, DateTime tradeDate)
{
    // 計算 5/20/60 日移動平均
    var recent5Days = await _context.TradeData
        .Where(t => t.StockCode == stockCode && t.TradeDate <= tradeDate)
        .OrderByDescending(t => t.TradeDate)
        .Take(5)
        .ToListAsync();

    var recent20Days = recent5Days.Count >= 5
        ? await _context.TradeData...Take(20).ToListAsync()
        : null;

    var recent60Days = recent20Days != null
        ? await _context.TradeData...Take(60).ToListAsync()
        : null;

    var stats = new Stock60Days
    {
        StockCode = stockCode,
        LastUpdate = tradeDate,
        Avg5Price = recent5Days.Average(t => t.ClosePrice),
        Avg5Volume = (long)recent5Days.Average(t => t.Volume),
        Avg20Price = recent20Days?.Average(t => t.ClosePrice),
        Avg60Price = recent60Days?.Average(t => t.ClosePrice),
        Avg60Volume = recent60Days != null ? (long)recent60Days.Average(t => t.Volume) : null
    };

    await _context.Stock60Days.Upsert(stats).RunAsync();
}
```

---

### 3. AlertLog（警報日誌）

**Purpose**: 記錄匯入過程中的錯誤和警告

**Table Name**: `alertlog`（現有資料表，維持相容性）

**Schema**:
```sql
CREATE TABLE IF NOT EXISTS alertlog (
    id              BIGINT          NOT NULL AUTO_INCREMENT COMMENT '警報ID',
    job_id          CHAR(36)        NULL     COMMENT '關聯的匯入任務ID（UUID）',
    alert_type      VARCHAR(20)     NOT NULL COMMENT '警報類型: ERROR/WARNING/INFO',
    stock_code      VARCHAR(10)     NULL     COMMENT '相關股票代碼（若適用）',
    message         TEXT            NOT NULL COMMENT '警報訊息',
    stack_trace     TEXT            NULL     COMMENT '錯誤堆疊（ERROR時）',
    created_at      DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP COMMENT '發生時間',
    
    PRIMARY KEY (id),
    INDEX idx_job_id (job_id),
    INDEX idx_alert_type (alert_type),
    INDEX idx_created_at (created_at DESC),
    INDEX idx_stock_code (stock_code)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='警報與錯誤日誌';
```

**C# Entity**:
```csharp
public class AlertLog
{
    [Key]
    [Column("id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long Id { get; set; }

    [Column("job_id")]
    [StringLength(36)]
    public string? JobId { get; set; }

    [Required]
    [Column("alert_type")]
    [StringLength(20)]
    public string AlertType { get; set; } = string.Empty; // ERROR, WARNING, INFO

    [Column("stock_code")]
    [StringLength(10)]
    public string? StockCode { get; set; }

    [Required]
    [Column("message")]
    public string Message { get; set; } = string.Empty;

    [Column("stack_trace")]
    public string? StackTrace { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ImportJob? ImportJob { get; set; }
}

public enum AlertType
{
    INFO,
    WARNING,
    ERROR
}
```

**Usage Examples**:
```csharp
// 記錄個股異常數據警告
await _alertLog.RecordAsync(new AlertLog
{
    JobId = currentJobId,
    AlertType = "WARNING",
    StockCode = "2330",
    Message = "價格變動超過50%：前日100元，今日160元，變動+60%"
});

// 記錄資料筆數異常錯誤
await _alertLog.RecordAsync(new AlertLog
{
    JobId = currentJobId,
    AlertType = "ERROR",
    Message = $"資料筆數差異過大：前次1000筆，本次200筆，差異800筆",
    StackTrace = Environment.StackTrace
});
```

---

### 4. ImportJob（匯入任務）

**Purpose**: 追蹤每次匯入執行的狀態和審計資訊

**Table Name**: `import_job`（新增資料表）

**Schema**:
```sql
CREATE TABLE IF NOT EXISTS import_job (
    id                  CHAR(36)        NOT NULL COMMENT '任務ID（UUID）',
    market              VARCHAR(20)     NOT NULL COMMENT '市場類別: TSE/OTC/EMERGING/ALL',
    status              VARCHAR(20)     NOT NULL COMMENT '狀態: RUNNING/COMPLETED/FAILED/CANCELLED',
    start_time          DATETIME        NOT NULL COMMENT '開始時間',
    end_time            DATETIME        NULL     COMMENT '結束時間',
    success_count       INT             NOT NULL DEFAULT 0 COMMENT '成功匯入股票數',
    failed_count        INT             NOT NULL DEFAULT 0 COMMENT '失敗股票數',
    total_count         INT             NOT NULL DEFAULT 0 COMMENT '總股票數',
    executor_type       VARCHAR(20)     NOT NULL COMMENT '執行者類型: USER/SCHEDULER',
    executor_identity   VARCHAR(100)    NOT NULL COMMENT '執行者識別: UserId 或 "HangfireScheduler"',
    trigger_ip_address  VARCHAR(45)     NULL     COMMENT '觸發IP位址（IPv4/IPv6）',
    error_message       TEXT            NULL     COMMENT '失敗原因（若FAILED）',
    created_at          DATETIME        NOT NULL DEFAULT CURRENT_TIMESTAMP,
    
    PRIMARY KEY (id),
    INDEX idx_market_status (market, status),
    INDEX idx_start_time (start_time DESC),
    INDEX idx_executor (executor_type, executor_identity)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='匯入任務追蹤';
```

**C# Entity**:
```csharp
public class ImportJob
{
    [Key]
    [Column("id")]
    [StringLength(36)]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [Required]
    [Column("market")]
    [StringLength(20)]
    public string Market { get; set; } = string.Empty; // TSE, OTC, EMERGING, ALL

    [Required]
    [Column("status")]
    [StringLength(20)]
    public string Status { get; set; } = JobStatus.Running; // RUNNING, COMPLETED, FAILED, CANCELLED

    [Required]
    [Column("start_time")]
    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    [Column("end_time")]
    public DateTime? EndTime { get; set; }

    [Column("success_count")]
    public int SuccessCount { get; set; } = 0;

    [Column("failed_count")]
    public int FailedCount { get; set; } = 0;

    [Column("total_count")]
    public int TotalCount { get; set; } = 0;

    [Required]
    [Column("executor_type")]
    [StringLength(20)]
    public string ExecutorType { get; set; } = string.Empty; // USER, SCHEDULER

    [Required]
    [Column("executor_identity")]
    [StringLength(100)]
    public string ExecutorIdentity { get; set; } = string.Empty;

    [Column("trigger_ip_address")]
    [StringLength(45)]
    public string? TriggerIpAddress { get; set; }

    [Column("error_message")]
    public string? ErrorMessage { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation property
    public ICollection<AlertLog> AlertLogs { get; set; } = new List<AlertLog>();
}

public static class JobStatus
{
    public const string Running = "RUNNING";
    public const string Completed = "COMPLETED";
    public const string Failed = "FAILED";
    public const string Cancelled = "CANCELLED";
}

public static class ExecutorType
{
    public const string User = "USER";
    public const string Scheduler = "SCHEDULER";
}
```

**State Transition**:
```
RUNNING ──┬──> COMPLETED (success_count + failed_count = total_count)
          ├──> FAILED (exception thrown, error_message set)
          └──> CANCELLED (CancellationToken triggered)
```

---

## Database Migration Strategy

### Initial Setup
```bash
# 檢查現有資料表
SELECT TABLE_NAME FROM information_schema.TABLES 
WHERE TABLE_SCHEMA = 'sst_db' 
AND TABLE_NAME IN ('tradedata', 'stock60days', 'alertlog');

# 僅創建新增的 import_job 表
# tradedata, stock60days, alertlog 維持現有結構（相容性）
```

### EF Core Migration
```bash
# 生成初始遷移
dotnet ef migrations add InitialCreate --project SST.Infrastructure

# 僅對 import_job 表執行遷移，跳過現有表
dotnet ef database update --project SST.Infrastructure
```

### Data Compatibility
- **TradeData**: 欄位名稱使用 snake_case（與舊系統一致）
- **Stock60Days**: 保留原欄位名稱和資料型態
- **AlertLog**: 新增 `job_id` 欄位，nullable（舊資料無 job_id）
- **ImportJob**: 全新資料表，無相容性問題

---

## Indexing Strategy

### Performance-Critical Queries
```sql
-- Query 1: 查詢特定日期的市場數據（用於筆數驗證）
SELECT COUNT(*) FROM tradedata 
WHERE market = 'TSE' AND trade_date = '2025-11-23';
-- INDEX: idx_market_date (market, trade_date)

-- Query 2: 查詢最近60天數據（用於統計計算）
SELECT * FROM tradedata 
WHERE stock_code = '2330' AND trade_date <= '2025-11-23'
ORDER BY trade_date DESC LIMIT 60;
-- INDEX: PRIMARY KEY (stock_code, trade_date)

-- Query 3: 查詢任務歷史
SELECT * FROM import_job 
WHERE market = 'TSE' AND status = 'COMPLETED'
ORDER BY start_time DESC LIMIT 10;
-- INDEX: idx_market_status (market, status), idx_start_time (start_time DESC)

-- Query 4: 查詢特定任務的警報日誌
SELECT * FROM alertlog 
WHERE job_id = 'uuid-xxx' AND alert_type = 'ERROR';
-- INDEX: idx_job_id (job_id), idx_alert_type (alert_type)
```

---

## Data Validation Rules

### Application-Level Validation (FluentValidation)
```csharp
public class TradeDataValidator : AbstractValidator<TradeData>
{
    public TradeDataValidator()
    {
        RuleFor(x => x.StockCode)
            .NotEmpty()
            .Length(4, 10)
            .Matches(@"^[A-Z0-9]+$");

        RuleFor(x => x.Market)
            .Must(x => new[] { "TSE", "OTC", "EMERGING" }.Contains(x));

        RuleFor(x => x.HighPrice)
            .GreaterThanOrEqualTo(x => x.OpenPrice)
            .GreaterThanOrEqualTo(x => x.ClosePrice)
            .GreaterThanOrEqualTo(x => x.LowPrice);

        RuleFor(x => x.LowPrice)
            .LessThanOrEqualTo(x => x.OpenPrice)
            .LessThanOrEqualTo(x => x.ClosePrice);

        RuleFor(x => x.Volume).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TradeCount).GreaterThanOrEqualTo(0).When(x => x.TradeCount.HasValue);
    }
}
```

---

## Summary

| Entity | Purpose | Rows (Est.) | Key Indexes |
|--------|---------|-------------|-------------|
| TradeData | 每日交易數據 | ~2000/day × 365 days = 730K/year | PK(stock_code, trade_date), idx_market_date |
| Stock60Days | 移動平均統計 | ~2000 (一股票一筆) | PK(stock_code) |
| AlertLog | 警報日誌 | ~100-500/day | idx_job_id, idx_alert_type, idx_created_at |
| ImportJob | 任務追蹤 | ~2-10/day | PK(id), idx_market_status, idx_start_time |

**Total Storage Estimate**: ~100MB/year (TradeData), ~5MB (其他表)

---

✅ Data model complete - Proceed to API contracts design
