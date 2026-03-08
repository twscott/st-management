"""
测试不同月份的市场表现
"""

import pymysql
from datetime import datetime, timedelta

conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

cursor = conn.cursor()

print("\n" + "="*80)
print("MARKET PERFORMANCE BY MONTH - Best Config")
print("="*80 + "\n")

# Use best config: Short Cooldown
kd_min, kd_max = 20, 70
cool_min, cool_max = 5, 20
vol_min, vol_max = 10, 40

print(f"Config: KD={kd_min}-{kd_max}, Cooling={cool_min}-{cool_max}, Vol={vol_min}-{vol_max}\n")

# Test different months (going back in time, allowing 21-day tracking)
test_periods = [
    ("2026-01-07", "2026-02-11", "Jan-Feb 2026"),
    ("2025-12-01", "2025-12-31", "Dec 2025"),
    ("2025-11-01", "2025-11-30", "Nov 2025"),
    ("2025-10-01", "2025-10-31", "Oct 2025"),
]

results_by_month = []

for date_start, date_end, label in test_periods:
    print(f"Testing: {label}")
    
    try:
        cursor.execute(f"""
            SELECT 
                COUNT(*) as total,
                COALESCE(SUM(CASE WHEN max_gain >= 5 THEN 1 ELSE 0 END), 0) as success_5,
                COALESCE(SUM(CASE WHEN max_gain >= 10 THEN 1 ELSE 0 END), 0) as success_10,
                COALESCE(SUM(CASE WHEN max_gain >= 15 THEN 1 ELSE 0 END), 0) as success_15,
                COALESCE(AVG(max_gain), 0) as avg_gain,
                COALESCE(MAX(max_gain), 0) as max_gain
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
            ) as gains
            WHERE max_gain IS NOT NULL
        """)
        
        result = cursor.fetchone()
        
        if result and result[0] > 0:
            total, succ_5, succ_10, succ_15, avg_gain, max_gain = result
            
            acc_5 = (succ_5 / total * 100) if total > 0 else 0
            acc_10 = (succ_10 / total * 100) if total > 0 else 0
            acc_15 = (succ_15 / total * 100) if total > 0 else 0
            
            results_by_month.append({
                'period': label,
                'total': total,
                'avg_gain': avg_gain,
                'max_gain': max_gain,
                'acc_5': acc_5,
                'acc_10': acc_10,
                'acc_15': acc_15
            })
            
            print(f"  Candidates: {total}")
            print(f"  Avg: {avg_gain:.2f}%, Max: {max_gain:.2f}%")
            print(f"  Accuracy: 5%+={acc_5:.1f}%, 10%+={acc_10:.1f}%, 15%+={acc_15:.1f}%\n")
        else:
            print(f"  ⚠️  No data for this period\n")
    
    except Exception as e:
        print(f"  ❌ Error: {e}\n")

# Summary
if results_by_month:
    print("="*80)
    print("SUMMARY - MARKET TRENDS:")
    print("="*80 + "\n")
    
    best_avg = max(results_by_month, key=lambda x: x['avg_gain'])
    best_acc = max(results_by_month, key=lambda x: x['acc_10'])
    
    print(f"Best Average Gain: {best_avg['period']}")
    print(f"  Avg: {best_avg['avg_gain']:.2f}%, Accuracy 10%+: {best_avg['acc_10']:.1f}%\n")
    
    print(f"Best 10%+ Accuracy: {best_acc['period']}")
    print(f"  Accuracy: {best_acc['acc_10']:.1f}%, Avg Gain: {best_acc['avg_gain']:.2f}%\n")
    
    # Key insight
    avg_of_avgs = sum(r['avg_gain'] for r in results_by_month) / len(results_by_month)
    print(f"Overall average gain across all periods: {avg_of_avgs:.2f}%")
    
    if avg_of_avgs < 5:
        print("\n⚠️  CRITICAL INSIGHT:")
        print("    Average gains are consistently LOW across all tested periods.")
        print("    This suggests:")
        print("    1. Current filtering criteria may not be effective")
        print("    2. Market conditions in late 2025-early 2026 were generally weak")
        print("    3. 30% profit target is unrealistic - consider 5-10% instead")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
