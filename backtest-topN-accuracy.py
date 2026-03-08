"""
时光机回测 - Top N 精选模式
策略：每天只推荐3-5支最优质股票，测试准确率上限
"""

import pymysql
import pandas as pd
import numpy as np
from datetime import datetime, timedelta
from itertools import product
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

print("\n" + "="*70)
print("Time Machine Backtest - Top N Selection Mode")
print("="*70)
print("Strategy: Only recommend 3-5 top-quality stocks per day")
print("Target: 30% profit in 20 days\n")

# 配置参数
TARGET_GAIN = 30
TRACKING_DAYS = 20
TOP_N_LIST = [3, 5, 10]  # 测试不同的推荐数量

# 信号源
SIGNAL_TYPES = {
    'alertlist': 'Volume Spike',
    'tradedata': 'Big Candle',
    't_longshadowcover': 'Long Lower Shadow'
}

# 参数范围（基于之前的最佳实践）
BEST_PARAMS = {
    'alertlist': [
        {'kd': (40, 70), 'cooling': (15, 30), 'volume': (10, 30), 'maturity': 65},
        {'kd': (50, 80), 'cooling': (15, 30), 'volume': (10, 40), 'maturity': 70},
        {'kd': (50, 80), 'cooling': (10, 25), 'volume': (10, 50), 'maturity': 60},
    ],
    'tradedata': [
        {'kd': (40, 70), 'cooling': (10, 25), 'volume': (6, 10), 'maturity': 65},
        {'kd': (50, 80), 'cooling': (15, 30), 'volume': (6, 10), 'maturity': 70},
    ],
    't_longshadowcover': [
        {'kd': (50, 80), 'cooling': (15, 30), 'volume': (0, 100), 'maturity': 60},
        {'kd': (60, 90), 'cooling': (8, 20), 'volume': (0, 100), 'maturity': 65},
    ]
}

# 分析期间：最近3个月，每周一次
end_date = datetime.now() - timedelta(days=2)
start_date = end_date - timedelta(days=90)

test_dates = []
current = start_date
while current <= end_date:
    test_dates.append(current.strftime('%Y-%m-%d'))
    current += timedelta(days=7)

print(f"Analysis period: {start_date.strftime('%Y-%m-%d')} to {end_date.strftime('%Y-%m-%d')}")
print(f"Test dates: {len(test_dates)}")
print(f"Tracking days: {TRACKING_DAYS}")
print(f"Target profit: {TARGET_GAIN}%\n")

all_results = []

# 对每种信号源进行分析
for signal_table, signal_name in SIGNAL_TYPES.items():
    print(f"\n{'='*70}")
    print(f"Signal Source: {signal_name} ({signal_table})")
    print(f"{'='*70}\n")
    
    if signal_table not in BEST_PARAMS:
        continue
    
    for param_set in BEST_PARAMS[signal_table]:
        kd_range = param_set['kd']
        cooling_range = param_set['cooling']
        volume_range = param_set['volume']
        maturity = param_set['maturity']
        
        print(f"Testing params: KD {kd_range[0]}-{kd_range[1]}, "
              f"Cooling {cooling_range[0]}-{cooling_range[1]} days, "
              f"Maturity {maturity}")
        
        for top_n in TOP_N_LIST:
            print(f"  Top {top_n} mode: ", end='', flush=True)
            
            total_recommendations = 0
            total_success = 0
            
            for test_date in test_dates:
                # 构建查询：获取该日期的候选股票
                if signal_table == 'alertlist':
                    query = f"""
                    SELECT 
                        a.StockID,
                        a.alertDate AS signal_date,
                        s60.StockDate AS analysis_date,
                        s60.EndPrice AS entry_price,
                        s60.KD_K,
                        -- 成熟度评分
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
                      AND DATEDIFF('{test_date}', a.alertDate) BETWEEN {cooling_range[0]} AND {cooling_range[1]}
                      AND a.maxPLVR BETWEEN {volume_range[0]} AND {volume_range[1]}
                      AND s60.KD_K BETWEEN {kd_range[0]} AND {kd_range[1]}
                      AND s60.MA20 > 0 
                      AND (s60.MA5 / s60.MA20 * 100) >= 102
                    HAVING maturity_score >= {maturity}
                    ORDER BY maturity_score DESC
                    LIMIT {top_n}
                    """
                elif signal_table == 'tradedata':
                    query = f"""
                    SELECT 
                        t.StockID,
                        t.TransDate AS signal_date,
                        s60.StockDate AS analysis_date,
                        s60.EndPrice AS entry_price,
                        s60.KD_K,
                        (70 + (t.StockDiffRate - 6) * 2) AS maturity_score
                    FROM tradedata t
                    INNER JOIN stock60days s60 
                        ON s60.StockID = t.StockID 
                        AND s60.StockDate = '{test_date}'
                    WHERE t.TransDate < '{test_date}'
                      AND DATEDIFF('{test_date}', t.TransDate) BETWEEN {cooling_range[0]} AND {cooling_range[1]}
                      AND t.StockDiffRate >= 6.0
                      AND t.Vol >= 1000
                      AND s60.KD_K BETWEEN {kd_range[0]} AND {kd_range[1]}
                      AND s60.MA20 > 0 
                      AND (s60.MA5 / s60.MA20 * 100) >= 102
                    HAVING maturity_score >= {maturity}
                    ORDER BY maturity_score DESC
                    LIMIT {top_n}
                    """
                else:  # t_longshadowcover
                    query = f"""
                    SELECT 
                        t.stockid AS StockID,
                        t.transDate AS signal_date,
                        s60.StockDate AS analysis_date,
                        s60.EndPrice AS entry_price,
                        s60.KD_K,
                        75 AS maturity_score
                    FROM t_longshadowcover t
                    INNER JOIN stock60days s60 
                        ON s60.StockID = t.stockid 
                        AND s60.StockDate = '{test_date}'
                    WHERE t.transDate < '{test_date}'
                      AND DATEDIFF('{test_date}', t.transDate) BETWEEN {cooling_range[0]} AND {cooling_range[1]}
                      AND s60.KD_K BETWEEN {kd_range[0]} AND {kd_range[1]}
                      AND s60.MA20 > 0 
                      AND (s60.MA5 / s60.MA20 * 100) >= 102
                    HAVING maturity_score >= {maturity}
                    ORDER BY maturity_score DESC
                    LIMIT {top_n}
                    """
                
                try:
                    df_candidates = pd.read_sql(query, conn)
                    
                    if len(df_candidates) == 0:
                        continue
                    
                    # 对每个候选股票，检查后续20天表现
                    for _, candidate in df_candidates.iterrows():
                        stock_id = candidate['StockID']
                        analysis_date = candidate['analysis_date']
                        entry_price = candidate['entry_price']
                        
                        # 查询后续20天最高涨幅
                        future_query = f"""
                        SELECT 
                            MAX((EndPrice - {entry_price}) / {entry_price} * 100) AS max_gain
                        FROM stock60days
                        WHERE StockID = '{stock_id}'
                          AND StockDate > '{analysis_date}'
                          AND StockDate <= DATE_ADD('{analysis_date}', INTERVAL {TRACKING_DAYS} DAY)
                        """
                        
                        df_future = pd.read_sql(future_query, conn)
                        
                        if len(df_future) > 0 and pd.notna(df_future.iloc[0]['max_gain']):
                            total_recommendations += 1
                            max_gain = df_future.iloc[0]['max_gain']
                            if max_gain >= TARGET_GAIN:
                                total_success += 1
                
                except Exception as e:
                    continue
            
            if total_recommendations > 0:
                accuracy = (total_success / total_recommendations) * 100
                
                result = {
                    'signal': signal_name,
                    'kd_range': f"{kd_range[0]}-{kd_range[1]}",
                    'cooling_days': f"{cooling_range[0]}-{cooling_range[1]}",
                    'maturity': maturity,
                    'top_n': top_n,
                    'total': total_recommendations,
                    'success': total_success,
                    'accuracy': round(accuracy, 2)
                }
                all_results.append(result)
                
                status = "✅" if accuracy >= 80 else "📊"
                print(f"{status} {accuracy:.1f}% ({total_success}/{total_recommendations})")
            else:
                print("No data")

conn.close()

# 汇总结果
print("\n" + "="*70)
print("RESULTS SUMMARY")
print("="*70)

df_results = pd.DataFrame(all_results)

if len(df_results) > 0:
    # 按Top N分组显示
    for top_n in TOP_N_LIST:
        df_topn = df_results[df_results['top_n'] == top_n].sort_values(
            'accuracy', ascending=False
        )
        
        if len(df_topn) > 0:
            print(f"\n{'='*70}")
            print(f"TOP {top_n} RECOMMENDATIONS PER DAY")
            print(f"{'='*70}")
            
            # 显示最高准确率
            best = df_topn.iloc[0]
            print(f"\n🏆 HIGHEST ACCURACY: {best['accuracy']:.1f}%")
            print(f"   Signal: {best['signal']}")
            print(f"   KD Range: {best['kd_range']}")
            print(f"   Cooling Days: {best['cooling_days']}")
            print(f"   Maturity Score: {best['maturity']}")
            print(f"   Success Rate: {best['success']}/{best['total']}")
            
            # 显示准确率≥80%的配置
            high_accuracy = df_topn[df_topn['accuracy'] >= 80]
            if len(high_accuracy) > 0:
                print(f"\n✅ {len(high_accuracy)} configurations with accuracy ≥80%:\n")
                print(high_accuracy[['signal', 'kd_range', 'cooling_days', 
                                     'total', 'success', 'accuracy']].to_string(index=False))
            
            # 显示所有结果
            print(f"\nAll Results (Top {top_n}):\n")
            print(df_topn[['signal', 'kd_range', 'cooling_days', 'maturity',
                          'total', 'success', 'accuracy']].to_string(index=False))
    
    # 保存结果
    csv_file = f'backtest-topN-results-{datetime.now().strftime("%Y%m%d-%H%M%S")}.csv'
    df_results.to_csv(csv_file, index=False, encoding='utf-8-sig')
    print(f"\n💾 Results saved to: {csv_file}")
    
    # 最终总结
    print("\n" + "="*70)
    print("FINAL SUMMARY")
    print("="*70)
    
    for top_n in TOP_N_LIST:
        df_topn = df_results[df_results['top_n'] == top_n]
        if len(df_topn) > 0:
            best_acc = df_topn['accuracy'].max()
            avg_acc = df_topn['accuracy'].mean()
            print(f"\nTop {top_n} mode:")
            print(f"  Highest accuracy: {best_acc:.1f}%")
            print(f"  Average accuracy: {avg_acc:.1f}%")
            print(f"  Configs ≥80%: {len(df_topn[df_topn['accuracy'] >= 80])}")

else:
    print("\n⚠️ Insufficient data for analysis")

print("\n" + "="*70)
print("Analysis complete!")
print("="*70)
