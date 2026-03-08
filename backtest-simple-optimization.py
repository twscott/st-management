"""
时光机简化优化 - 超快版本
策略：简化查询，快速获取结果
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
print("TIME MACHINE SIMPLIFIED OPTIMIZATION")
print("="*80)
print("Finding best configurations for each signal type\n")

TARGET_GAIN = 30
MIN_SAMPLES = 10

# === 测试参数预设（基于经验的合理范围） ===

# 热点（Volume Spike）- 测试配置
ALERTLIST_PARAMS = [
    {'kd': (20, 70), 'cooling': (10, 30), 'volume': (10, 40), 'name': 'Moderate'},
    {'kd': (30, 80), 'cooling': (15, 35), 'volume': (10, 50), 'name': 'Conservative'},
    {'kd': (20, 60), 'cooling': (5, 25), 'volume': (10, 30), 'name': 'Aggressive'},
]

# 大阳线（Big Candle）- 测试配置
BIGCANDLE_PARAMS = [
    {'kd': (20, 70), 'cooling': (10, 30), 'name': 'Moderate'},
    {'kd': (30, 80), 'cooling': (15, 35), 'name': 'Conservative'},
    {'kd': (20, 60), 'cooling': (5, 25), 'name': 'Aggressive'},
]

# 长下影线 - 测试配置
SHADOW_PARAMS = [
    {'kd': (20, 70), 'cooling': (10, 30), 'name': 'Moderate'},
    {'kd': (30, 80), 'cooling': (15, 35), 'name': 'Conservative'},
    {'kd': (20, 60), 'cooling': (5, 25), 'name': 'Aggressive'},
]

def test_configuration(signal_key, params_list, signal_name):
    """测试一个信号类型的多个配置"""
    
    print(f"\n{'='*80}")
    print(f"TESTING: {signal_name}")
    print(f"{'='*80}\n")
    
    results = []
    
    for params in params_list:
        kd_min, kd_max = params['kd']
        cooling_min, cooling_max = params['cooling']
        config_name = params['name']
        
        print(f"Testing {config_name} config: ", end='', flush=True)
        
        # 构建查询（简化版，不计算maturity score）
        if signal_key == 'alertlist':
            vol_min, vol_max = params['volume']
            
            query = f"""
            WITH candidates AS (
                SELECT 
                    a.StockID,
                    s60.StockDate AS entry_date,
                    s60.EndPrice AS entry_price
                FROM alertlist a
                INNER JOIN stock60days s60 
                    ON s60.StockID = a.StockID 
                WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN {cooling_min} AND {cooling_max}
                  AND a.maxPLVR BETWEEN {vol_min} AND {vol_max}
                  AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                  AND s60.MA20 > 0
                  AND s60.StockDate >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)
                  AND s60.StockDate <= DATE_SUB(CURDATE(), INTERVAL 25 DAY)
            )
            SELECT 
                c.StockID,
                c.entry_date,
                c.entry_price,
                MAX((s2.EndPrice - c.entry_price) / c.entry_price * 100) AS max_gain
            FROM candidates c
            INNER JOIN stock60days s2 
                ON s2.StockID = c.StockID
                AND s2.StockDate > c.entry_date
                AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL 21 DAY)
            GROUP BY c.StockID, c.entry_date, c.entry_price
            """
        
        elif signal_key == 'tradedata':
            query = f"""
            WITH candidates AS (
                SELECT 
                    t.StockID,
                    s60.StockDate AS entry_date,
                    s60.EndPrice AS entry_price
                FROM tradedata t
                INNER JOIN stock60days s60 
                    ON s60.StockID = t.StockID 
                WHERE DATEDIFF(s60.StockDate, t.TransDate) BETWEEN {cooling_min} AND {cooling_max}
                  AND t.StockDiffRate >= 6.0
                  AND t.Vol >= 1000
                  AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                  AND s60.MA20 > 0
                  AND s60.StockDate >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)
                  AND s60.StockDate <= DATE_SUB(CURDATE(), INTERVAL 25 DAY)
            )
            SELECT 
                c.StockID,
                c.entry_date,
                c.entry_price,
                MAX((s2.EndPrice - c.entry_price) / c.entry_price * 100) AS max_gain
            FROM candidates c
            INNER JOIN stock60days s2 
                ON s2.StockID = c.StockID
                AND s2.StockDate > c.entry_date
                AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL 21 DAY)
            GROUP BY c.StockID, c.entry_date, c.entry_price
            """
        
        else:  # t_longshadowcover
            query = f"""
            WITH candidates AS (
                SELECT 
                    t.stockid AS StockID,
                    s60.StockDate AS entry_date,
                    s60.EndPrice AS entry_price
                FROM t_longshadowcover t
                INNER JOIN stock60days s60 
                    ON s60.StockID = t.stockid 
                WHERE DATEDIFF(s60.StockDate, t.transDate) BETWEEN {cooling_min} AND {cooling_max}
                  AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
                  AND s60.MA20 > 0
                  AND s60.StockDate >= DATE_SUB(CURDATE(), INTERVAL 90 DAY)
                  AND s60.StockDate <= DATE_SUB(CURDATE(), INTERVAL 25 DAY)
            )
            SELECT 
                c.StockID,
                c.entry_date,
                c.entry_price,
                MAX((s2.EndPrice - c.entry_price) / c.entry_price * 100) AS max_gain
            FROM candidates c
            INNER JOIN stock60days s2 
                ON s2.StockID = c.StockID
                AND s2.StockDate > c.entry_date
                AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL 21 DAY)
            GROUP BY c.StockID, c.entry_date, c.entry_price
            """
        
        try:
            df = pd.read_sql(query, conn)
            
            if len(df) >= MIN_SAMPLES:
                total = len(df)
                success = (df['max_gain'] >= TARGET_GAIN).sum()
                accuracy = (success / total) * 100
                
                result = {
                    'config': config_name,
                    'signal': signal_name,
                    'kd': f"{kd_min}-{kd_max}",
                    'cooling': f"{cooling_min}-{cooling_max}",
                    'total': total,
                    'success': int(success),
                    'accuracy': round(accuracy, 2)
                }
                
                if signal_key == 'alertlist':
                    result['volume'] = f"{vol_min}-{vol_max}"
                
                results.append(result)
                
                status = "✅" if accuracy >= 70 else "📊" if accuracy >= 50 else "⚠️"
                print(f"{status} {accuracy:.1f}% ({int(success)}/{total} success)")
            else:
                print(f"⚠️  Only {len(df)} samples (need {MIN_SAMPLES}+)")
        
        except Exception as e:
            print(f"❌ Error: {e}")
    
    return results

# === 执行测试 ===
all_results = []

results_alert = test_configuration('alertlist', ALERTLIST_PARAMS, 'Volume Spike (热点)')
all_results.extend(results_alert)

results_bigcandle = test_configuration('tradedata', BIGCANDLE_PARAMS, 'Big Candle (大阳线)')
all_results.extend(results_bigcandle)

results_shadow = test_configuration('t_longshadowcover', SHADOW_PARAMS, 'Long Lower Shadow (长下影线)')
all_results.extend(results_shadow)

conn.close()

# === 最终总结 ===
print(f"\n{'='*80}")
print("RECOMMENDED CONFIGURATIONS")
print(f"{'='*80}\n")

if len(all_results) == 0:
    print("⚠️  No valid configurations found")
else:
    df_results = pd.DataFrame(all_results)
    
    # 为每种信号类型找出最佳配置
    for signal_name in df_results['signal'].unique():
        signal_results = df_results[df_results['signal'] == signal_name]
        best = signal_results.loc[signal_results['accuracy'].idxmax()]
        
        print(f"{signal_name}:")
        print(f"  Best Config: {best['config']}")
        print(f"  Accuracy: {best['accuracy']:.1f}%")
        print(f"  KD Range: {best['kd']}")
        print(f"  Cooling Days: {best['cooling']}")
        if 'volume' in best:
            print(f"  Volume Range: {best['volume']}")
        print(f"  Sample Size: {best['success']}/{best['total']}")
        print()

print(f"{'='*80}")
print("COMPLETE - Ready to implement in TimeMachine UI")
print(f"{'='*80}\n")
