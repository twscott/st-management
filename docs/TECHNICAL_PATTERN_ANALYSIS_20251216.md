# Technical Pattern Analysis - November 3, 2025 Winners

**Analysis Date**: 2025-12-16  
**Data Source**: 9 stocks achieving 30%+ gain from 2025-11-03  
**Success Rate Baseline**: 16.7% (15/90 recommendations in November)

---

## 🎯 Core Discovery: "跌深反彈" (Deep Pullback Reversal) Strategy

### Critical Insight
**This is NOT a momentum/strong-trend strategy. It's a mean-reversion/oversold-bounce strategy!**

The successful stocks were NOT picking strong uptrends, but rather:
- **Stocks in downtrends** (Bearish MA alignment)
- **Trading below MA20** (oversold levels)
- **With 25-day cooling period** (key discriminator)
- **KD showing reversal signals** (oversold or golden cross)

---

## 📊 Data Summary (9 Winners on 2025-11-03)

### MA Alignment Distribution
| Pattern | Count | Percentage | Interpretation |
|---------|-------|------------|----------------|
| **Bearish** | 6 | 67% | MA5 < MA10 < MA20 (downtrend) |
| Mixed | 1 | 11% | No clear alignment |
| Bullish | 2 | 22% | MA5 > MA10 > MA20 (uptrend) |

**Key Finding**: **67% of winners had BEARISH alignment** (not bullish!)

### Price Position vs MA20
- **Average**: -1.98% below MA20
- **Below MA20**: 6 stocks (67%)
- **Above MA20**: 3 stocks (33%)
- **Range**: -8.16% to +6.44%

**Key Finding**: Most winners were **trading BELOW MA20** at entry point (逢低進場)

### KD Indicator Distribution
| Status | Count | Percentage | Signal |
|--------|-------|------------|--------|
| **Oversold** | 4 | 44% | KD_K < 20 (ready to bounce) |
| **GoldenCross** | 4 | 44% | KD_K > KD_D (buy signal) |
| DeathCross | 1 | 11% | KD_K < KD_D (sell signal - outlier) |

**Key Finding**: **88% had either Oversold or GoldenCross** - both are reversal signals!

### Volume Characteristics
- **Range**: 0.36x to 2.08x of MV20
- **Median**: ~1.0x MV20 (normal volume)
- **Most stocks**: 0.5-1.5x range (NOT heavy volume spikes)
- **Exceptions**: 6548 (1.65x), 6624 (2.08x) showed higher volume

**Key Finding**: No requirement for heavy volume - **moderate volume acceptable**

---

## 🔍 Detailed Stock Analysis

### Group 1: Bearish + Oversold (4 stocks)
Perfect "跌深反彈" candidates:
- **8240**: -4.57% from MA20, KD=6.43 (oversold), Vol=1.01x
- **7715**: -5.24% from MA20, KD=17.07 (oversold), Vol=0.36x  
- **4561**: -2.56% from MA20, KD=14.00 (oversold), Vol=0.71x
- **3322**: -5.29% from MA20, KD=4.31 (oversold), Vol=0.86x

Pattern: Price dropped below MA20, KD extremely low, ready to bounce back

### Group 2: Bearish + GoldenCross (2 stocks)
Starting to reverse:
- **1623**: -1.83% from MA20, KD crossed (20.00>18.31), Vol=0.62x
- **3585**: -8.16% from MA20, KD crossed (26.73>35.39 - DeathCross, outlier)

Pattern: Price still below MA20 but KD starting to turn up

### Group 3: Bullish + GoldenCross (2 stocks)
Already in uptrend:
- **6548**: +6.44% from MA20, KD=88.55 (strong), Vol=1.65x ⚠️
- **6624**: +1.29% from MA20, KD=69.00 (strong), Vol=2.08x ⚠️

Pattern: Price broke above MA20, strong KD momentum

⚠️ Note: These 2 stocks had high KD (69-88), close to overbought. May represent late entries.

### Group 4: Mixed Alignment (1 stock)
- **5274**: +2.06% from MA20, KD=61.74 Golden Cross, Vol=1.24x

---

## 🎯 Recommended Technical Filters

Based on 88% pattern match (Oversold + GoldenCross):

### Filter Set 1: Conservative (Match 67% "Bearish + Oversold/GC" pattern)
```sql
WHERE 
    -- Price below MA20 (pullback condition)
    s60.EndPrice < s60.MA20
    
    -- KD reversal signals
    AND (
        trade.KD_K < 20  -- Oversold (準備反彈)
        OR 
        (trade.KD_K > trade.KD_D AND trade.KD_K < 80)  -- Golden Cross (買入信號)
    )
    
    -- Cooling period = 25 days (強特徵from previous analysis)
    AND CoolingDays = 25
    
    -- Minimum volume (avoid dead stocks)
    AND s60.Vol > s60.MV20 * 0.3
    
    -- Bearish or Mixed MA alignment (downtrend/consolidation)
    AND (s60.MA5 <= s60.MA20)  -- Not strong uptrend
```

### Filter Set 2: Relaxed (Include all 9 patterns)
```sql
WHERE 
    -- KD not in extreme overbought (exclude KD > 90)
    trade.KD_K < 90
    
    -- KD showing buy signal
    AND (
        trade.KD_K < 30  -- Oversold or close
        OR 
        trade.KD_K > trade.KD_D  -- Golden Cross
    )
    
    -- Cooling period = 25 days
    AND CoolingDays = 25
    
    -- Minimum liquidity
    AND s60.Vol > s60.MV20 * 0.3
```

---

## 📈 Expected Impact

### Current Performance (November 2025)
- Total Recommendations: 90 stocks
- Success Rate: 16.7% (15 stocks achieved 20%+)
- Average Max Loss: -39.8%
- Failure Rate: 83.3%

### With Technical Filters (Estimated)
Assuming:
- Filter identifies 40-50 candidates (instead of 90)
- Captures 8-9 of the original 15 winners (89-100% capture rate)
- Filters out 30-40 losers

**Estimated New Success Rate**:
- Success: 8-9 stocks
- Total: 40-50 stocks
- **New Rate: 16-22.5%** (slight improvement)
- **Risk Reduction**: Better quality candidates

---

## 🚀 Implementation Plan

### Phase 1: Add Technical Filters to SmartRecommendationService
Location: `src/SST.StockImport.Core/Services/SmartRecommendationService.cs`

Modify `FindTodayCandidatesAsync()` around line 130-180:
1. Add JOIN with `stock60days` table
2. Add JOIN with `tradedata` table
3. Add WHERE conditions for:
   - Price vs MA20
   - KD indicators
   - Volume requirements

### Phase 2: Re-run November Backtest
Script: `backtest-november-2025.ps1`
- Compare old vs new success rate
- Verify winner capture rate (aim for ≥80%)
- Check if average max loss improves

### Phase 3: December Validation
Script: `backtest-december-2025.ps1`
- Test on out-of-sample data
- Confirm filter generalization
- Adjust thresholds if needed

### Phase 4: Monitor Live Performance
- Deploy to production
- Track January 2026 results
- Iterate on KD thresholds

---

## ⚠️ Caveats & Considerations

### 1. Sample Size Limitation
Only 9 stocks analyzed from single day (2025-11-03). Patterns may not generalize to all 15 November winners.

**Mitigation**: Analyze all 15 winners across November to confirm pattern consistency.

### 2. KD Indicator Accuracy
User mentioned: "你們的 KD 有重算過，跟線上看到的數字不一樣"

**Risk**: If KD calculation differs from standard, filters may not work as expected.

**Mitigation**: 
- Verify KD formula matches industry standard
- Test with alternative thresholds
- Consider using only Price vs MA20 filter initially

### 3. Market Regime Dependency
"跌深反彈" strategy works best in sideways/recovery markets. May fail in strong downtrends.

**Mitigation**: Add market condition check (overall market MA trend)

### 4. Cooling Period Rigidity
ALL 9 winners had exactly 25-day cooling. Too strict filter (=25) may miss good candidates at 24 or 26 days.

**Mitigation**: Test range: 23-27 days in backtest

---

## 📝 Next Steps

- [ ] **Immediate**: Verify pattern across all 15 November winners (not just Nov-3)
- [ ] **Short-term**: Implement Filter Set 2 (relaxed) in SmartRecommendationService
- [ ] **Mid-term**: Run comprehensive backtest (Nov + Dec)
- [ ] **Long-term**: Track live performance and iterate

---

## 📌 Key Takeaway

> **The algorithm is NOT finding "strong stocks going higher".**  
> **It's finding "oversold stocks ready to bounce back".**
>
> This is a **mean-reversion strategy**, not a **momentum strategy**.

Success depends on:
1. ✅ **25-day cooling period** (strongest signal)
2. ✅ **KD Oversold or Golden Cross** (reversal timing)
3. ✅ **Price below MA20** (entry at pullback)
4. ⚠️ **Risk management** (83% still fail - need tight stop-loss!)

---

**Analysis by**: GitHub Copilot AI Agent  
**Review required by**: Human analyst for KD validation & market condition assessment
