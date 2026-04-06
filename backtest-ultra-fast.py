"""
时光机超快速优化 - 仅测试3个最优配置
直接用3个月数据，只测试3种精选配置，快速得出结果
目标: 20% gain in 20 days
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
print("ULTRA-FAST OPTIMIZATION - Top 3 Configs Only")
print("="*80)
print("Using 3-month data for better accuracy\n")

TARGET_GAIN = 20
TRACKING_DAYS = 20

one_month_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

print(f"Data range: {one_month_ago} to {tracking_cutoff}")
print(f"Target: {TARGET_GAIN}% profit in {TRACKING_DAYS} days\n")

# 精选3个配置（基于经验判断）
CONFIGS = {
    'alertlist': {
        'name': 'Volume Spike (热点)',
        'tests': [
            {'kd': (20, 70), 'cooling': (10, 30), 'volume': (10, 40), 'name': 'Balanced'},
            {'kd': (30, 80), 'cooling': (15, 35), 'volume': (10, 50), 'name': 'Conservative'},
            {'kd': (10, 60), 'cooling': (8, 25), 'volume': (10, 30), 'name': 'Aggressive'},
        ]
    },
    'tradedata': {
        'name': 'Big Candle (大阳线)',
        'tests': [
            {'kd': (30, 80), 'cooling': (10, 30), 'name': 'Balanced'},
            {'kd': (20, 70), 'cooling': (10, 30), 'name': 'Moderate'},
            {'kd': (40, 85), 'cooling': (15, 35), 'name': 'Conservative'},
        ]
    },
    't_longshadowcover': {
        'name': 'Long Lower Shadow (长下影线)',
        'tests': [
            {'kd': (20, 70), 'cooling': (10, 25), 'name': 'Balanced'},
            {'kd': (30, 80), 'cooling': (10, 30), 'name': 'Conservative'},
            {'kd': (10, 60), 'cooling': (8, 20), 'name': 'Aggressive'},
        ]
    }
}

results = {}

for signal_key, config_group in CONFIGS.items():
    print(f"\n{'='*80}")
    print(f"{config_group['name']}")
    print(f"{'='*80}")
    
    best_accuracy = 0
    best_config = None
    
    for test_config in config_group['tests']:
        config_name = test_config['name']
        kd_min, kd_max = test_config['kd']
        cooling_min, cooling_max = test_config['cooling']
        
        desc = f"{config_name}: KD={kd_min}-{kd_max}, Cooling={cooling_min}-{cooling_max}"
        if 'volume' in test_config:
            vol_min, vol_max = test_config['volume']
            desc += f", Vol={vol_min}-{vol_max}"
        
        print(f"\n{desc}")
        print("  Running query...", end=' ', flush=True)
        
        # 构建查询
        if signal_key == 'alertlist':
            vol_min, vol_max = test_config['volume']
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
                  AND s60.StockDate >= '{one_month_ago}'
                  AND s60.StockDate <= '{tracking_cutoff}'
            )
            SELECT 
                COUNT(*) as total,
                SUM(CASE WHEN max_gain >= {TARGET_GAIN} THEN 1 ELSE 0 END) as success
            FROM (
                SELECT 
                    c.StockID,
                    c.entry_date,
                    MAX((s2.EndPrice - c.entry_price) / c.entry_price * 100) AS max_gain
                FROM candidates c
                INNER JOIN stock60days s2 
                    ON s2.StockID = c.StockID
                    AND s2.StockDate > c.entry_date
                    AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL {TRACKING_DAYS} DAY)
                GROUP BY c.StockID, c.entry_date
            ) gains
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
                  AND s60.StockDate >= '{one_month_ago}'
                  AND s60.StockDate <= '{tracking_cutoff}'
            )
            SELECT 
                COUNT(*) as total,
                SUM(CASE WHEN max_gain >= {TARGET_GAIN} THEN 1 ELSE 0 END) as success
            FROM (
                SELECT 
                    c.StockID,
                    c.entry_date,
                    MAX((s2.EndPrice - c.entry_price) / c.entry_price * 100) AS max_gain
                FROM candidates c
                INNER JOIN stock60days s2 
                    ON s2.StockID = c.StockID
                    AND s2.StockDate > c.entry_date
                    AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL {TRACKING_DAYS} DAY)
                GROUP BY c.StockID, c.entry_date
            ) gains
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
                  AND s60.StockDate >= '{one_month_ago}'
                  AND s60.StockDate <= '{tracking_cutoff}'
            )
            SELECT 
                COUNT(*) as total,
                SUM(CASE WHEN max_gain >= {TARGET_GAIN} THEN 1 ELSE 0 END) as success
            FROM (
                SELECT 
                    c.StockID,
                    c.entry_date,
                    MAX((s2.EndPrice - c.entry_price) / c.entry_price * 100) AS max_gain
                FROM candidates c
                INNER JOIN stock60days s2 
                    ON s2.StockID = c.StockID
                    AND s2.StockDate > c.entry_date
                    AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL {TRACKING_DAYS} DAY)
                GROUP BY c.StockID, c.entry_date
            ) gains
            """
        
        try:
            df = pd.read_sql(query, conn)
            
            if len(df) > 0 and df.iloc[0]['total'] > 0:
                total = int(df.iloc[0]['total'])
                success = int(df.iloc[0]['success'])
                accuracy = (success / total) * 100 if total > 0 else 0
                
                status = "✅" if accuracy >= 70 else "📊" if accuracy >= 50 else "⚠️"
                print(f"{status} {accuracy:.1f}% ({success}/{total})")
                
                if accuracy > best_accuracy:
                    best_accuracy = accuracy
                    best_config = {
                        'name': config_name,
                        'config': test_config,
                        'desc': desc,
                        'accuracy': accuracy,
                        'total': total,
                        'success': success
                    }
            else:
                print("❌ No data")
        
        except Exception as e:
            print(f"❌ Error: {e}")
    
    if best_config:
        results[signal_key] = best_config
        print(f"\n  🏆 BEST: {best_config['name']} - {best_config['accuracy']:.1f}% ({best_config['success']}/{best_config['total']})")

conn.close()

# === 最终推荐 ===
print(f"\n\n{'='*80}")
print("FINAL RECOMMENDATIONS (1-month validation)")
print(f"{'='*80}\n")

if not results:
    print("⚠️  No valid configurations found")
else:
    for signal_key in ['alertlist', 'tradedata', 't_longshadowcover']:
        if signal_key in results:
            rec = results[signal_key]
            config = rec['config']
            
            signal_name = CONFIGS[signal_key]['name']
            print(f"\n{signal_name}:")
            print(f"  ✅ Best: {rec['name']}")
            print(f"  📊 Accuracy: {rec['accuracy']:.1f}%")
            print(f"  📈 Sample Size: {rec['success']}/{rec['total']}")
            print(f"  🎯 KD: {config['kd'][0]}-{config['kd'][1]}")
            print(f"  ⏱️  Cooling: {config['cooling'][0]}-{config['cooling'][1]} days")
            if 'volume' in config:
                print(f"  📊 Volume: {config['volume'][0]}-{config['volume'][1]}x")

print(f"\n{'='*80}")
print("COMPLETE - Apply these to TimeMachine UI")
print(f"{'='*80}\n")
