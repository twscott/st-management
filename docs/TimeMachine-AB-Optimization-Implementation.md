# Time Machine A+B 优化实施报告

**实施日期**: 2026-03-07  
**优化依据**: [Docs/alertlist-success-pattern-analysis-report.md](alertlist-success-pattern-analysis-report.md)  
**实施策略**: A+B 组合（参数优化 + 筛选逻辑增强）

---

## ✅ 已完成修改

### 📋 选项 A: 修改 Time Machine 默认参数

**文件**: `src/SST.StockImport.Web/Components/Pages/TimeMachineAnalysis.razor` (line 590-599)

**修改对比**:

| 参数 | 原值（猜测） | 新值（数据驱动） | 调整理由 |
|------|-------------|----------------|----------|
| `MinMaturityScore` | 60 | **65** ↑ | 成功案例平均成熟度更高 |
| `MaxVolumeRatio` | 50 | **30** ↓ | **10-30x 成功率最高**，超过30x成功率降低 |
| `MaxCoolingDays` | 50 | **30** ↓ | 聚焦最佳进场时机（8-30天） |
| `MinKD` | 30 | **50** ↑ | 成功案例 avg KD=65.69 |
| `MaxKD` | 60 | **80** ↑ | 避免过滤掉高 KD 的优质标的 |
| `MinBandwidth` | 30 | **25** ↓ | 稍微放宽，增加候选数量 |

**影响**: 
- 减少量能倍数过高的风险案例
- 提高技术指标门槛（KD 50-80）
- 聚焦 8-30 天冷却期（最佳进场时机）

---

### 🎯 选项 B: 添加进货/出货比率筛选

**文件**: `src/SST.StockImport.Infrastructure/Services/TimeMachineAnalysisService.cs`

**新增数据字段** (line 139-152):
```sql
-- ⭐ 进货/出货指标 (基于成功模式分析)
a.pLVRatePosCnt as buy_count,        -- 价涨大量进货次数
a.pLVRateNegCnt as sell_count,       -- 价跌大量出货次数
a.pApRatePosCnt as peak_buy_count,   -- 10倍盘量进货次数

-- 进货/出货比率 (最关键指标！成功案例 avg=2.45)
CASE 
    WHEN a.pLVRateNegCnt = 0 THEN 999  -- 无出货视为极高比率
    ELSE ROUND(a.pLVRatePosCnt / a.pLVRateNegCnt, 2)
END as buy_sell_ratio,

-- MA5/MA20 ratio (成功案例 avg=103.34%)
CASE 
    WHEN s60.MA20 > 0 THEN ROUND((s60.MA5 / s60.MA20) * 100, 2)
    ELSE NULL
END as ma_ratio,
```

**新增筛选条件** (line 191-200):
```sql
-- ⭐ 新增筛选条件 (基于成功模式分析)
AND a.pLVRateNegCnt <= 1                    -- 限制出货次数 (成功案例 avg=0.53)
AND a.pApRatePosCnt >= 1                    -- 至少 1 次 10倍盘量进货
AND (
    a.pLVRateNegCnt = 0                     -- 无出货，或
    OR (a.pLVRatePosCnt / a.pLVRateNegCnt) >= 2.5  -- 进货优势比 >= 2.5x
)
AND (s60.MA20 IS NULL OR s60.MA20 = 0 OR (s60.MA5 / s60.MA20 * 100) >= 102)  -- 均线多头
```

**影响**:
- 过滤掉主力出货的股票（出货次数 ≤ 1）
- 确保有明显主力进货迹象（10倍盘量进货 ≥ 1）
- 进货优势比 ≥ 2.5x（成功案例平均值，50% 成功率时为 5x）
- 均线多头排列（MA5/MA20 ≥ 102%）

---

## 📊 预期效果

### 成功率提升预期

**原参数（猜测）问题**:
- MaxVolumeRatio=50 太高 → 包含过多追高案例
- 缺少进货/出货比率筛选 → 包含主力出货案例
- KD 范围太低（30-60）→ 错过高 KD 优质标的

**新参数（数据驱动）优势**:
- 量能倍数 10-30x → **成功率 25%+**（原 50x+ 只有 17%）
- 进货优势比 ≥ 2.5x → **成功率 30%+**（原无筛选约 20%）
- 出货次数 ≤ 1 → 排除主力出货案例
- 10倍盘量进货 ≥ 1 → 确保有明显异动

**综合预期**: 
- 候选数量: 可能减少 20-30%（更精准）
- 成功率: **预期从 ~20% 提升到 30-35%**
- 涨幅: 成功案例平均涨幅应类似（~67%）

---

## 🧪 验证方法

### 1. 回测验证（推荐）

运行时光机回测，对比新旧参数效果：

```powershell
# 使用新参数回测 2025年 9月
# 访问 http://localhost:5089/timemachine
# 设置 Analysis Date: 2025-09-15
# 点击 "分析" 查看候选和成功率
```

### 2. SQL 验证（快速）

直接查询新筛选条件的效果：

```sql
-- 查看 2025年9月符合新条件的候选数量
SELECT COUNT(*) as candidate_count
FROM alertlist a
INNER JOIN stock60days s60 ON s60.StockID = a.StockID AND s60.StockDate = '2025-09-15'
WHERE a.alertDate < '2025-09-15'
  AND DATEDIFF('2025-09-15', a.alertDate) BETWEEN 8 AND 30  -- 新 MaxCoolingDays
  AND a.maxPLVR BETWEEN 10 AND 30  -- 新 MaxVolumeRatio
  AND s60.KD_K BETWEEN 50 AND 80   -- 新 KD 范围
  AND a.pLVRateNegCnt <= 1         -- 新增
  AND a.pApRatePosCnt >= 1         -- 新增
  AND (a.pLVRateNegCnt = 0 OR (a.pLVRatePosCnt / a.pLVRateNegCnt) >= 2.5);  -- 新增
```

---

## 🚀 下一步建议

### 短期（本周）
1. ✅ **已完成**: 实施 A+B 参数优化
2. ⏳ **待验证**: 运行回测验证 2025年8-12月数据
3. ⏳ **待观察**: 实际使用 1-2 周，收集反馈

### 中期（下周）
1. **按股票类型差异化筛选**:
   - 興櫃: VolumeRatio 10-25x, MaturityScore 60
   - 上市/上櫃: VolumeRatio 15-30x, MaturityScore 70

2. **添加 UI 显示进货/出货比率**:
   - 在候选列表中显示 `BuySellRatio` 字段
   - 高亮显示比率 ≥ 5x 的案例（50% 成功率）

### 长期（选项 D）
3. **创建「智能推荐 2.0」**:
   - 完全基于成功模式分析的新算法
   - 自动按股票类型调整参数
   - 整合更多 alertlist 指标（p5VRatePosCnt, paRatePosCnt）

---

## 📝 技术细节

### 编译验证
```bash
✅ dotnet build src/SST.StockImport.Infrastructure/SST.StockImport.Infrastructure.csproj
   建置成功。0 個警告 0 個錯誤

✅ dotnet build src/SST.StockImport.Web/SST.StockImport.Web.csproj
   建置成功。0 個警告 0 個錯誤
```

### 受影响的文件
1. `src/SST.StockImport.Web/Components/Pages/TimeMachineAnalysis.razor` - 默认参数修改
2. `src/SST.StockImport.Infrastructure/Services/TimeMachineAnalysisService.cs` - SQL 查询增强

### 向后兼容性
- ✅ UI 参数可手动调整（用户可恢复旧值）
- ✅ SQL 查询向后兼容（新字段为 NULL 时不影响）
- ✅ 现有 DTO 无需修改（新字段未暴露到 API）

---

## 📚 参考文档

- 完整分析报告: [Docs/alertlist-success-pattern-analysis-report.md](alertlist-success-pattern-analysis-report.md)
- 分析 SQL: `analyze-alertlist-success-simple.sql`
- 原始数据: 2025年8-12月 alertlist (261 个案例，58 个成功案例)

---

**修改者**: AI Agent (GitHub Copilot)  
**审核状态**: 待用户验证  
**版本**: 1.0
