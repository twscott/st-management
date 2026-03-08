"""
测试均线过滤对准确率的影响
策略测试：
1. 无均线过滤（基准）
2. 价格 > MA20（站上20日均线）
3. MA5 > MA10 > MA20（多头排列）
4. MA5 > MA10 > MA20 > MA60（完整多头排列）
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
print("均线过滤效果测试 - Volume Spike (量能爆发)")
print("="*80 + "\n")

# 使用最佳配置 + 12月数据（最好的市场环境）
kd_min, kd_max = 20, 70
cool_min, cool_max = 5, 20
vol_min, vol_max = 10, 40

date_start = "2025-12-01"
date_end = "2025-12-31"

print(f"测试期间: {date_start} to {date_end} (最佳市场月份)")
print(f"基础配置: KD={kd_min}-{kd_max}, Cooling={cool_min}-{cool_max}, Vol={vol_min}-{vol_max}\n")

# 定义测试策略
strategies = [
    {
        'name': '无均线过滤（基准）',
        'ma_filter': '',
        'description': '当前配置'
    },
    {
        'name': '价格 > MA20',
        'ma_filter': 'AND s60.EndPrice > s60.MA20',
        'description': '站上20日均线'
    },
    {
        'name': '价格 > MA5',
        'ma_filter': 'AND s60.EndPrice > s60.MA5',
        'description': '站上5日均线'
    },
    {
        'name': 'MA5 > MA10',
        'ma_filter': 'AND s60.MA5 > s60.MA10',
        'description': '短期均线上穿'
    },
    {
        'name': 'MA5 > MA10 > MA20',
        'ma_filter': 'AND s60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20',
        'description': '多头排列（基础）'
    },
    {
        'name': 'MA5 > MA10 > MA20 > MA60',
        'ma_filter': 'AND s60.MA5 > s60.MA10 AND s60.MA10 > s60.MA20 AND s60.MA20 > s60.MA60',
        'description': '完整多头排列'
    },
    {
        'name': '价格 > MA20 且 MA5 > MA10',
        'ma_filter': 'AND s60.EndPrice > s60.MA20 AND s60.MA5 > s60.MA10',
        'description': '站上均线 + 短期金叉'
    },
]

results = []

for strategy in strategies:
    print(f"测试: {strategy['name']}")
    print(f"  说明: {strategy['description']}")
    
    start = time.time()
    
    # 使用优化查询 + 均线过滤
    query = f"""
        SELECT 
            COUNT(*) as total,
            SUM(CASE WHEN max_gain >= 5 THEN 1 ELSE 0 END) as success_5,
            SUM(CASE WHEN max_gain >= 10 THEN 1 ELSE 0 END) as success_10,
            SUM(CASE WHEN max_gain >= 15 THEN 1 ELSE 0 END) as success_15,
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
              {strategy['ma_filter']}
        ) as gains
        WHERE max_gain IS NOT NULL
    """
    
    try:
        cursor.execute(query)
        result = cursor.fetchone()
        elapsed = time.time() - start
        
        if result and result[0] > 0:
            total, succ_5, succ_10, succ_15, avg_gain, max_gain = result
            
            acc_5 = (succ_5 / total * 100) if total > 0 else 0
            acc_10 = (succ_10 / total * 100) if total > 0 else 0
            acc_15 = (succ_15 / total * 100) if total > 0 else 0
            
            results.append({
                'name': strategy['name'],
                'total': total,
                'avg_gain': avg_gain,
                'acc_5': acc_5,
                'acc_10': acc_10,
                'acc_15': acc_15,
                'time': elapsed
            })
            
            print(f"  候选数: {total}")
            print(f"  平均涨幅: {avg_gain:.2f}%")
            print(f"  准确率: 5%+={acc_5:.1f}%, 10%+={acc_10:.1f}%, 15%+={acc_15:.1f}%")
            print(f"  [{elapsed:.2f}s]\n")
        else:
            print(f"  ⚠️  无候选股票（过滤太严格）[{elapsed:.2f}s]\n")
    
    except Exception as e:
        print(f"  ❌ 错误: {e}\n")

# 结果对比
if results:
    print("="*80)
    print("结果对比 - 按10%+准确率排序")
    print("="*80 + "\n")
    
    # 基准（无均线过滤）
    baseline = results[0]
    
    # 按10%准确率排序
    sorted_results = sorted(results, key=lambda x: x['acc_10'], reverse=True)
    
    print(f"{'策略':<35} {'候选数':>8} {'平均涨幅':>10} {'10%+准确率':>12} {'vs基准':>10}")
    print("-"*80)
    
    for r in sorted_results:
        improvement = r['acc_10'] - baseline['acc_10']
        status = "✓" if improvement > 0 else ("=" if improvement == 0 else "✗")
        
        print(f"{r['name']:<35} {r['total']:>8} {r['avg_gain']:>9.2f}% {r['acc_10']:>11.1f}% {improvement:>+9.1f}% {status}")
    
    # 找出最佳策略
    best = sorted_results[0]
    
    print("\n" + "="*80)
    print("💡 最佳策略:")
    print("="*80)
    print(f"\n策略: {best['name']}")
    print(f"候选数: {best['total']}")
    print(f"平均涨幅: {best['avg_gain']:.2f}%")
    print(f"10%+准确率: {best['acc_10']:.1f}%")
    
    if best['name'] != baseline['name']:
        improvement = best['acc_10'] - baseline['acc_10']
        print(f"\n✅ 比基准配置提升: {improvement:+.1f}% (准确率)")
        
        # 计算相对提升
        relative_improvement = (improvement / baseline['acc_10'] * 100) if baseline['acc_10'] > 0 else 0
        print(f"   相对提升: {relative_improvement:+.1f}%")
        
        # 判断是否值得采用
        if improvement >= 3:  # 绝对提升≥3%
            print(f"\n🎯 **强烈建议采用此均线过滤策略**")
        elif improvement >= 1:
            print(f"\n⚠️  有轻微改善，可考虑采用")
        else:
            print(f"\n❌ 改善不明显，不建议增加复杂度")
    else:
        print(f"\n❌ 均线过滤未带来改善，保持当前配置")
    
    # 检查是否过度过滤
    if best['total'] < baseline['total'] * 0.3:  # 候选数减少>70%
        print(f"\n⚠️  注意: 候选数大幅减少 ({baseline['total']} → {best['total']})")
        print(f"   过度过滤可能导致机会减少")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
