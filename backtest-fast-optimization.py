"""
时光机快速优化 - 针对三种信号类型找出最佳参数
策略：使用合理的参数范围，快速找出最佳配置
目标：准确率最高 + 21天内30%获利
"""

import pymysql
import pandas as pd
import numpy as np
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
print("TIME MACHINE FAST OPTIMIZATION - Smart Parameter Search")
print("="*80)
print("Goal: Find best settings for each signal type")
print("Target: Highest accuracy, 30% profit in 21 days\n")

# === 配置 ===
TARGET_GAIN = 30
TRACKING_DAYS = 21
MIN_SAMPLE_SIZE = 8  # 降低门槛以获得更多数据

# === 获取交易日期 ===
print("Loading trading dates...")
cursor = conn.cursor()

three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

cursor.execute(f"""
    SELECT DISTINCT StockDate
    FROM stock60days
    WHERE StockDate >= '{three_months_ago}'
      AND StockDate <= '{tracking_cutoff}'
    ORDER BY StockDate
""")

test_dates = [row[0].strftime('%Y-%m-%d') for row in cursor.fetchall()]
print(f"Testing on {len(test_dates)} trading days: {test_dates[0]} to {test_dates[-1]}\n")

# === 预设合理参数（基于经验） ===
PARAM_CONFIGS = {
    'alertlist': {
        'name': 'Volume Spike (热点)',
        'test_sets': [
            # KD范围, 冷却天数, 成交量倍数, 成熟度
            {'kd': (30, 70), 'cooling': (10, 30), 'volume': (10, 40), 'maturity': 60},
            {'kd': (40, 80), 'cooling': (15, 35), 'volume': (15, 50), 'maturity': 65},
            {'kd': (20, 60), 'cooling': (8, 25), 'volume': (10, 30), 'maturity': 55},
            {'kd': (30, 80), 'cooling': (10, 40), 'volume': (10, 50), 'maturity': 60},
            {'kd': (20, 70), 'cooling': (5, 20), 'volume': (10, 30), 'maturity': 50},
            {'kd': (40, 90), 'cooling': (15, 40), 'volume': (15, 60), 'maturity': 65},
            {'kd': (30, 80), 'cooling': (8, 30), 'volume': (10, 40), 'maturity': 55},
            {'kd': (20, 80), 'cooling': (10, 35), 'volume': (10, 50), 'maturity': 60},
        ]
    },
    'tradedata': {
        'name': 'Big Candle (大阳线)',
        'test_sets': [
            {'kd': (30, 70), 'cooling': (10, 30), 'maturity': 65},
            {'kd': (40, 80), 'cooling': (15, 35), 'maturity': 70},
            {'kd': (20, 70), 'cooling': (8, 25), 'maturity': 60},
            {'kd': (30, 80), 'cooling': (10, 40), 'maturity': 65},
            {'kd': (20, 60), 'cooling': (5, 20), 'maturity': 55},
            {'kd': (40, 90), 'cooling': (15, 40), 'maturity': 70},
            {'kd': (30, 75), 'cooling': (8, 30), 'maturity': 60},
            {'kd': (20, 80), 'cooling': (10, 35), 'maturity': 65},
        ]
    },
    't_longshadowcover': {
        'name': 'Long Lower Shadow (长下影线)',
        'test_sets': [
            {'kd': (30, 70), 'cooling': (10, 30), 'maturity': 60},
            {'kd': (40, 80), 'cooling': (15, 35), 'maturity': 65},
            {'kd': (20, 70), 'cooling': (8, 25), 'maturity': 55},
            {'kd': (30, 80), 'cooling': (10, 40), 'maturity': 60},
            {'kd': (20, 60), 'cooling': (5, 20), 'maturity': 50},
            {'kd': (40, 90), 'cooling': (15, 40), 'maturity': 65},
            {'kd': (30, 75), 'cooling': (8, 30), 'maturity': 55},
            {'kd': (20, 80), 'cooling': (10, 35), 'maturity': 60},
        ]
    }
}

all_results = []

# === 对每种信号类型测试 ===
for signal_key, config in PARAM_CONFIGS.items():
    print("="*80)
    print(f"TESTING: {config['name']}")
    print("="*80)
    
    best_accuracy = 0
    best_config = None
    
    for param_set in config['test_sets']:
        kd_min, kd_max = param_set['kd']
        cooling_min, cooling_max = param_set['cooling']
        maturity = param_set['maturity']
        
        # 为 alertlist 处理 volume 参数
        if 'volume' in param_set:
            vol_min, vol_max = param_set['volume']
            param_desc = f"KD={kd_min}-{kd_max}, Cool={cooling_min}-{cooling_max}, Vol={vol_min}-{vol_max}, Mat={maturity}"
        else:
            vol_min, vol_max = None, None
            param_desc = f"KD={kd_min}-{kd_max}, Cool={cooling_min}-{cooling_max}, Mat={maturity}"
        
        print(f"\nTesting: {param_desc}")
        
        total_success = 0
        total_candidates = 0
        
        for test_date in test_dates:
            # 构建查询
            if signal_key == 'alertlist':
                query = f"""
                SELECT 
                    a.StockID,
                    s60.EndPrice AS entry_price,
                    s60.StockDate AS test_date
                FROM alertlist a
                INNER JOIN stock60days s60 
                    ON s60.StockID = a.StockID 
                    AND s60.StockDate = '{test_date}'
                WHERE a.alertDate < '{test_date}'
                  AND DATEDIFF('{test_date}', a.alertDate) BETWEEN {cooling_min} AND {cooling_max}
                  AND a.maxPLVR BETWEEN {vol_min} AND {vol_max}
                  AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                  AND s60.MA20 > 0
                """
            
            elif signal_key == 'tradedata':
                query = f"""
                SELECT 
                    t.StockID,
                    s60.EndPrice AS entry_price,
                    s60.StockDate AS test_date
                FROM tradedata t
                INNER JOIN stock60days s60 
                    ON s60.StockID = t.StockID 
                    AND s60.StockDate = '{test_date}'
                WHERE t.TransDate < '{test_date}'
                  AND DATEDIFF('{test_date}', t.TransDate) BETWEEN {cooling_min} AND {cooling_max}
                  AND t.StockDiffRate >= 6.0
                  AND t.Vol >= 1000
                  AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                  AND s60.MA20 > 0
                """
            
            else:  # t_longshadowcover
                query = f"""
                SELECT 
                    t.stockid AS StockID,
                    s60.EndPrice AS entry_price,
                    s60.StockDate AS test_date
                FROM t_longshadowcover t
                INNER JOIN stock60days s60 
                    ON s60.StockID = t.stockid 
                    AND s60.StockDate = '{test_date}'
                WHERE t.transDate < '{test_date}'
                  AND DATEDIFF('{test_date}', t.transDate) BETWEEN {cooling_min} AND {cooling_max}
                  AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                  AND s60.MA20 > 0
                """
            
            try:
                df_candidates = pd.read_sql(query, conn)
                
                for _, candidate in df_candidates.iterrows():
                    stock_id = candidate['StockID']
                    entry_price = candidate['entry_price']
                    entry_date = candidate['test_date']
                    
                    # 查询后续表现
                    future_query = f"""
                    SELECT 
                        MAX((EndPrice - {entry_price}) / {entry_price} * 100) AS max_gain
                    FROM stock60days
                    WHERE StockID = '{stock_id}'
                      AND StockDate > '{entry_date}'
                      AND StockDate <= DATE_ADD('{entry_date}', INTERVAL {TRACKING_DAYS} DAY)
                    """
                    
                    df_future = pd.read_sql(future_query, conn)
                    
                    if len(df_future) > 0 and pd.notna(df_future.iloc[0]['max_gain']):
                        total_candidates += 1
                        max_gain = df_future.iloc[0]['max_gain']
                        if max_gain >= TARGET_GAIN:
                            total_success += 1
            
            except Exception as e:
                continue
        
        # 保存结果
        if total_candidates >= MIN_SAMPLE_SIZE:
            accuracy = (total_success / total_candidates) * 100
            
            result = {
                'signal_type': config['name'],
                'kd_range': f"{kd_min}-{kd_max}",
                'cooling_days': f"{cooling_min}-{cooling_max}",
                'maturity': maturity,
                'total': total_candidates,
                'success': total_success,
                'accuracy': round(accuracy, 2)
            }
            
            if signal_key == 'alertlist':
                result['volume_range'] = f"{vol_min}-{vol_max}"
            
            all_results.append(result)
            
            if accuracy > best_accuracy:
                best_accuracy = accuracy
                best_config = result.copy()
            
            # 显示结果
            status = "✅" if accuracy >= 70 else "📊" if accuracy >= 50 else "⚠️"
            print(f"  {status} Accuracy: {accuracy:.1f}% ({total_success}/{total_candidates})")
        else:
            print(f"  ⚠️  Insufficient data: only {total_candidates} instances")
    
    # 显示该信号类型最佳结果
    if best_config:
        print(f"\n{'='*80}")
        print(f"BEST RESULT for {config['name']}")
        print(f"{'='*80}")
        print(f"Accuracy: {best_accuracy:.1f}%")
        print(f"KD Range: {best_config['kd_range']}")
        print(f"Cooling Days: {best_config['cooling_days']}")
        print(f"Maturity Threshold: {best_config['maturity']}")
        if 'volume_range' in best_config:
            print(f"Volume Range: {best_config['volume_range']}")
        print(f"Sample Size: {best_config['success']}/{best_config['total']}")
    print()

conn.close()

# === 最终总结 ===
print("\n" + "="*80)
print("FINAL SUMMARY - RECOMMENDED CONFIGURATIONS")
print("="*80)

if len(all_results) == 0:
    print("⚠️  No valid configurations found")
else:
    df_results = pd.DataFrame(all_results)
    df_results = df_results.sort_values('accuracy', ascending=False)
    
    # 为每种信号类型显示Top 3
    for signal_type in df_results['signal_type'].unique():
        print(f"\n{'='*80}")
        print(f"{signal_type} - Top 3 Configurations")
        print(f"{'='*80}")
        
        top_3 = df_results[df_results['signal_type'] == signal_type].head(3)
        
        for idx, (_, row) in enumerate(top_3.iterrows(), 1):
            print(f"\n#{idx} - {row['accuracy']:.1f}% accuracy ({row['success']}/{row['total']} instances)")
            print(f"    KD Range: {row['kd_range']}")
            print(f"    Cooling Days: {row['cooling_days']}")
            print(f"    Maturity: {row['maturity']}")
            if 'volume_range' in row:
                print(f"    Volume Range: {row['volume_range']}")

print("\n" + "="*80)
print("OPTIMIZATION COMPLETE")
print("="*80)
print("\nRecommendation: Use the #1 configuration for each signal type in TimeMachine\n")
