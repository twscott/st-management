"""
时光机全面回测优化 - 为三种信号类型分别找出最佳参数配置
目标：准确率最高，达到80%以上 + 21天内30%获利
数据范围：至少3个月历史数据
"""

import pymysql
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
import warnings
warnings.filterwarnings('ignore')

# 数据库连接
conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

print("\n" + "="*80)
print("TIME MACHINE COMPREHENSIVE BACKTEST OPTIMIZATION")
print("="*80)
print("Goal: Find best parameters for each signal type")
print("Target: 80%+ accuracy, 30% profit in 21 days")
print("Data: 3+ months historical data\n")

# === 配置参数 ===
TARGET_GAIN = 30  # 目标获利 30%
TRACKING_DAYS = 21  # 追踪天数 21 天
MIN_SAMPLE_SIZE = 10  # 最少样本数
MIN_ACCURACY = 80  # 最低准确率要求

# === 获取实际的交易日期（有数据的日期）===
print("Loading trading dates from database...")
cursor = conn.cursor()

# 获取最近3个月的交易日期（确保后面还有至少21天可以追踪）
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
print(f"Found {len(test_dates)} trading days for testing")
print(f"Date range: {test_dates[0]} to {test_dates[-1]}\n")

if len(test_dates) < 30:
    print("⚠️  Warning: Less than 30 trading days available. Results may not be reliable.\n")

# === 信号类型定义 ===
SIGNAL_CONFIGS = {
    'alertlist': {
        'name': 'Volume Spike (热点)',
        'table': 'alertlist',
        'date_col': 'alertDate',
        'param_space': {
            'kd_min': [20, 30, 40, 50],
            'kd_max': [60, 70, 80, 90],
            'cooling_min': [5, 8, 10, 15],
            'cooling_max': [20, 25, 30, 40],
            'volume_min': [10, 15, 20],
            'volume_max': [30, 40, 50, 100],
            'maturity': [50, 55, 60, 65, 70],
        }
    },
    'tradedata': {
        'name': 'Big Candle (大阳线)',
        'table': 'tradedata',
        'date_col': 'TransDate',
        'param_space': {
            'kd_min': [20, 30, 40, 50],
            'kd_max': [60, 70, 80, 90],
            'cooling_min': [5, 8, 10, 15],
            'cooling_max': [20, 25, 30, 40],
            'maturity': [50, 55, 60, 65, 70],
        }
    },
    't_longshadowcover': {
        'name': 'Long Lower Shadow (长下影线)',
        'table': 't_longshadowcover',
        'date_col': 'transDate',
        'param_space': {
            'kd_min': [20, 30, 40, 50],
            'kd_max': [60, 70, 80, 90],
            'cooling_min': [5, 8, 10, 15],
            'cooling_max': [20, 25, 30, 40],
            'maturity': [50, 55, 60, 65, 70],
        }
    }
}

all_results = []

# === 对每种信号类型进行完整参数搜索 ===
for signal_key, config in SIGNAL_CONFIGS.items():
    print("="*80)
    print(f"SIGNAL TYPE: {config['name']}")
    print("="*80)
    
    param_space = config['param_space']
    table = config['table']
    date_col = config['date_col']
    
    # 生成所有参数组合
    test_count = 0
    best_accuracy = 0
    best_config = None
    
    for kd_min in param_space['kd_min']:
        for kd_max in param_space['kd_max']:
            if kd_max <= kd_min:
                continue
                
            for cooling_min in param_space['cooling_min']:
                for cooling_max in param_space['cooling_max']:
                    if cooling_max <= cooling_min:
                        continue
                        
                    for maturity in param_space['maturity']:
                        test_count += 1
                        
                        # 对于 alertlist，还要测试不同的 volume 范围
                        volume_ranges = []
                        if signal_key == 'alertlist':
                            for vol_min in param_space['volume_min']:
                                for vol_max in param_space['volume_max']:
                                    if vol_max > vol_min:
                                        volume_ranges.append((vol_min, vol_max))
                        else:
                            volume_ranges = [(None, None)]
                        
                        for vol_min, vol_max in volume_ranges:
                            total_candidates = 0
                            total_success = 0
                            
                            # 在每个测试日期上运行查询
                            for test_date in test_dates:
                                # 构建查询
                                if signal_key == 'alertlist':
                                    query = f"""
                                    SELECT 
                                        a.StockID,
                                        s60.EndPrice AS entry_price,
                                        s60.StockDate AS test_date,
                                        (
                                            CASE 
                                                WHEN DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 15 AND 30 THEN 40
                                                WHEN DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 31 AND 50 THEN 35
                                                WHEN DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 8 AND 14 THEN 25
                                                ELSE 15
                                            END +
                                            CASE 
                                                WHEN a.maxPLVR BETWEEN 10 AND 20 THEN 30
                                                WHEN a.maxPLVR BETWEEN 20 AND 30 THEN 25
                                                WHEN a.maxPLVR BETWEEN 30 AND 50 THEN 15
                                                ELSE 10
                                            END +
                                            CASE 
                                                WHEN a.panvolScore BETWEEN 20 AND 50 THEN 20
                                                WHEN a.panvolScore < 20 THEN 10
                                                WHEN a.panvolScore BETWEEN 50 AND 100 THEN 5
                                                ELSE 0
                                            END +
                                            CASE 
                                                WHEN a.panVol5CntPos > a.panVol5CntNeg THEN 10
                                                ELSE 0
                                            END
                                        ) AS maturity_score
                                    FROM alertlist a
                                    INNER JOIN stock60days s60 
                                        ON s60.StockID = a.StockID 
                                        AND s60.StockDate = '{test_date}'
                                    WHERE a.alertDate < '{test_date}'
                                      AND DATEDIFF('{test_date}', a.alertDate) BETWEEN {cooling_min} AND {cooling_max}
                                      AND a.maxPLVR BETWEEN {vol_min} AND {vol_max}
                                      AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                                      AND s60.MA20 > 0
                                    HAVING maturity_score >= {maturity}
                                    """
                                
                                elif signal_key == 'tradedata':
                                    query = f"""
                                    SELECT 
                                        t.StockID,
                                        s60.EndPrice AS entry_price,
                                        s60.StockDate AS test_date,
                                        (70 + (t.StockDiffRate - 6) * 2) AS maturity_score
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
                                    HAVING maturity_score >= {maturity}
                                    """
                                
                                else:  # t_longshadowcover
                                    query = f"""
                                    SELECT 
                                        t.stockid AS StockID,
                                        s60.EndPrice AS entry_price,
                                        s60.StockDate AS test_date,
                                        75 AS maturity_score
                                    FROM t_longshadowcover t
                                    INNER JOIN stock60days s60 
                                        ON s60.StockID = t.stockid 
                                        AND s60.StockDate = '{test_date}'
                                    WHERE t.transDate < '{test_date}'
                                      AND DATEDIFF('{test_date}', t.transDate) BETWEEN {cooling_min} AND {cooling_max}
                                      AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                                      AND s60.MA20 > 0
                                    HAVING maturity_score >= {maturity}
                                    """
                                
                                try:
                                    df_candidates = pd.read_sql(query, conn)
                                    
                                    # 对每个候选股票，检查后续表现
                                    for _, candidate in df_candidates.iterrows():
                                        stock_id = candidate['StockID']
                                        entry_price = candidate['entry_price']
                                        entry_date = candidate['test_date']
                                        
                                        # 查询后续21天最高涨幅
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
                                    'maturity_threshold': maturity,
                                    'total_instances': total_candidates,
                                    'success_count': total_success,
                                    'accuracy': round(accuracy, 2)
                                }
                                
                                if signal_key == 'alertlist':
                                    result['volume_range'] = f"{vol_min}-{vol_max}"
                                
                                all_results.append(result)
                                
                                # 更新最佳配置
                                if accuracy > best_accuracy:
                                    best_accuracy = accuracy
                                    best_config = result.copy()
                                
                                # 实时显示进度（只显示达标的）
                                if accuracy >= MIN_ACCURACY:
                                    status = "✅"
                                    print(f"{status} {accuracy:.1f}% - KD={kd_min}-{kd_max}, "
                                          f"Cooling={cooling_min}-{cooling_max}, Mat={maturity}"
                                          + (f", Vol={vol_min}-{vol_max}" if vol_min else "")
                                          + f" ({total_success}/{total_candidates})")
    
    # 显示该信号类型的最佳结果
    print(f"\n--- Best Result for {config['name']} ---")
    if best_config:
        print(f"Accuracy: {best_accuracy:.1f}%")
        print(f"KD Range: {best_config['kd_range']}")
        print(f"Cooling Days: {best_config['cooling_days']}")
        print(f"Maturity: {best_config['maturity_threshold']}")
        if 'volume_range' in best_config:
            print(f"Volume Range: {best_config['volume_range']}")
        print(f"Instances: {best_config['success_count']}/{best_config['total_instances']}")
    else:
        print("No configuration met minimum requirements")
    print(f"\nTested {test_count} parameter combinations\n")

conn.close()

# === 输出最终结果 ===
print("\n" + "="*80)
print("FINAL RESULTS - TOP CONFIGURATIONS")
print("="*80)

if len(all_results) == 0:
    print("⚠️  No configurations met the minimum requirements")
    print(f"   Minimum sample size: {MIN_SAMPLE_SIZE}")
    print(f"   Target accuracy: {MIN_ACCURACY}%")
else:
    # 按准确率排序
    df_results = pd.DataFrame(all_results)
    df_results = df_results.sort_values('accuracy', ascending=False)
    
    # 显示每种信号类型的前3名
    for signal_type in df_results['signal_type'].unique():
        print(f"\n=== {signal_type} - Top 3 Configurations ===")
        top_3 = df_results[df_results['signal_type'] == signal_type].head(3)
        
        for idx, row in top_3.iterrows():
            print(f"\n{row['accuracy']:.1f}% accuracy ({row['success_count']}/{row['total_instances']} instances)")
            print(f"  KD Range: {row['kd_range']}")
            print(f"  Cooling Days: {row['cooling_days']}")
            print(f"  Maturity Threshold: {row['maturity_threshold']}")
            if 'volume_range' in row:
                print(f"  Volume Range: {row['volume_range']}")
    
    # 显示达到80%+准确率的所有配置
    high_accuracy = df_results[df_results['accuracy'] >= MIN_ACCURACY]
    
    if len(high_accuracy) > 0:
        print(f"\n" + "="*80)
        print(f"CONFIGURATIONS WITH {MIN_ACCURACY}%+ ACCURACY ({len(high_accuracy)} found)")
        print("="*80)
        
        for idx, row in high_accuracy.iterrows():
            print(f"\n{row['signal_type']}: {row['accuracy']:.1f}% ({row['success_count']}/{row['total_instances']})")
            print(f"  KD: {row['kd_range']}, Cooling: {row['cooling_days']}, Maturity: {row['maturity_threshold']}"
                  + (f", Volume: {row['volume_range']}" if 'volume_range' in row else ""))

print("\n" + "="*80)
print("OPTIMIZATION COMPLETE")
print("="*80)
