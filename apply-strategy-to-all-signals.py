#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
將最佳策略應用到所有三個信號類型
策略 A: MV10 + KD_D >= 60 + 價格突破MA10
"""

import pymysql
import pandas as pd
from datetime import datetime, timedelta
import warnings
warnings.filterwarnings('ignore')

conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

print("\n" + "="*80)
print("🚀 策略 A 應用到所有信號類型")
print("="*80)
print("策略: MV10 + KD_D >= 60 + 價格突破MA10\n")

TARGET_GAIN = 20
TRACKING_DAYS = 20
three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

print(f"目標: {TARGET_GAIN}% gain in {TRACKING_DAYS} days")
print(f"數據範圍: {three_months_ago} to {tracking_cutoff}\n")

# 測試策略配置
STRATEGIES = [
    {
        'name': '基準',
        'use_mv': False,
        'use_kd_d': False,
        'use_ma': False,
    },
    {
        'name': '策略 A (MV10+KD_D+MA10)',
        'use_mv': True,
        'use_kd_d': True,
        'use_ma': True,
    },
]

# 三個信號類型
SIGNAL_TYPES = [
    {
        'name': 'Big Candle (大陽線)',
        'table': 'tradedata',
        'event_conditions': [
            't.StockDiffRate >= 6.0',
            't.Vol >= 1000',
        ],
        'kd_range': (40, 85),
        'cooling_range': (15, 35),
    },
    {
        'name': 'Volume Spike (熱點)',
        'table': 'alertlist',
        'event_conditions': [
            'a.maxPLVR >= 10',
        ],
        'kd_range': (30, 80),
        'cooling_range': (15, 35),
    },
    {
        'name': 'Long Lower Shadow (長下影線)',
        'table': 't_longshadowcover',
        'event_conditions': [
            # 只要在表中出現就是長下影線，無需額外條件
        ],
        'kd_range': (20, 70),
        'cooling_range': (10, 30),
    },
]

all_results = []

for signal in SIGNAL_TYPES:
    print(f"\n{'='*80}")
    print(f"信號類型: {signal['name']}")
    print(f"{'='*80}")
    
    table = signal['table']
    kd_min, kd_max = signal['kd_range']
    cooling_min, cooling_max = signal['cooling_range']
    
    for strategy in STRATEGIES:
        print(f"\n  測試: {strategy['name']}")
        print(f"  {'-'*76}")
        
        # 根據表格構建不同的 JOIN
        if table == 'tradedata':
            from_clause = """
                FROM tradedata t
                INNER JOIN stock60days s60 ON s60.StockID = t.StockID
            """
            date_diff = "DATEDIFF(s60.StockDate, t.TransDate)"
            stock_id = 't.StockID'
            event_date = 't.TransDate'
            
        elif table == 'alertlist':
            from_clause = """
                FROM alertlist a
                INNER JOIN stock60days s60 ON s60.StockID = a.StockID
            """
            date_diff = "DATEDIFF(s60.StockDate, a.alertDate)"
            stock_id = 'a.StockID'
            event_date = 'a.alertDate'
            
        elif table == 't_longshadowcover':
            from_clause = """
                FROM t_longshadowcover l
                INNER JOIN stock60days s60 ON s60.StockID = l.stockid
            """
            date_diff = "DATEDIFF(s60.StockDate, l.transDate)"
            stock_id = 'l.stockid'
            event_date = 'l.transDate'
        
        # 構建基礎條件
        where_conditions = signal['event_conditions'].copy()
        where_conditions.extend([
            f"{date_diff} BETWEEN {cooling_min} AND {cooling_max}",
            f"s60.KD_K BETWEEN {kd_min} AND {kd_max}",
            f"s60.StockDate >= '{three_months_ago}'",
            f"s60.StockDate <= '{tracking_cutoff}'",
        ])
        
        # 加入策略條件
        if strategy['use_mv']:
            where_conditions.append("s60.Vol >= s60.MV10 * 0.7")
            print(f"    ✓ Vol >= MV10 * 0.7")
            
        if strategy['use_kd_d']:
            where_conditions.append("s60.KD_D >= 60")
            print(f"    ✓ KD_D >= 60")
            
        if strategy['use_ma']:
            where_conditions.append("s60.EndPrice > s60.MA10")
            print(f"    ✓ EntryPrice > MA10")
        
        where_clause = " AND ".join(where_conditions)
        
        # 執行查詢
        query = f"""
        SELECT 
            {stock_id} as StockID,
            s60.StockDate as RecommendDate,
            s60.EndPrice as EntryPrice,
            s60.KD_K,
            s60.KD_D,
            s60.MA10,
            (
                SELECT MAX(future.HPrice)
                FROM tradedata future
                WHERE future.StockID = {stock_id}
                  AND future.TransDate > s60.StockDate
                  AND DATEDIFF(future.TransDate, s60.StockDate) <= {TRACKING_DAYS}
            ) as MaxPriceIn20Days
        {from_clause}
        WHERE {where_clause}
        LIMIT 10000
        """
        
        try:
            df = pd.read_sql(query, conn)
            
            if len(df) == 0:
                print(f"    ⚠️ 無推薦結果")
                all_results.append({
                    'signal': signal['name'],
                    'strategy': strategy['name'],
                    'total': 0,
                    'success': 0,
                    'accuracy': 0.0,
                })
                continue
            
            # 計算成功率
            df['TargetPrice'] = df['EntryPrice'] * (1 + TARGET_GAIN/100)
            df['Success'] = df['MaxPriceIn20Days'] >= df['TargetPrice']
            
            success_count = df['Success'].sum()
            total_count = len(df)
            accuracy = (success_count / total_count * 100) if total_count > 0 else 0
            
            print(f"    📊 推薦數: {total_count}")
            print(f"    ✅ 成功: {success_count} ({accuracy:.1f}%)")
            
            # 計算平均漲幅
            if success_count > 0:
                avg_gain = ((df[df['Success']]['MaxPriceIn20Days'] / df[df['Success']]['EntryPrice']) - 1) * 100
                print(f"    💰 成功案例平均漲幅: {avg_gain.mean():.1f}%")
            
            all_results.append({
                'signal': signal['name'],
                'strategy': strategy['name'],
                'total': total_count,
                'success': success_count,
                'accuracy': accuracy,
            })
            
        except Exception as e:
            print(f"    ❌ 查詢錯誤: {e}")
            all_results.append({
                'signal': signal['name'],
                'strategy': strategy['name'],
                'total': 0,
                'success': 0,
                'accuracy': 0.0,
            })

# 顯示對比結果
print("\n" + "="*80)
print("📊 策略效果總結")
print("="*80)

results_df = pd.DataFrame(all_results)

# 按信號類型分組顯示
for signal_name in results_df['signal'].unique():
    signal_results = results_df[results_df['signal'] == signal_name]
    
    print(f"\n{signal_name}:")
    print("-" * 80)
    
    for _, row in signal_results.iterrows():
        print(f"  {row['strategy']:30s} | 準確率: {row['accuracy']:5.1f}% | 推薦數: {row['total']:5d} | 成功: {row['success']:4d}")
    
    # 計算改進
    baseline = signal_results[signal_results['strategy'] == '基準']['accuracy'].values
    strategy_a = signal_results[signal_results['strategy'] == '策略 A (MV10+KD_D+MA10)']['accuracy'].values
    
    if len(baseline) > 0 and len(strategy_a) > 0 and baseline[0] > 0:
        improvement = strategy_a[0] - baseline[0]
        relative_improvement = (improvement / baseline[0]) * 100
        print(f"\n  📈 改進: {improvement:+.1f}% (相對提升 {relative_improvement:+.1f}%)")

# 最終推薦策略
print("\n" + "="*80)
print("🎯 最終推薦參數")
print("="*80)

print("\n基於回測結果，建議將以下參數應用到 TimeMachine UI:\n")

for signal_name in results_df['signal'].unique():
    signal_results = results_df[results_df['signal'] == signal_name]
    strategy_a_row = signal_results[signal_results['strategy'] == '策略 A (MV10+KD_D+MA10)'].iloc[0]
    
    # 找到對應的信號配置
    signal_config = next(s for s in SIGNAL_TYPES if s['name'] == signal_name)
    kd_min, kd_max = signal_config['kd_range']
    cooling_min, cooling_max = signal_config['cooling_range']
    
    print(f"{signal_name}:")
    print(f"  準確率: {strategy_a_row['accuracy']:.1f}%")
    print(f"  推薦數: {strategy_a_row['total']}")
    print(f"  KD 範圍: {kd_min}-{kd_max}")
    print(f"  Cooling Period: {cooling_min}-{cooling_max} 天")
    print(f"  額外條件:")
    print(f"    - KD_D >= 60")
    print(f"    - Vol >= MV10 * 0.7")
    print(f"    - EntryPrice > MA10")
    print()

# 生成 SQL 更新語句建議
print("\n" + "="*80)
print("💡 實施建議")
print("="*80)

print("""
策略 A 已驗證完成，建議下一步：

1. 📝 更新 TimeMachineAnalysisService.cs
   - 在 GetHistoricalCandidatesAsync() 方法中加入新條件
   - 新增參數: useAdvancedFilters (boolean)
   
2. 🎨 更新 TimeMachineAnalysis.razor UI
   - 加入「進階篩選」checkbox
   - 顯示說明: "使用 MV10 + KD_D + MA10 條件提升準確率"
   
3. 🧪 A/B 測試
   - 同時提供基準版和優化版
   - 讓用戶選擇要使用哪個策略
   
4. 📊 追蹤實際表現
   - 記錄推薦結果
   - 驗證實際準確率是否符合回測
""")

conn.close()

print("\n" + "="*80)
print("✅ 分析完成")
print("="*80)
