"""
时光机两阶段优化 - 快速找出最佳配置
Phase 1: 1个月数据，测试多种参数组合，找出 Top 5
Phase 2: 3个月数据，验证 Top 5，得出最终推荐
"""

import pymysql
import pandas as pd
from datetime import datetime, timedelta
from itertools import product
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
print("TWO-PHASE OPTIMIZATION STRATEGY")
print("="*80)
print("Phase 1: Quick scan with 1-month data")
print("Phase 2: Deep validation with 3-month data")
print("Target: 30% profit in 21 days\n")

TARGET_GAIN = 30
TRACKING_DAYS = 21
MIN_SAMPLES = 5  # Phase 1 最少样本数
MIN_SAMPLES_PHASE2 = 15  # Phase 2 最少样本数

# === PHASE 1: 快速扫描（1个月数据） ===
print("="*80)
print("PHASE 1: QUICK SCAN (1 MONTH DATA)")
print("="*80)

one_month_ago = (datetime.now() - timedelta(days=30)).strftime('%Y-%m-%d')
tracking_cutoff_1m = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')

print(f"Date range: {one_month_ago} to {tracking_cutoff_1m}\n")

# 测试参数空间（适度范围）
PARAM_SPACE = {
    'alertlist': {
        'name': 'Volume Spike (热点)',
        'configs': [
            # KD范围, 冷却天数, 成交量倍数
            {'kd': (10, 60), 'cooling': (8, 25), 'volume': (10, 30)},
            {'kd': (20, 70), 'cooling': (10, 30), 'volume': (10, 40)},
            {'kd': (30, 80), 'cooling': (10, 35), 'volume': (10, 50)},
            {'kd': (20, 60), 'cooling': (5, 20), 'volume': (10, 30)},
            {'kd': (30, 70), 'cooling': (15, 35), 'volume': (15, 50)},
            {'kd': (40, 80), 'cooling': (15, 40), 'volume': (10, 40)},
            {'kd': (20, 80), 'cooling': (8, 30), 'volume': (10, 50)},
            {'kd': (25, 75), 'cooling': (10, 30), 'volume': (15, 40)},
        ]
    },
    'tradedata': {
        'name': 'Big Candle (大阳线)',
        'configs': [
            {'kd': (10, 60), 'cooling': (8, 25)},
            {'kd': (20, 70), 'cooling': (10, 30)},
            {'kd': (30, 80), 'cooling': (10, 35)},
            {'kd': (20, 60), 'cooling': (5, 20)},
            {'kd': (30, 70), 'cooling': (15, 35)},
            {'kd': (40, 80), 'cooling': (15, 40)},
            {'kd': (20, 80), 'cooling': (8, 30)},
            {'kd': (25, 75), 'cooling': (10, 30)},
        ]
    },
    't_longshadowcover': {
        'name': 'Long Lower Shadow (长下影线)',
        'configs': [
            {'kd': (10, 60), 'cooling': (8, 25)},
            {'kd': (20, 70), 'cooling': (10, 30)},
            {'kd': (30, 80), 'cooling': (10, 35)},
            {'kd': (20, 60), 'cooling': (5, 20)},
            {'kd': (30, 70), 'cooling': (15, 35)},
            {'kd': (40, 80), 'cooling': (15, 40)},
            {'kd': (20, 80), 'cooling': (8, 25)},
            {'kd': (25, 75), 'cooling': (10, 30)},
        ]
    }
}

def test_config_phase1(signal_key, config, one_month=True):
    """Phase 1: 测试单个配置（1个月数据）"""
    
    kd_min, kd_max = config['kd']
    cooling_min, cooling_max = config['cooling']
    
    if one_month:
        date_filter = f"AND s60.StockDate >= '{one_month_ago}' AND s60.StockDate <= '{tracking_cutoff_1m}'"
        signal_date_filter = f"AND signal_date >= '{one_month_ago}'"
    else:
        # Phase 2 用3个月
        three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
        tracking_cutoff_3m = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')
        date_filter = f"AND s60.StockDate >= '{three_months_ago}' AND s60.StockDate <= '{tracking_cutoff_3m}'"
        signal_date_filter = f"AND signal_date >= '{three_months_ago}'"
    
    if signal_key == 'alertlist':
        vol_min, vol_max = config['volume']
        query = f"""
        WITH candidates AS (
            SELECT 
                a.StockID,
                a.alertDate as signal_date,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM alertlist a
            INNER JOIN stock60days s60 
                ON s60.StockID = a.StockID 
            WHERE DATEDIFF(s60.StockDate, a.alertDate) BETWEEN {cooling_min} AND {cooling_max}
              AND a.maxPLVR BETWEEN {vol_min} AND {vol_max}
              AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
              AND s60.MA20 > 0
              {date_filter}
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
            AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL {TRACKING_DAYS} DAY)
        GROUP BY c.StockID, c.entry_date, c.entry_price
        """
    
    elif signal_key == 'tradedata':
        query = f"""
        WITH candidates AS (
            SELECT 
                t.StockID,
                t.TransDate as signal_date,
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
              {date_filter}
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
            AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL {TRACKING_DAYS} DAY)
        GROUP BY c.StockID, c.entry_date, c.entry_price
        """
    
    else:  # t_longshadowcover
        query = f"""
        WITH candidates AS (
            SELECT 
                t.stockid AS StockID,
                t.transDate as signal_date,
                s60.StockDate AS entry_date,
                s60.EndPrice AS entry_price
            FROM t_longshadowcover t
            INNER JOIN stock60days s60 
                ON s60.StockID = t.stockid 
            WHERE DATEDIFF(s60.StockDate, t.transDate) BETWEEN {cooling_min} AND {cooling_max}
              AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
              AND s60.MA20 > 0
              {date_filter}
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
            AND s2.StockDate <= DATE_ADD(c.entry_date, INTERVAL {TRACKING_DAYS} DAY)
        GROUP BY c.StockID, c.entry_date, c.entry_price
        """
    
    try:
        df = pd.read_sql(query, conn)
        
        if len(df) > 0:
            total = len(df)
            success = (df['max_gain'] >= TARGET_GAIN).sum()
            accuracy = (success / total) * 100 if total > 0 else 0
            
            return {
                'total': total,
                'success': int(success),
                'accuracy': round(accuracy, 2)
            }
    except Exception as e:
        print(f"    Error: {e}")
    
    return None

# Phase 1: 快速扫描
phase1_results = {}

for signal_key, signal_config in PARAM_SPACE.items():
    print(f"\n{'='*80}")
    print(f"Testing: {signal_config['name']}")
    print(f"{'='*80}\n")
    
    results = []
    
    for idx, config in enumerate(signal_config['configs'], 1):
        kd_desc = f"KD={config['kd'][0]}-{config['kd'][1]}"
        cool_desc = f"Cool={config['cooling'][0]}-{config['cooling'][1]}"
        vol_desc = f", Vol={config['volume'][0]}-{config['volume'][1]}" if 'volume' in config else ""
        
        print(f"[{idx}/{len(signal_config['configs'])}] {kd_desc}, {cool_desc}{vol_desc}...", end=' ', flush=True)
        
        result = test_config_phase1(signal_key, config, one_month=True)
        
        if result and result['total'] >= MIN_SAMPLES:
            result['config'] = config
            result['desc'] = f"{kd_desc}, {cool_desc}{vol_desc}"
            results.append(result)
            
            status = "✅" if result['accuracy'] >= 70 else "📊" if result['accuracy'] >= 50 else "⚠️"
            print(f"{status} {result['accuracy']:.1f}% ({result['success']}/{result['total']})")
        else:
            sample_count = result['total'] if result else 0
            print(f"❌ Insufficient ({sample_count} samples)")
    
    # 保存该信号类型的所有结果
    if results:
        # 按准确率排序，取Top 5
        results_sorted = sorted(results, key=lambda x: x['accuracy'], reverse=True)
        phase1_results[signal_key] = results_sorted[:5]
        
        print(f"\n--- Phase 1 Top 5 for {signal_config['name']} ---")
        for i, r in enumerate(phase1_results[signal_key], 1):
            print(f"{i}. {r['accuracy']:.1f}% - {r['desc']} ({r['success']}/{r['total']})")
    else:
        print(f"\n⚠️  No valid configurations found for {signal_config['name']}")
        phase1_results[signal_key] = []

# === PHASE 2: 深度验证（3个月数据） ===
print(f"\n\n{'='*80}")
print("PHASE 2: DEEP VALIDATION (3 MONTHS DATA)")
print(f"{'='*80}\n")

three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
tracking_cutoff_3m = (datetime.now() - timedelta(days=TRACKING_DAYS + 2)).strftime('%Y-%m-%d')
print(f"Date range: {three_months_ago} to {tracking_cutoff_3m}\n")

final_recommendations = {}

for signal_key, top_configs in phase1_results.items():
    if not top_configs:
        continue
    
    signal_name = PARAM_SPACE[signal_key]['name']
    print(f"\n{'='*80}")
    print(f"Validating: {signal_name}")
    print(f"{'='*80}\n")
    
    validated_results = []
    
    for idx, config_result in enumerate(top_configs, 1):
        config = config_result['config']
        desc = config_result['desc']
        
        print(f"[{idx}/5] {desc}...", end=' ', flush=True)
        
        result = test_config_phase1(signal_key, config, one_month=False)  # 用3个月数据
        
        if result and result['total'] >= MIN_SAMPLES_PHASE2:
            result['config'] = config
            result['desc'] = desc
            validated_results.append(result)
            
            status = "✅" if result['accuracy'] >= 70 else "📊" if result['accuracy'] >= 50 else "⚠️"
            print(f"{status} {result['accuracy']:.1f}% ({result['success']}/{result['total']})")
        else:
            sample_count = result['total'] if result else 0
            print(f"❌ Insufficient ({sample_count} samples)")
    
    if validated_results:
        # 选择准确率最高的作为最终推荐
        best = max(validated_results, key=lambda x: x['accuracy'])
        final_recommendations[signal_key] = {
            'signal_name': signal_name,
            'config': best['config'],
            'desc': best['desc'],
            'accuracy': best['accuracy'],
            'total': best['total'],
            'success': best['success']
        }

conn.close()

# === 最终推荐 ===
print(f"\n\n{'='*80}")
print("FINAL RECOMMENDATIONS (Based on 3-month validation)")
print(f"{'='*80}\n")

if not final_recommendations:
    print("⚠️  No configurations met the minimum requirements")
else:
    for signal_key, rec in final_recommendations.items():
        config = rec['config']
        
        print(f"\n{rec['signal_name']}:")
        print(f"  ✅ Accuracy: {rec['accuracy']:.1f}%")
        print(f"  📊 Sample Size: {rec['success']}/{rec['total']}")
        print(f"  🎯 KD Range: {config['kd'][0]}-{config['kd'][1]}")
        print(f"  ⏱️  Cooling Days: {config['cooling'][0]}-{config['cooling'][1]}")
        if 'volume' in config:
            print(f"  📈 Volume Range: {config['volume'][0]}-{config['volume'][1]}x")

print(f"\n{'='*80}")
print("OPTIMIZATION COMPLETE")
print(f"{'='*80}\n")

print("Next step: Apply these configurations to TimeMachine UI")
print("Update the ApplySmartRecommendation() method with validated parameters\n")
