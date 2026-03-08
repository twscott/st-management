"""
时光机回测优化 - Python版本
目标：找到21天内获利30%，准确率≥80%的最佳推荐条件
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

print("\n" + "="*60)
print("时光机回测优化分析")
print("="*60)
print(f"目标: 21天内获利30%，寻找准确率≥80%的条件\n")

# 参数搜索空间
SIGNAL_TYPES = {
    'alertlist': '量能爆发',
    'tradedata': '大阳线',
    't_longshadowcover': '长下影线'
}

KD_RANGES = [
    (30, 60),
    (40, 70),
    (50, 80),
    (60, 90)
]

COOLING_RANGES = [
    (8, 20),
    (10, 25),
    (15, 30),
    (8, 30)
]

VOLUME_RANGES = [
    (10, 30),
    (10, 50),
    (15, 40)
]

MATURITY_SCORES = [60, 65, 70, 75]

# 分析最近3个月
end_date = datetime.now() - timedelta(days=2)
start_date = end_date - timedelta(days=90)

print(f"分析期间: {start_date.strftime('%Y-%m-%d')} 至 {end_date.strftime('%Y-%m-%d')}")
print(f"追踪天数: 21天")
print(f"目标获利: 30%\n")

results = []

# 对每种信号源进行分析
for signal_table, signal_name in SIGNAL_TYPES.items():
    print(f"\n{'='*60}")
    print(f"分析信号源: {signal_name} ({signal_table})")
    print(f"{'='*60}\n")
    
    # 根据信号类型构建不同的查询
    if signal_table == 'alertlist':
        signal_query = f"""
        SELECT 
            a.StockID,
            a.alertDate AS signal_date,
            a.maxPLVR AS volume_ratio,
            a.panvolScore AS volume_score
        FROM alertlist a
        WHERE a.alertDate BETWEEN '{start_date.strftime('%Y-%m-%d')}' 
          AND '{end_date.strftime('%Y-%m-%d')}'
          AND a.maxPLVR BETWEEN 10 AND 50
        """
    elif signal_table == 'tradedata':
        signal_query = f"""
        SELECT 
            t.StockID,
            t.TransDate AS signal_date,
            t.StockDiffRate AS volume_ratio,
            50 AS volume_score
        FROM tradedata t
        WHERE t.TransDate BETWEEN '{start_date.strftime('%Y-%m-%d')}' 
          AND '{end_date.strftime('%Y-%m-%d')}'
          AND t.StockDiffRate >= 6.0
          AND t.Vol >= 1000
        """
    else:  # t_longshadowcover
        signal_query = f"""
        SELECT 
            t.stockid AS StockID,
            t.transDate AS signal_date,
            5.0 AS volume_ratio,
            40 AS volume_score
        FROM t_longshadowcover t
        WHERE t.transDate BETWEEN '{start_date.strftime('%Y-%m-%d')}' 
          AND '{end_date.strftime('%Y-%m-%d')}'
        """
    
    df_signals = pd.read_sql(signal_query, conn)
    print(f"找到 {len(df_signals)} 个信号事件")
    
    if len(df_signals) == 0:
        continue
    
    # 对每个参数组合进行测试
    total_combos = len(KD_RANGES) * len(COOLING_RANGES) * len(VOLUME_RANGES) * len(MATURITY_SCORES)
    current = 0
    
    for kd_range, cooling_range, volume_range, maturity in product(
        KD_RANGES, COOLING_RANGES, VOLUME_RANGES, MATURITY_SCORES
    ):
        current += 1
        progress = (current / total_combos) * 100
        print(f"\r进度: [{progress:5.1f}%] {current}/{total_combos}", end='', flush=True)
        
        success_count = 0
        total_count = 0
        
        for _, signal in df_signals.iterrows():
            stock_id = signal['StockID']
            signal_date = signal['signal_date']
            
            # 获取分析日（信号日 + 冷却期）
            analysis_query = f"""
            SELECT 
                s60.StockDate AS analysis_date,
                s60.EndPrice AS entry_price,
                s60.KD_K AS kd_k
            FROM stock60days s60
            WHERE s60.StockID = '{stock_id}'
              AND s60.StockDate > '{signal_date}'
              AND DATEDIFF(s60.StockDate, '{signal_date}') BETWEEN {cooling_range[0]} AND {cooling_range[1]}
              AND s60.KD_K IS NOT NULL
              AND s60.KD_K BETWEEN {kd_range[0]} AND {kd_range[1]}
            LIMIT 1
            """
            
            try:
                df_analysis = pd.read_sql(analysis_query, conn)
                if len(df_analysis) == 0:
                    continue
                
                analysis_date = df_analysis.iloc[0]['analysis_date']
                entry_price = df_analysis.iloc[0]['entry_price']
                
                # 获取后续21天的价格
                future_query = f"""
                SELECT 
                    MAX((s60.EndPrice - {entry_price}) / {entry_price} * 100) AS max_gain,
                    MIN(DATEDIFF(s60.StockDate, '{analysis_date}')) AS days_to_gain
                FROM stock60days s60
                WHERE s60.StockID = '{stock_id}'
                  AND s60.StockDate > '{analysis_date}'
                  AND s60.StockDate <= DATE_ADD('{analysis_date}', INTERVAL 21 DAY)
                  AND (s60.EndPrice - {entry_price}) / {entry_price} * 100 >= 30
                """
                
                df_future = pd.read_sql(future_query, conn)
                total_count += 1
                
                if len(df_future) > 0 and pd.notna(df_future.iloc[0]['max_gain']):
                    max_gain = df_future.iloc[0]['max_gain']
                    if max_gain >= 30:
                        success_count += 1
                        
            except Exception as e:
                continue
        
        if total_count >= 5:  # 样本数至少5个才有意义
            success_rate = (success_count / total_count) * 100
            
            result = {
                'signal_type': signal_name,
                'kd_min': kd_range[0],
                'kd_max': kd_range[1],
                'cooling_min': cooling_range[0],
                'cooling_max': cooling_range[1],
                'volume_min': volume_range[0],
                'volume_max': volume_range[1],
                'maturity': maturity,
                'total': total_count,
                'success': success_count,
                'rate': round(success_rate, 2)
            }
            results.append(result)
            
            # 实时报告高准确率
            if success_rate >= 80:
                print(f"\n✅ 发现高准确率！{signal_name} | KD:{kd_range[0]}-{kd_range[1]} | "
                      f"冷却:{cooling_range[0]}-{cooling_range[1]}天 | "
                      f"准确率:{success_rate:.1f}% ({success_count}/{total_count})")
    
    print()  # 换行

conn.close()

# 汇总结果
print("\n" + "="*60)
print("回测结果汇总")
print("="*60)

df_results = pd.DataFrame(results)

if len(df_results) > 0:
    # 筛选准确率≥80%
    df_top = df_results[df_results['rate'] >= 80].sort_values(
        by=['rate', 'total'], ascending=[False, False]
    )
    
    if len(df_top) > 0:
        print(f"\n🎯 找到 {len(df_top)} 个准确率≥80%的配置：\n")
        print(df_top.to_string(index=False))
        
        # 保存最佳配置
        df_top.to_csv(f'best-configs-{datetime.now().strftime("%Y%m%d-%H%M%S")}.csv', 
                      index=False, encoding='utf-8-sig')
    else:
        print("\n⚠️ 未找到准确率≥80%的配置")
        print("\n📊 准确率最高的前10个配置：\n")
        df_top10 = df_results.nlargest(10, 'rate')
        print(df_top10.to_string(index=False))
    
    # 按信号源分组汇总
    print("\n" + "="*60)
    print("各信号源表现对比")
    print("="*60 + "\n")
    
    summary = df_results.groupby('signal_type').agg({
        'rate': ['mean', 'max'],
        'total': 'sum',
        'success': 'sum'
    }).round(2)
    
    summary.columns = ['平均准确率', '最高准确率', '总样本数', '总成功数']
    summary['整体准确率'] = (summary['总成功数'] / summary['总样本数'] * 100).round(2)
    print(summary)
    
    # 保存完整结果
    csv_file = f'backtest-full-results-{datetime.now().strftime("%Y%m%d-%H%M%S")}.csv'
    df_results.to_csv(csv_file, index=False, encoding='utf-8-sig')
    print(f"\n💾 完整结果已保存至: {csv_file}")
    
else:
    print("\n⚠️ 没有足够的数据进行分析")

print("\n" + "="*60)
print("分析完成")
print("="*60)
