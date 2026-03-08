"""
测试均线过滤在差市场（1-2月）的效果
"""

import pymysql
import time

conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

cursor = conn.cursor()

print("\n" + "="*80)
print("均线过滤 - 差市场测试（2026年1-2月）")
print("="*80 + "\n")

# 最佳配置
kd_min, kd_max = 20, 70
cool_min, cool_max = 5, 20
vol_min, vol_max = 10, 40

date_start = "2026-01-07"
date_end = "2026-02-11"

print(f"测试期间: {date_start} to {date_end} (差市场)")
print(f"基础配置: KD={kd_min}-{kd_max}, Cooling={cool_min}-{cool_max}, Vol={vol_min}-{vol_max}\n")

# 只测试关键策略
strategies = [
    ('无均线过滤（基准）', ''),
    ('价格 > MA5', 'AND s60.EndPrice > s60.MA5'),
    ('MA5 > MA10 > MA20 > MA60', 'AND s60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20 AND s60.MA20 > s60.MA60'),
]

results = []

for name, ma_filter in strategies:
    print(f"测试: {name}")
    
    start = time.time()
    
    query = f"""
        SELECT 
            COUNT(*) as total,
            SUM(CASE WHEN max_gain >= 5 THEN 1 ELSE 0 END) as success_5,
            SUM(CASE WHEN max_gain >= 10 THEN 1 ELSE 0 END) as success_10,
            AVG(max_gain) as avg_gain,
            MAX(max_gain) as max_gain
        FROM (
            SELECT 
                (
                    SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
                    FROM stock60days s2
                    WHERE s2.StockID = a.StockID
                      AND s2.StockDate > s60.StockDate
                      AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL 21 DAY)
                ) AS max_gain
            FROM alertlist a
            INNER JOIN stock60days s60 
                ON s60.StockID = a.StockID
                AND s60.StockDate >= DATE_ADD(a.alertDate, INTERVAL {cool_min} DAY)
                AND s60.StockDate <= DATE_ADD(a.alertDate, INTERVAL {cool_max} DAY)
            WHERE a.alertDate >= '{date_start}' 
              AND a.alertDate <= '{date_end}'
              AND a.maxPLVR BETWEEN {vol_min} AND {vol_max}
              AND s60.KD_K BETWEEN {kd_min} AND {kd_max}
              AND s60.MA20 > 0
              {ma_filter}
        ) as gains
        WHERE max_gain IS NOT NULL
    """
    
    try:
        cursor.execute(query)
        result = cursor.fetchone()
        elapsed = time.time() - start
        
        if result and result[0] > 0:
            total, succ_5, succ_10, avg_gain, max_gain = result
            
            acc_5 = (succ_5 / total * 100) if total > 0 else 0
            acc_10 = (succ_10 / total * 100) if total > 0 else 0
            
            results.append({
                'name': name,
                'total': total,
                'avg_gain': avg_gain,
                'acc_5': acc_5,
                'acc_10': acc_10,
            })
            
            print(f"  候选数: {total}")
            print(f"  平均涨幅: {avg_gain:.2f}%")
            print(f"  准确率: 5%+={acc_5:.1f}%, 10%+={acc_10:.1f}%")
            print(f"  [{elapsed:.2f}s]\n")
        else:
            print(f"  ⚠️  无候选 [{elapsed:.2f}s]\n")
    except Exception as e:
        print(f"  ❌ 错误: {e}\n")

# 对比
if results:
    print("="*80)
    print("差市场结果对比")
    print("="*80 + "\n")
    
    baseline = results[0]
    
    print(f"{'策略':<30} {'候选数':>8} {'平均涨幅':>10} {'10%+准确率':>12} {'vs基准':>10}")
    print("-"*80)
    
    for r in sorted(results, key=lambda x: x['acc_10'], reverse=True):
        improvement = r['acc_10'] - baseline['acc_10']
        status = "✓" if improvement > 0 else "="
        print(f"{r['name']:<30} {r['total']:>8} {r['avg_gain']:>9.2f}% {r['acc_10']:>11.1f}% {improvement:>+9.1f}% {status}")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
