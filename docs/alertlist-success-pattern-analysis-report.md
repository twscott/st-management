# alertlist 成功模式分析报告
**分析时段**: 2025-08-01 ~ 2025-12-31  
**追踪天数**: 90天  
**成功标准**: 涨超 30%  
**数据来源**: MySQL sst database

---

## 📊 核心发现

### 🔥 最关键发现：量能倍数不是越高越好！

**成功率 by 量能倍数区间**：
```
10-15x:  25.25% success ✅ 最高区间之一
15-20x:  21.74% success ✅
20-30x:  27.91% success ✅ 最高！
30-50x:  13.16% success ⚠️ 降低
50x+:    17.14% success ⚠️ 更低
```

**结论**：量能倍数 10-30x 是最佳区间！超过30x反而成功率降低（可能是追高风险）

---

### 💎 最有效指标：进货/出货比率

**成功率 by pLVRatePosCnt / pLVRateNegCnt**：
```
5x+:              50.00% success 🌟 最佳！
1.5-2.5x:         30.77% success ✅
Pure Buy (无出货): 18.60% success ⚠️
<1.5x:            12.12% success ❌
2.5-5x:            0.00% success ❌
```

**结论**：进货优势比 >= 5x 时成功率最高（50%）！纯进货无出货反而不好（18.6%），可能是流动性差

---

### 📈 成功 vs 失败案例对比

|指标|SUCCESS (>30%)|MODERATE (10-30%)|FAILED (<10%)|关键差异|
|---|---|---|---|---|
|案例数|58|75|128|-|
|avg_maxPLVR|68.13|**139.26** ⚠️|70.07|失败案例量能倍数反而更高！|
|avg_paRatePosCnt|1.57|1.19|1.37|跳升次数差异不大|
|avg_pLVRatePosCnt|1.31|1.35|1.26|进货次数差异不大|
|avg_pLVRateNegCnt|**0.53** ✅|0.63|**0.69** ❌|成功案例出货更少！|
|avg_p5VRatePosCnt|0.67|0.76|0.83|5日均量进货差异不大|
|avg_pApRatePosCnt|1.36|1.53|1.13|10倍盘量进货差异小|
|**buy_sell_ratio_LV**|**2.45** ✅|2.15|**1.83** ❌|成功案例进货比高！|
|**buy_sell_ratio_5V**|**5.57** ✅|5.18|**4.61** ❌|成功案例进货比高！|
|avg_kd_k|**65.69** ✅|64.84|**60.38** ❌|成功案例KD更高|
|avg_ma5_ma20_ratio|**103.34%** ✅|102.61%|**101.88%** ❌|成功案例均线更强|
|avg_mv20|7,873|**31,145** ⚠️|4,115|中等涨幅案例流动性最好|

**关键洞察**：
1. ❌ **量能倍数不是越高越好**：MODERATE 组 avg_maxPLVR=139.26 最高，但涨幅不如 SUCCESS 组
2. ✅ **进货/出货比率最关键**：SUCCESS 组 buy_sell_ratio_LV=2.45 > FAILED 组的 1.83
3. ✅ **出货次数更少更好**：SUCCESS 组 avg_pLVRateNegCnt=0.53 < FAILED 组的 0.69
4. ✅ **KD 和 MA5/MA20 ratio 更高更好**：SUCCESS 组 KD=65.69, MA ratio=103.34%

---

### 🏢 按股票类型分析

|股票类型|总案例|成功案例|成功率|成功案例平均量能|
|---|---|---|---|---|
|**興櫃**|69|20|**28.99%** 🌟|**18.08**|
|上市|24|5|20.83%|34.60|
|上櫃|166|33|19.88%|**103.54** ⚠️|

**惊人发现**：
- 興櫃成功率最高（28.99%），但量能倍数要求更低（18.08）
- 上櫃案例最多（166），但成功率最低（19.88%），且量能倍数过高（103.54）

**结论**：不应该排除興櫃！反而應該針對不同股票類型設置不同的量能倍數門檻：
- 興櫃：10-25x
- 上市/上櫃：15-30x

---

### 🎯 Top 30 成功案例特征分析

**共同特征**：
1. **pApRatePosCnt >= 1**：多数案例有「价涨且5分钟盘量 > 昨日10倍」
2. **KD 值 60-97**：偏高，多数 > 60
3. **MA5/MA20 ratio > 100%**：均线多头排列
4. **量能倍数 10-30x**：适中，不追高

**有趣观察**：
- 很多成功案例 pLVRatePosCnt=0, pLVRateNegCnt=0（用昨天全天量衡量没有异动）
- 但 pApRatePosCnt >= 1（用昨日5分钟盘量10倍衡量有异动）
- 说明 **pApRatePosCnt（10倍盘量进货）比 pLVRatePosCnt（昨天全天量1/2进货）更敏感**

---

## 🎓 策略建议

### ✅ 应该调整的 Time Machine 参数

#### **当前参数（不准确）**：
```
MinMaturityScore = 60
MinVolumeRatio = 10
MaxVolumeRatio = 50  ← 太高了！
MinCoolingDays = 8
MaxCoolingDays = 50
MinKD = 30
MaxKD = 60  ← 太低了！
MinBandwidth = 30
```

#### **建议新参数（基于数据）**：

**全局筛选**：
```csharp
MinMaturityScore = 65      // 略微提高，从 60 → 65
MinVolumeRatio = 10        // 维持
MaxVolumeRatio = 30        // 从 50 降到 30！避免追高
MinCoolingDays = 8         // 维持
MaxCoolingDays = 30        // 从 50 降到 30，聚焦最佳时机
MinKD = 50                 // 从 30 提高到 50
MaxKD = 80                 // 从 60 提高到 80
MinBandwidth = 25          // 从 30 降到 25，稍微放宽
```

**按股票类型差异化筛选**（推荐！）：
```csharp
if (stock_type == "興櫃") {
    MinVolumeRatio = 10
    MaxVolumeRatio = 25      // 興櫃用较低倍数
    MinMaturityScore = 60     // 稍微放宽
}
else if (stock_type == "上市" || stock_type == "上櫃") {
    MinVolumeRatio = 15
    MaxVolumeRatio = 30      // 上市/上櫃用适中倍数
    MinMaturityScore = 70     // 提高要求
}
```

---

### ✅ 应该添加的新指标

#### 1. **进货/出货比率** (最重要！)
```sql
-- 计算进货优势比
(pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) >= 2.5

-- 或者更严格
(pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) >= 5.0  -- 50% 成功率！
```

#### 2. **MA5/MA20 ratio** (均线强度)
```sql
(MA5 / MA20 * 100) >= 102  -- 均线多头排列
```

#### 3. **出货次数限制** (避免主力出货)
```sql
pLVRateNegCnt <= 1  -- 最多只能有1次大量出货
```

#### 4. **pApRatePosCnt 优先** (10倍盘量进货)
```sql
pApRatePosCnt >= 1  -- 至少有1次「价涨且5分钟盘量 > 昨日10倍」
```

---

### ✅ 推荐的完整筛选逻辑

```sql
-- 基础指标
WHERE alertDate >= @start_date
  AND alertDate <= @end_date
  
  -- 量能倍数（适中最好）
  AND maxPLVR BETWEEN 10 AND 30
  
  -- 冷却天数
  AND DATEDIFF(@analysis_date, alertDate) BETWEEN 8 AND 30
  
  -- 技术指标
  AND KD_K BETWEEN 50 AND 80
  AND KD_D BETWEEN 40 AND 80
  AND (MA5 / MA20 * 100) >= 102  -- 均线多头
  AND bandwidth >= 25
  
  -- ⭐ 新增：进货/出货比率（最关键！）
  AND (
    (pLVRatePosCnt / NULLIF(pLVRateNegCnt, 0)) >= 2.5
    OR pLVRateNegCnt = 0  -- 无出货也可以
  )
  
  -- ⭐ 新增：限制出货次数
  AND pLVRateNegCnt <= 1
  
  -- ⭐ 新增：10倍盘量进货
  AND pApRatePosCnt >= 1
  
  -- 按股票类型差异化
  AND (
    (si.stype = '興櫃' AND maxPLVR BETWEEN 10 AND 25 AND MaturityScore >= 60)
    OR 
    (si.stype IN ('上市', '上櫃') AND maxPLVR BETWEEN 15 AND 30 AND MaturityScore >= 70)
  )
```

---

## 🚀 下一步行动

### Option 1: 修改 Time Machine UI 参数
- 文件：`src/SST.StockImport.Web/Components/Pages/TimeMachineAnalysis.razor`
- 修改默认参数值（line 598-607）

### Option 2: 修改 TimeMachineAnalysisService SQL 查询
- 文件：`src/SST.StockImport.Services/TimeMachineAnalysisService.cs`
- 添加进货/出货比率、MA5/MA20 ratio、pApRatePosCnt 筛选

### Option 3: 添加新的 DTO 字段
- 文件：`src/SST.StockImport.Core/DTOs/MaturityAnalysis/TimeMachineAnalysisDto.cs`
- 添加 `BuySellRatio`, `MA5MA20Ratio`, `PApRatePosTotal` 等字段

### Option 4: 创建新的「智能推荐」功能
- 基于上述分析结果，创建一个新的推荐算法
- 自动计算进货/出货比率 >= 5x 的案例
- 按股票类型自动调整参数

---

## 📝 结论

**最重要的3个发现**：
1. ⚠️ **量能倍数不是越高越好**：10-30x 是最佳区间，超过30x成功率反而降低
2. ⭐ **进货/出货比率最关键**：>= 5x 时成功率 50%，<= 1.5x 时只有 12%
3. 🎯 **興櫃不应排除**：成功率最高（28.99%），但需要不同的量能倍数门槛（10-25x）

**现有参数的问题**：
- MaxVolumeRatio = 50 太高（应该 30）
- MaxKD = 60 太低（应该 80）
- 缺少进货/出货比率筛选（最关键指标！）
- 缺少按股票类型差异化筛选

**建议优先实施**：
1. 调整 MaxVolumeRatio: 50 → 30
2. 调整 MaxKD: 60 → 80
3. 添加进货/出货比率筛选（>= 2.5）
4. 添加按股票类型差异化筛选

---

**分析完成时间**: 2026-03-07  
**数据来源**: d:\vibeCoding\sst\analyze-alertlist-success-simple.sql
