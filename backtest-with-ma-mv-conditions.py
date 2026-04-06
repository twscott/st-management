#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
改進版回測 - 加入均量線和均價線條件
基於成功案例特徵分析的結果
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
print("🚀 改進版回測 - 加入 MA/MV 條件")
print("="*80)
print("基於成功案例特徵分析的結果\n")

TARGET_GAIN = 20
TRACKING_DAYS = 20
three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

print(f"目標: {TARGET_GAIN}% gain in {TRACKING_DAYS} days")
print(f"數據範圍: {three_months_ago} to {tracking_cutoff}\n")

# 測試配置
TEST_CONFIGS = [
    {
        'name': '基準 (原始)',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': False,
        'use_ma': False,
        'use_kd_d': False,
    },
    {
        'name': '加入 MV5 條件',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': True,
        'mv_field': 'MV5',
        'mv_ratio': 0.8,
        'use_ma': False,
        'use_kd_d': False,
    },
    {
        'name': '加入 MV10 條件',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': True,
        'mv_field': 'MV10',
        'mv_ratio': 0.7,
        'use_ma': False,
        'use_kd_d': False,
    },
    {
        'name': '加入 MA5 接近條件',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': False,
        'use_ma': True,
        'ma_threshold': 2.0,  # 2%以內
        'use_kd_d': False,
    },
    {
        'name': '加入 KD_D >= 60',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': False,
        'use_ma': False,
        'use_kd_d': True,
        'kd_d_min': 60,
    },
    {
        'name': '組合 MV10 + MA5',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': True,
        'mv_field': 'MV10',
        'mv_ratio': 0.7,
        'use_ma': True,
        'ma_threshold': 2.0,
        'use_kd_d': False,
    },
    {
        'name': '組合 MV10 + KD_D',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': True,
        'mv_field': 'MV10',
        'mv_ratio': 0.7,
        'use_ma': False,
        'use_kd_d': True,
        'kd_d_min': 60,
    },
    {
        'name': '全部條件',
        'kd_k': (40, 85),
        'cooling': (15, 35),
        'use_mv': True,
        'mv_field': 'MV10',
        'mv_ratio': 0.7,
        'use_ma': True,
        'ma_threshold': 2.0,
        'use_kd_d': True,
        'kd_d_min': 60,
    },
]

results = []

for config in TEST_CONFIGS:
    print(f"\n{'='*80}")
    print(f"測試配置: {config['name']}")
    print(f"{'='*80}")
    
    kd_min, kd_max = config['kd_k']
    cooling_min, cooling_max = config['cooling']
    
    # 構建 SQL 條件
    where_conditions = [
        "t.StockDiffRate >= 6.0",
        "t.Vol >= 1000",
        f"DATEDIFF(s60.StockDate, t.TransDate) BETWEEN {cooling_min} AND {cooling_max}",
        f"s60.KD_K BETWEEN {kd_min} AND {kd_max}",
        f"s60.StockDate >= '{three_months_ago}'",
        f"s60.StockDate <= '{tracking_cutoff}'"
    ]
    
    # 加入 MV 條件
    if config.get('use_mv'):
        mv_field = config['mv_field']
        mv_ratio = config['mv_ratio']
        where_conditions.append(f"s60.Vol >= s60.{mv_field} * {mv_ratio}")
        print(f"  ✓ 條件: Vol >= {mv_field} * {mv_ratio}")
    
    # 加入 MA 條件
    if config.get('use_ma'):
        ma_threshold = config['ma_threshold']
        where_conditions.append(f"ABS((s60.EndPrice - s60.MA5) / s60.MA5 * 100) <= {ma_threshold}")
        print(f"  ✓ 條件: EntryPrice 接近 MA5 (±{ma_threshold}%)")
    
    # 加入 KD_D 條件
    if config.get('use_kd_d'):
        kd_d_min = config['kd_d_min']
        where_conditions.append(f"s60.KD_D >= {kd_d_min}")
        print(f"  ✓ 條件: KD_D >= {kd_d_min}")
    
    where_clause = " AND ".join(where_conditions)
    
    # 執行查詢
    query = f"""
    SELECT 
        t.StockID,
        s60.StockDate as RecommendDate,
        s60.EndPrice as EntryPrice,
        (
            SELECT MAX(future.HPrice)
            FROM tradedata future
            WHERE future.StockID = t.StockID
              AND future.TransDate > s60.StockDate
              AND DATEDIFF(future.TransDate, s60.StockDate) <= {TRACKING_DAYS}
        ) as MaxPriceIn20Days
    FROM tradedata t
    INNER JOIN stock60days s60 ON s60.StockID = t.StockID
    WHERE {where_clause}
    LIMIT 10000
    """
    
    df = pd.read_sql(query, conn)
    
    if len(df) == 0:
        print(f"  ⚠️ 無推薦結果")
        results.append({
            'config': config['name'],
            'total': 0,
            'success': 0,
            'accuracy': 0.0
        })
        continue
    
    # 計算成功率
    df['TargetPrice'] = df['EntryPrice'] * (1 + TARGET_GAIN/100)
    df['Success'] = df['MaxPriceIn20Days'] >= df['TargetPrice']
    
    success_count = df['Success'].sum()
    total_count = len(df)
    accuracy = (success_count / total_count * 100) if total_count > 0 else 0
    
    print(f"  📊 推薦數: {total_count}")
    print(f"  ✅ 成功: {success_count} ({accuracy:.1f}%)")
    print(f"  ❌ 失敗: {total_count - success_count} ({100-accuracy:.1f}%)")
    
    results.append({
        'config': config['name'],
        'total': total_count,
        'success': success_count,
        'accuracy': accuracy
    })

# 顯示對比結果
print("\n" + "="*80)
print("📊 結果對比")
print("="*80)

results_df = pd.DataFrame(results)
results_df = results_df.sort_values('accuracy', ascending=False)

print("\n" + results_df.to_string(index=False))

# 找出最佳配置
best_config = results_df.iloc[0]
print("\n" + "="*80)
print("🏆 最佳配置")
print("="*80)
print(f"  名稱: {best_config['config']}")
print(f"  準確率: {best_config['accuracy']:.1f}%")
print(f"  推薦數: {best_config['total']}")
print(f"  成功數: {best_config['success']}")

# 計算改進幅度
baseline = results_df[results_df['config'] == '基準 (原始)']['accuracy'].values[0]
improvement = best_config['accuracy'] - baseline

print(f"\n  📈 相較基準改進: {improvement:+.1f}%")
print(f"  📊 相對提升: {(improvement/baseline*100):.1f}%")

conn.close()

print("\n" + "="*80)
print("✅ 分析完成")
print("="*80)
