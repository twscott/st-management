#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
測試不同 MA 條件的效果
找出最佳的均價線篩選邏輯
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
print("📊 測試不同 MA 條件")
print("="*80)

TARGET_GAIN = 20
TRACKING_DAYS = 20
three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

print(f"目標: {TARGET_GAIN}% gain in {TRACKING_DAYS} days")
print(f"數據範圍: {three_months_ago} to {tracking_cutoff}\n")

# 基礎配置 (最佳的 MV10 + KD_D)
BASE_KD_K = (40, 85)
BASE_COOLING = (15, 35)
BASE_MV_CONDITION = "s60.Vol >= s60.MV10 * 0.7"
BASE_KD_D_CONDITION = "s60.KD_D >= 60"

# 測試不同的 MA 條件
TEST_CONFIGS = [
    {
        'name': '基準 (MV10+KD_D)',
        'ma_condition': None,
    },
    {
        'name': 'MA5 ±3%',
        'ma_condition': 'ABS((s60.EndPrice - s60.MA5) / s60.MA5 * 100) <= 3',
    },
    {
        'name': 'MA5 ±5%',
        'ma_condition': 'ABS((s60.EndPrice - s60.MA5) / s60.MA5 * 100) <= 5',
    },
    {
        'name': 'MA5 ±10%',
        'ma_condition': 'ABS((s60.EndPrice - s60.MA5) / s60.MA5 * 100) <= 10',
    },
    {
        'name': '價格 > MA5',
        'ma_condition': 's60.EndPrice > s60.MA5',
    },
    {
        'name': '價格 > MA5 * 0.98',
        'ma_condition': 's60.EndPrice > s60.MA5 * 0.98',
    },
    {
        'name': '價格 > MA5 * 1.02',
        'ma_condition': 's60.EndPrice > s60.MA5 * 1.02',
    },
    {
        'name': '價格介於 MA5-MA10',
        'ma_condition': 's60.EndPrice >= s60.MA10 AND s60.EndPrice <= s60.MA5',
    },
    {
        'name': '多頭排列 (MA5>MA10>MA20)',
        'ma_condition': 's60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20',
    },
    {
        'name': '多頭排列 + 價格>MA5',
        'ma_condition': 's60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20 AND s60.EndPrice > s60.MA5',
    },
    {
        'name': '價格突破MA10',
        'ma_condition': 's60.EndPrice > s60.MA10',
    },
    {
        'name': '價格突破MA20',
        'ma_condition': 's60.EndPrice > s60.MA20',
    },
    {
        'name': '價格在MA5-MA20之間',
        'ma_condition': 's60.EndPrice >= s60.MA20 AND s60.EndPrice <= s60.MA5',
    },
    {
        'name': 'MA5向上 (MA5>MA10)',
        'ma_condition': 's60.MA5 > s60.MA10',
    },
    {
        'name': '價格>MA5且MA5向上',
        'ma_condition': 's60.EndPrice > s60.MA5 AND s60.MA5 > s60.MA10',
    },
]

results = []

for config in TEST_CONFIGS:
    print(f"\n{'='*80}")
    print(f"測試: {config['name']}")
    print(f"{'='*80}")
    
    kd_min, kd_max = BASE_KD_K
    cooling_min, cooling_max = BASE_COOLING
    
    # 構建 SQL 條件
    where_conditions = [
        "t.StockDiffRate >= 6.0",
        "t.Vol >= 1000",
        f"DATEDIFF(s60.StockDate, t.TransDate) BETWEEN {cooling_min} AND {cooling_max}",
        f"s60.KD_K BETWEEN {kd_min} AND {kd_max}",
        f"s60.StockDate >= '{three_months_ago}'",
        f"s60.StockDate <= '{tracking_cutoff}'",
        BASE_MV_CONDITION,
        BASE_KD_D_CONDITION,
    ]
    
    # 加入 MA 條件
    if config['ma_condition']:
        where_conditions.append(config['ma_condition'])
        print(f"  ✓ MA條件: {config['ma_condition']}")
    
    where_clause = " AND ".join(where_conditions)
    
    # 執行查詢
    query = f"""
    SELECT 
        t.StockID,
        t.StockName,
        s60.StockDate as RecommendDate,
        s60.EndPrice as EntryPrice,
        s60.MA5,
        s60.MA10,
        s60.MA20,
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
            'accuracy': 0.0,
            'empty_rate': 100.0
        })
        continue
    
    # 計算成功率
    df['TargetPrice'] = df['EntryPrice'] * (1 + TARGET_GAIN/100)
    df['Success'] = df['MaxPriceIn20Days'] >= df['TargetPrice']
    
    success_count = df['Success'].sum()
    total_count = len(df)
    accuracy = (success_count / total_count * 100) if total_count > 0 else 0
    
    # 計算空檔率 (推薦數減少的比例)
    baseline_count = 4148  # 基準配置的推薦數
    empty_rate = (1 - total_count / baseline_count) * 100 if baseline_count > 0 else 0
    
    print(f"  📊 推薦數: {total_count} (空檔率: {empty_rate:.1f}%)")
    print(f"  ✅ 成功: {success_count} ({accuracy:.1f}%)")
    print(f"  ❌ 失敗: {total_count - success_count}")
    
    # 顯示幾個案例
    if len(df) > 0 and success_count > 0:
        avg_gain = df[df['Success']]['MaxPriceIn20Days'] / df[df['Success']]['EntryPrice'] * 100 - 100
        print(f"  💰 成功案例平均漲幅: {avg_gain.mean():.1f}%")
    
    results.append({
        'config': config['name'],
        'total': total_count,
        'success': success_count,
        'accuracy': accuracy,
        'empty_rate': empty_rate
    })

# 顯示對比結果
print("\n" + "="*80)
print("📊 結果對比")
print("="*80)

results_df = pd.DataFrame(results)
results_df = results_df.sort_values('accuracy', ascending=False)

# 加入得分計算 (準確率 - 空檔率懲罰)
# 如果空檔率 > 40%，扣分
results_df['score'] = results_df['accuracy'] - results_df['empty_rate'].apply(lambda x: max(0, x - 40) * 0.5)
results_df = results_df.sort_values('score', ascending=False)

print("\n按綜合得分排序 (準確率 - 空檔懲罰):")
print(results_df[['config', 'accuracy', 'total', 'success', 'empty_rate', 'score']].to_string(index=False))

# 找出最佳配置
print("\n" + "="*80)
print("🏆 推薦配置")
print("="*80)

# 方案1: 最高準確率
best_accuracy = results_df.iloc[0]
print(f"\n方案1 - 最高準確率:")
print(f"  名稱: {best_accuracy['config']}")
print(f"  準確率: {best_accuracy['accuracy']:.1f}%")
print(f"  推薦數: {best_accuracy['total']}")
print(f"  空檔率: {best_accuracy['empty_rate']:.1f}%")

# 方案2: 準確率 > 27% 且空檔率 < 40%
balanced = results_df[(results_df['accuracy'] >= 27) & (results_df['empty_rate'] < 40)]
if len(balanced) > 0:
    best_balanced = balanced.iloc[0]
    print(f"\n方案2 - 平衡方案 (準確率≥27% 且 空檔率<40%):")
    print(f"  名稱: {best_balanced['config']}")
    print(f"  準確率: {best_balanced['accuracy']:.1f}%")
    print(f"  推薦數: {best_balanced['total']}")
    print(f"  空檔率: {best_balanced['empty_rate']:.1f}%")
else:
    print(f"\n方案2 - 無符合條件的配置 (準確率≥27% 且 空檔率<40%)")

# 方案3: 推薦基準配置的改進
baseline_accuracy = results_df[results_df['config'] == '基準 (MV10+KD_D)']['accuracy'].values[0]
improved = results_df[(results_df['accuracy'] > baseline_accuracy) & (results_df['empty_rate'] < 50)]
if len(improved) > 0:
    print(f"\n方案3 - 比基準更好的配置:")
    for idx, row in improved.head(3).iterrows():
        improvement = row['accuracy'] - baseline_accuracy
        print(f"  • {row['config']}: {row['accuracy']:.1f}% (+{improvement:.1f}%), 空檔率 {row['empty_rate']:.1f}%")
else:
    print(f"\n方案3 - 無比基準更好的配置")

conn.close()

print("\n" + "="*80)
print("✅ 分析完成")
print("="*80)
