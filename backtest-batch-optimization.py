"""
时光机批量优化 - 高效版本
使用批量查询，一次性获取所有数据，然后在内存中处理
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
print("TIME MACHINE BATCH OPTIMIZATION - Efficient Version")
print("="*80)
print("Strategy: Bulk query + in-memory processing")
print("Target: 30% profit in 21 days\n")

TARGET_GAIN = 30
TRACKING_DAYS = 21
MIN_SAMPLES = 8

# === 获取交易日期 ===
print("Step 1: Loading trading dates...")
three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

df_dates = pd.read_sql(f"""
    SELECT DISTINCT StockDate
    FROM stock60days
    WHERE StockDate >= '{three_months_ago}'
      AND StockDate <= '{tracking_cutoff}'
    ORDER BY StockDate
""", conn)

test_dates = df_dates['StockDate'].tolist()
print(f"Found {len(test_dates)} trading days\n")

# === 为每种信号类型批量加载数据 ===

def analyze_signal_type(signal_key, signal_name, param_sets):
    """分析一种信号类型，返回最佳配置"""
    
    print("="*80)
    print(f"ANALYZING: {signal_name}")
    print("="*80)
    
    # Step 1: 批量查询所有候选数据（带有所有需要的字段）
    print(f"Loading all candidates and their future performance...")
    
    if signal_key == 'alertlist':
        query = f"""
        SELECT 
            a.StockID,
            a.alertDate,
            s60.StockDate AS test_date,
            s60.EndPrice AS entry_price,
            s60.KD_K,
            DATEDIFF(s60.StockDate, a.alertDate) AS cooling_days,
            a.maxPLVR,
            (
                SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
                FROM stock60days s2
                WHERE s2.StockID = a.StockID
                  AND s2.StockDate > s60.StockDate
                  AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL {TRACKING_DAYS} DAY)
            ) AS max_future_gain
        FROM alertlist a
        INNER JOIN stock60days s60 
            ON s60.StockID = a.StockID 
            AND s60.StockDate >= a.alertDate
            AND s60.StockDate <= '{tracking_cutoff}'
            AND s60.MA20 > 0
        WHERE a.alertDate >= '{three_months_ago}'
          AND a.alertDate < '{tracking_cutoff}'
          AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 5 AND 50
        """
    
    elif signal_key == 'tradedata':
        query = f"""
        SELECT 
            t.StockID,
            t.TransDate AS signal_date,
            s60.StockDate AS test_date,
            s60.EndPrice AS entry_price,
            s60.KD_K,
            DATEDIFF(s60.StockDate, t.TransDate) AS cooling_days,
            t.StockDiffRate,
            (
                SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
                FROM stock60days s2
                WHERE s2.StockID = t.StockID
                  AND s2.StockDate > s60.StockDate
                  AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL {TRACKING_DAYS} DAY)
            ) AS max_future_gain
        FROM tradedata t
        INNER JOIN stock60days s60 
            ON s60.StockID = t.StockID 
            AND s60.StockDate >= t.TransDate
            AND s60.StockDate <= '{tracking_cutoff}'
            AND s60.MA20 > 0
        WHERE t.TransDate >= '{three_months_ago}'
          AND t.TransDate < '{tracking_cutoff}'
          AND t.StockDiffRate >= 6.0
          AND t.Vol >= 1000
          AND DATEDIFF(s60.StockDate, t.TransDate) BETWEEN 5 AND 50
        """
    
    else:  # t_longshadowcover
        query = f"""
        SELECT 
            t.stockid AS StockID,
            t.transDate AS signal_date,
            s60.StockDate AS test_date,
            s60.EndPrice AS entry_price,
            s60.KD_K,
            DATEDIFF(s60.StockDate, t.TransDate) AS cooling_days,
            (
                SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
                FROM stock60days s2
                WHERE s2.StockID = t.stockid
                  AND s2.StockDate > s60.StockDate
                  AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL {TRACKING_DAYS} DAY)
            ) AS max_future_gain
        FROM t_longshadowcover t
        INNER JOIN stock60days s60 
            ON s60.StockID = t.stockid 
            AND s60.StockDate >= t.transDate
            AND s60.StockDate <= '{tracking_cutoff}'
            AND s60.MA20 > 0
        WHERE t.transDate >= '{three_months_ago}'
          AND t.transDate < '{tracking_cutoff}'
          AND DATEDIFF(s60.StockDate, t.transDate) BETWEEN 5 AND 50
        """
    
    print("Executing bulk query (this may take 30-60 seconds)...")
    df_all = pd.read_sql(query, conn)
    
    # 过滤掉没有未来数据的行
    df_all = df_all[df_all['max_future_gain'].notna()]
    
    print(f"Loaded {len(df_all)} candidate instances")
    
    if len(df_all) == 0:
        print("⚠️  No data available for this signal type\n")
        return None, []
    
    # Step 2: 在内存中测试不同参数组合
    print(f"\nTesting {len(param_sets)} parameter combinations...")
    
    results = []
    best_accuracy = 0
    best_config = None
    
    for params in param_sets:
        kd_min, kd_max = params['kd']
        cooling_min, cooling_max = params['cooling']
        
        # 应用过滤条件
        mask = (
            (df_all['KD_K'] >= kd_min) & 
            (df_all['KD_K'] <= kd_max) &
            (df_all['cooling_days'] >= cooling_min) &
            (df_all['cooling_days'] <= cooling_max)
        )
        
        if signal_key == 'alertlist' and 'volume' in params:
            vol_min, vol_max = params['volume']
            mask &= (df_all['maxPLVR'] >= vol_min) & (df_all['maxPLVR'] <= vol_max)
        
        df_filtered = df_all[mask]
        
        if len(df_filtered) >= MIN_SAMPLES:
            total = len(df_filtered)
            success = (df_filtered['max_future_gain'] >= TARGET_GAIN).sum()
            accuracy = (success / total) * 100
            
            result = {
                'signal': signal_name,
                'kd': f"{kd_min}-{kd_max}",
                'cooling': f"{cooling_min}-{cooling_max}",
                'total': total,
                'success': int(success),
                'accuracy': round(accuracy, 2)
            }
            
            if signal_key == 'alertlist' and 'volume' in params:
                result['volume'] = f"{params['volume'][0]}-{params['volume'][1]}"
            
            results.append(result)
            
            if accuracy > best_accuracy:
                best_accuracy = accuracy
                best_config = result.copy()
            
            # 只显示准确率≥60%的
            if accuracy >= 60:
                status = "✅" if accuracy >= 70 else "📊"
                desc = f"KD={result['kd']}, Cool={result['cooling']}"
                if 'volume' in result:
                    desc += f", Vol={result['volume']}"
                print(f"{status} {accuracy:.1f}% - {desc} ({int(success)}/{total})")
    
    # 显示最佳结果
    if best_config:
        print(f"\n{'='*40}")
        print(f"BEST: {best_accuracy:.1f}% accuracy")
        print(f"KD: {best_config['kd']}, Cooling: {best_config['cooling']}")
        if 'volume' in best_config:
            print(f"Volume: {best_config['volume']}")
        print(f"Samples: {best_config['success']}/{best_config['total']}")
        print()
    
    return best_config, results

# === 测试参数组合 ===
PARAM_SETS_ALERTLIST = [
    {'kd': (20, 70), 'cooling': (8, 30), 'volume': (10, 40)},
    {'kd': (30, 80), 'cooling': (10, 35), 'volume': (10, 50)},
    {'kd': (20, 60), 'cooling': (5, 25), 'volume': (10, 30)},
    {'kd': (30, 70), 'cooling': (10, 30), 'volume': (15, 50)},
    {'kd': (40, 80), 'cooling': (15, 40), 'volume': (10, 40)},
    {'kd': (20, 80), 'cooling': (8, 35), 'volume': (10, 50)},
]

PARAM_SETS_BIGCANDLE = [
    {'kd': (20, 70), 'cooling': (8, 30)},
    {'kd': (30, 80), 'cooling': (10, 35)},
    {'kd': (20, 60), 'cooling': (5, 25)},
    {'kd': (30, 70), 'cooling': (10, 30)},
    {'kd': (40, 80), 'cooling': (15, 40)},
    {'kd': (20, 80), 'cooling': (8, 35)},
]

PARAM_SETS_SHADOW = [
    {'kd': (20, 70), 'cooling': (8, 30)},
    {'kd': (30, 80), 'cooling': (10, 35)},
    {'kd': (20, 60), 'cooling': (5, 25)},
    {'kd': (30, 70), 'cooling': (10, 30)},
    {'kd': (40, 80), 'cooling': (15, 40)},
    {'kd': (20, 80), 'cooling': (8, 35)},
]

# === 执行分析 ===
best_configs = {}
all_results = []

best_volumespike, results_vs = analyze_signal_type('alertlist', 'Volume Spike (热点)', PARAM_SETS_ALERTLIST)
if best_volumespike:
    best_configs['热点'] = best_volumespike
all_results.extend(results_vs)

best_bigcandle, results_bc = analyze_signal_type('tradedata', 'Big Candle (大阳线)', PARAM_SETS_BIGCANDLE)
if best_bigcandle:
    best_configs['大阳线'] = best_bigcandle
all_results.extend(results_bc)

best_shadow, results_sh = analyze_signal_type('t_longshadowcover', 'Long Lower Shadow (长下影线)', PARAM_SETS_SHADOW)
if best_shadow:
    best_configs['长下影线'] = best_shadow
all_results.extend(results_sh)

conn.close()

# === 最终总结 ===
print("="*80)
print("FINAL RECOMMENDED CONFIGURATIONS")
print("="*80)

if len(best_configs) == 0:
    print("⚠️  No viable configurations found")
else:
    for signal_name, config in best_configs.items():
        print(f"\n{signal_name}:")
        print(f"  Accuracy: {config['accuracy']:.1f}%")
        print(f"  KD Range: {config['kd']}")
        print(f"  Cooling Days: {config['cooling']}")
        if 'volume' in config:
            print(f"  Volume Range: {config['volume']}")
        print(f"  Sample Size: {config['success']}/{config['total']}")

print("\n" + "="*80)
print("NEXT STEP: Apply these configurations to TimeMachine UI")
print("="*80)
print("""
Implementation plan:
1. Add a "智能推荐配置" button for each signal type
2. When clicked, auto-fill the optimal parameters
3. User can still manually adjust if needed
""")
