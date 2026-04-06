#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
分析成功案例的特徵
檢查均價線(MA)和均量線(MV)對準確率的影響
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
print("📊 分析成功案例特徵")
print("="*80)

# 1. 先檢查 stock60days 表有哪些欄位
print("\n1️⃣ 檢查 stock60days 表結構...")
cursor = conn.cursor()
cursor.execute("SHOW COLUMNS FROM stock60days")
columns = cursor.fetchall()

print("\n可用欄位:")
ma_fields = []
mv_fields = []
for col in columns:
    col_name = col[0]
    print(f"  - {col_name:30s} {col[1]}")
    if col_name.upper().startswith('MA'):
        ma_fields.append(col_name)
    if col_name.upper().startswith('MV') or col_name.upper().startswith('VOL'):
        mv_fields.append(col_name)

print(f"\n找到均價線欄位: {ma_fields}")
print(f"找到均量線欄位: {mv_fields}")

# 2. 分析大陽線成功案例 (Conservative配置: KD=40-85, Cooling=15-35)
print("\n" + "="*80)
print("2️⃣ 分析大陽線成功 vs 失敗案例特徵")
print("="*80)

TARGET_GAIN = 20
TRACKING_DAYS = 20
three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

print(f"\n配置: 20% gain in 20 days, KD=40-85, Cooling=15-35")
print(f"數據範圍: {three_months_ago} to {tracking_cutoff}\n")

# 構建查詢 - 包含均價線和均量線
ma_fields_str = ', '.join([f's60.{f}' for f in ma_fields]) if ma_fields else ''
mv_fields_str = ', '.join([f's60.{f}' for f in mv_fields]) if mv_fields else ''

additional_fields = []
if ma_fields_str:
    additional_fields.append(ma_fields_str)
if mv_fields_str:
    additional_fields.append(mv_fields_str)

fields_sql = ', ' + ', '.join(additional_fields) if additional_fields else ''

query = f"""
SELECT 
    t.StockID,
    t.StockName,
    t.TransDate as EventDate,
    s60.StockDate as RecommendDate,
    s60.EndPrice as EntryPrice,
    s60.KD_K,
    s60.KD_D,
    t.StockDiffRate,
    t.Vol as EventVolume
    {fields_sql},
    (
        SELECT MAX(future.HPrice)
        FROM tradedata future
        WHERE future.StockID = t.StockID
          AND future.TransDate > s60.StockDate
          AND DATEDIFF(future.TransDate, s60.StockDate) <= {TRACKING_DAYS}
    ) as MaxPriceIn20Days
FROM tradedata t
INNER JOIN stock60days s60 
    ON s60.StockID = t.StockID
WHERE t.StockDiffRate >= 6.0
  AND t.Vol >= 1000
  AND DATEDIFF(s60.StockDate, t.TransDate) BETWEEN 15 AND 35
  AND s60.KD_K BETWEEN 40 AND 85
  AND s60.StockDate >= '{three_months_ago}'
  AND s60.StockDate <= '{tracking_cutoff}'
LIMIT 5000
"""

print("執行查詢... (可能需要1-2分鐘)")
df = pd.read_sql(query, conn)

print(f"✅ 查詢完成，共 {len(df)} 筆記錄\n")

# 計算是否成功
df['TargetPrice'] = df['EntryPrice'] * (1 + TARGET_GAIN/100)
df['Success'] = df['MaxPriceIn20Days'] >= df['TargetPrice']
df['ActualGain'] = ((df['MaxPriceIn20Days'] - df['EntryPrice']) / df['EntryPrice'] * 100).round(2)

success_df = df[df['Success'] == True]
fail_df = df[df['Success'] == False]

success_count = len(success_df)
total_count = len(df)
accuracy = (success_count / total_count * 100) if total_count > 0 else 0

print(f"總計: {total_count} 筆推薦")
print(f"成功: {success_count} 筆 ({accuracy:.1f}%)")
print(f"失敗: {len(fail_df)} 筆 ({100-accuracy:.1f}%)")

# 3. 分析均價線和均量線特徵
print("\n" + "="*80)
print("3️⃣ 成功 vs 失敗案例的技術指標對比")
print("="*80)

def analyze_features(success_df, fail_df, feature_name):
    """分析某個特徵在成功和失敗案例中的差異"""
    if feature_name not in success_df.columns:
        return
    
    # 移除 None/NaN 值
    success_values = success_df[feature_name].dropna()
    fail_values = fail_df[feature_name].dropna()
    
    if len(success_values) == 0 or len(fail_values) == 0:
        return
    
    print(f"\n{feature_name}:")
    print(f"  成功案例: 平均={success_values.mean():.2f}, 中位數={success_values.median():.2f}, 標準差={success_values.std():.2f}")
    print(f"  失敗案例: 平均={fail_values.mean():.2f}, 中位數={fail_values.median():.2f}, 標準差={fail_values.std():.2f}")
    print(f"  差異: {(success_values.mean() - fail_values.mean()):.2f} ({((success_values.mean() / fail_values.mean() - 1) * 100):.1f}%)")

# 分析基本指標
print("\n📈 基本技術指標:")
analyze_features(success_df, fail_df, 'KD_K')
analyze_features(success_df, fail_df, 'KD_D')
analyze_features(success_df, fail_df, 'StockDiffRate')
analyze_features(success_df, fail_df, 'EventVolume')

# 分析均價線
if ma_fields:
    print("\n📊 均價線 (MA):")
    for field in ma_fields:
        if field in df.columns:
            analyze_features(success_df, fail_df, field)

# 分析均量線
if mv_fields:
    print("\n📊 均量線 (MV/Vol):")
    for field in mv_fields:
        if field in df.columns:
            analyze_features(success_df, fail_df, field)

# 4. 檢查價格與均價線的關係
print("\n" + "="*80)
print("4️⃣ 價格與均價線關係分析")
print("="*80)

if 'MA5' in df.columns and 'MA10' in df.columns and 'MA20' in df.columns:
    # 計算推薦時的價格相對於均線的位置
    df['Price_vs_MA5'] = ((df['EntryPrice'] - df['MA5']) / df['MA5'] * 100).round(2)
    df['Price_vs_MA10'] = ((df['EntryPrice'] - df['MA10']) / df['MA10'] * 100).round(2)
    df['Price_vs_MA20'] = ((df['EntryPrice'] - df['MA20']) / df['MA20'] * 100).round(2)
    
    # 判斷多頭/空頭排列
    df['Bullish_MA'] = (df['MA5'] > df['MA10']) & (df['MA10'] > df['MA20'])
    
    success_df = df[df['Success'] == True]
    fail_df = df[df['Success'] == False]
    
    print("\n推薦時價格相對均線位置:")
    analyze_features(success_df, fail_df, 'Price_vs_MA5')
    analyze_features(success_df, fail_df, 'Price_vs_MA10')
    analyze_features(success_df, fail_df, 'Price_vs_MA20')
    
    # 多頭排列比例
    bullish_success = success_df['Bullish_MA'].sum()
    bullish_fail = fail_df['Bullish_MA'].sum()
    print(f"\n多頭排列 (MA5>MA10>MA20):")
    print(f"  成功案例: {bullish_success}/{len(success_df)} ({bullish_success/len(success_df)*100:.1f}%)")
    print(f"  失敗案例: {bullish_fail}/{len(fail_df)} ({bullish_fail/len(fail_df)*100:.1f}%)")
else:
    print("\n⚠️ 缺少 MA5/MA10/MA20 欄位，無法分析")

# 5. 檢查成交量與均量線的關係
print("\n" + "="*80)
print("5️⃣ 成交量與均量線關係分析")
print("="*80)

# 檢查是否有 Vol_MA5 等欄位
vol_ma_fields = [f for f in df.columns if 'vol' in f.lower() and 'ma' in f.lower()]

if vol_ma_fields:
    print(f"\n找到均量線欄位: {vol_ma_fields}")
    for field in vol_ma_fields:
        analyze_features(success_df, fail_df, field)
else:
    print("\n⚠️ 未找到均量線欄位 (如 Vol_MA5)")

# 6. 建議的篩選條件
print("\n" + "="*80)
print("6️⃣ 建議的改進措施")
print("="*80)

recommendations = []

# 基於 KD 值的建議
if 'KD_K' in success_df.columns and len(success_df) > 0:
    avg_kd_success = success_df['KD_K'].mean()
    avg_kd_fail = fail_df['KD_K'].mean()
    if avg_kd_success > avg_kd_fail + 5:
        recommendations.append(f"✅ 提高 KD 下限至 {int(success_df['KD_K'].quantile(0.25))} 以上")
    elif avg_kd_success < avg_kd_fail - 5:
        recommendations.append(f"✅ 降低 KD 下限至 {int(success_df['KD_K'].quantile(0.25))} 左右")

# 基於均線的建議
if 'Price_vs_MA5' in df.columns:
    success_df = df[df['Success'] == True]
    above_ma5_success = (success_df['Price_vs_MA5'] > 0).sum() / len(success_df) * 100
    above_ma5_fail = (fail_df['Price_vs_MA5'] > 0).sum() / len(fail_df) * 100
    
    if above_ma5_success > above_ma5_fail + 10:
        recommendations.append(f"✅ 加入條件: 推薦價 > MA5 (成功率差異 {above_ma5_success - above_ma5_fail:.1f}%)")
    elif above_ma5_success < above_ma5_fail - 10:
        recommendations.append(f"✅ 加入條件: 推薦價 < MA5 (成功率差異 {above_ma5_fail - above_ma5_success:.1f}%)")

# 基於多頭排列的建議
if 'Bullish_MA' in df.columns:
    bullish_success_rate = bullish_success / len(success_df) * 100 if len(success_df) > 0 else 0
    bullish_fail_rate = bullish_fail / len(fail_df) * 100 if len(fail_df) > 0 else 0
    
    if bullish_success_rate > bullish_fail_rate + 10:
        recommendations.append(f"✅ 加入條件: 多頭排列 (MA5>MA10>MA20) - 提升 {bullish_success_rate - bullish_fail_rate:.1f}%")

print("\n建議措施:")
if recommendations:
    for i, rec in enumerate(recommendations, 1):
        print(f"  {i}. {rec}")
else:
    print("  ⚠️ 暫無明確建議，需要更多數據分析")

# 7. 顯示幾個成功案例供參考
print("\n" + "="*80)
print("7️⃣ 成功案例範例 (Top 5)")
print("="*80)

if len(success_df) > 0:
    top_success = success_df.nlargest(5, 'ActualGain')
    display_cols = ['StockID', 'StockName', 'RecommendDate', 'EntryPrice', 'MaxPriceIn20Days', 
                    'ActualGain', 'KD_K', 'KD_D']
    
    # 加入 MA 欄位
    if 'MA5' in top_success.columns:
        display_cols.extend(['MA5', 'MA10', 'MA20'])
    
    print("\n" + top_success[display_cols].to_string(index=False))

cursor.close()
conn.close()

print("\n" + "="*80)
print("✅ 分析完成")
print("="*80)
