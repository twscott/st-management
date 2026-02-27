# Stock60 重算功能设计文档

## 概述

为 SST 股票导入系统添加 Stock60 批量重算功能，用于重新计算 stock60days 表中的技术指标（MA、MV、KD、布林带）。

## 背景

1. 原始系统的 MA/MV/KD/布林带计算使用了错误的日期逻辑（使用自然日期而非交易日）
2. 用户希望能够批量重新计算这些指标
3. 需要支持长时间运行的计算任务，并支持中断和断点续算

## 功能需求

### 3.1 核心修正：交易日逻辑

**关键点**：所有技术指标的计算都必须基于数据库中的 `LastDate` 栏位来确定交易日关系，而不是自然日期。

- **向前 N 天**：指的是向前取 N 个 **LastDate** 记录，而不是向前 N 个日历日
- **下一天**：需要查询数据库中该股票的下一个 LastDate，而不是简单日期加一

### 3.2 参数设计
- **开始 LastDate (StartLastDate)**: 从哪一个 LastDate 开始计算
- **天数 (Days)**: 计算几个 LastDate

### 3.3 计算内容

| 指标 | 历史数据需求 | 计算方式 |
|------|-------------|---------|
| MA(N) | 前 N 个 **LastDate** | 按 LastDate 排序取前 N 条，计算平均价 |
| MV(N) | 前 N 个 **LastDate** | 按 LastDate 排序取前 N 条，计算平均量 |
| KD | 前 9 个 **LastDate** + 前一天 K/D | RSV = (收盘价 - 9日最低) / (9日最高 - 9日最低) × 100 |
| 布林带 | 前 20 个 **LastDate** | Mid = 平均价，StdDev = 标准差，Up/Down = Mid ± 2×StdDev |

1. **MA (移动平均价)**: MA5, MA10, MA14, MA20, MA35, MA60
2. **MV (移动平均量)**: MV5, MV10, MV14, MV20, MV35, MV60
3. **KD 指标**: KD_RSV, KD_K, KD_D
4. **布林带**: boolUp, boolMid, boolDown

### 3.4 算法说明

**MA/MV 计算**:
```csharp
// 按 LastDate 排序，取前 N 条记录计算平均值
var historicalData = await _context.Stock60Days
    .Where(s => s.StockID == stockId && s.LastDate <= targetLastDate)
    .OrderByDescending(s => s.LastDate)
    .Take(period)
    .ToListAsync();

MA = historicalData.Average(s => s.EndPrice);
MV = (int)historicalData.Average(s => s.Vol);
```

**KD 计算**:
```csharp
// 获取最近 9 个 LastDate 的数据
var last9Days = historicalData.Take(9).ToList();
var high = last9Days.Max(s => s.EndPrice);
var low = last9Days.Min(s => s.EndPrice);

RSV = (currentPrice - low) / (high - low) * 100;

// 获取前一交易日的 K/D 值
K = (2/3) * previousK + (1/3) * RSV;
D = (2/3) * previousD + (1/3) * K;
```

**布林带计算**:
```csharp
// 取最近 20 个 LastDate
var last20Days = historicalData.Take(20).ToList();
var mid = last20Days.Average(s => s.EndPrice);
var stdDev = CalculateStdDev(last20Days.Select(s => s.EndPrice));

boolUp = mid + 2 * stdDev;
boolMid = mid;
boolDown = mid - 2 * stdDev;
```

### 3.5 计算流程

```
从 StartLastDate 开始（必须按时间顺序依序计算）：
  │
  ├─ 获取该 LastDate 的所有股票
  │
  ├─ 对每只股票：
  │    ├─ 向前取 N 个 LastDate 的历史数据
  │    ├─ 计算 MA/MV/KD/布林带
  │    └─ 保存结果
  │
  ├─ 找到下一个 LastDate（从任意股票获取下一个 LastDate）
  └─ 继续循环
```

**重要约束**：
- 必须按时间顺序从最早日期依序计算到最新日期
- 因为 KD 指标需要前一天的 K/D 值（递归计算）

### 3.6 UI 功能
- LastDate 选择器：选择开始计算的 LastDate
- 天数输入框：输入计算几天
- 快捷选项：7天、14天、30天
- "开始计算" 按钮
- "中断" 按钮
- 进度显示：已计算天数（从开始至今）
- 中断后显示最后处理的 LastDate

### 3.7 断点续算
- **中断时**：显示最后处理的 **LastDate**
- **续算**：用户需要手动输入起始 LastDate
- 用户可查询数据库 `stock60days` 表的 `LastDate` 栏位确认上一次的进度

### 3.8 约束
- 数据从 2025-01-01 开始
- 交易日通过数据库的 LastDate 栏位确定
- 支持长时间运行任务的取消

## 技术架构

### 4.1 后端架构
```
Stock60DaysRecalcService
  ├── RecalculateAsync(startLastDate, days) - 主方法
  ├── ProcessSingleDayAsync() - 处理单个 LastDate
  ├── CalculateMAAsync() - 计算 MA/MV
  ├── CalculateKDAsync() - 计算 KD
  └── CalculateBollingerBandsAsync() - 计算布林带
```

**实现方式**：方案 B - 在内存中按 LastDate 排序计算
- 预先加载历史数据到内存
- 按 LastDate 排序后计算
- 代码清晰，易于理解和维护

### 4.2 API 端点
- `POST /api/stock60days/recalc` - 开始重算
- `POST /api/stock60days/cancel` - 中断计算
- `GET /api/stock60days/progress` - 获取进度

### 4.3 前端页面
- `/stock60days-recalc` - Stock60 重算页面

## 性能

### 5.1 性能预估
- 每个交易日约 **2.5 分钟**
- 40 个交易日约 **100 分钟**
- 每天约 2300 檔股票

### 5.2 优化策略
- 批量预加载历史数据（减少数据库往返）
- 使用内存计算（避免 SQL 复杂度）

## 文件清单

### 新增文件
- `src/SST.StockImport.Services/Stock60DaysRecalcService.cs`
- `src/SST.StockImport.Core/Interfaces/IStock60DaysRecalcService.cs`
- `src/SST.StockImport.API/Controllers/Stock60DaysController.cs`
- `src/SST.StockImport.Web/Components/Pages/Stock60DaysRecalcPage.razor`

### 修改文件
- `src/SST.StockImport.Web/Components/Layout/NavMenu.razor`
- `src/SST.StockImport.Services/ServiceCollectionExtensions.cs`
- `src/SST.StockImport.Web/Components/Pages/ScheduleManagementPage.razor`
