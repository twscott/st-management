"""
检查12月（最佳时期）的30%目标准确率
"""

import pymysql

conn = pymysql.connect(
    host='127.0.0.1',
    user='root',
    password='',
    database='sst',
    charset='utf8mb4'
)

cursor = conn.cursor()

print("\n" + "="*80)
print("DEC 2025 DETAILED ANALYSIS - Best Config")
print("="*80 + "\n")

# Best config
kd_min, kd_max = 20, 70
cool_min, cool_max = 5, 20
vol_min, vol_max = 10, 40

date_start = "2025-12-01"
date_end = "2025-12-31"

print(f"Period: Dec 2025")
print(f"Config: KD={kd_min}-{kd_max}, Cooling={cool_min}-{cool_max}, Vol={vol_min}-{vol_max}\n")

# Detailed accuracy at various thresholds
cursor.execute(f"""
    SELECT 
        COUNT(*) as total,
        SUM(CASE WHEN max_gain >= 5 THEN 1 ELSE 0 END) as success_5,
        SUM(CASE WHEN max_gain >= 10 THEN 1 ELSE 0 END) as success_10,
        SUM(CASE WHEN max_gain >= 15 THEN 1 ELSE 0 END) as success_15,
        SUM(CASE WHEN max_gain >= 20 THEN 1 ELSE 0 END) as success_20,
        SUM(CASE WHEN max_gain >= 25 THEN 1 ELSE 0 END) as success_25,
        SUM(CASE WHEN max_gain >= 30 THEN 1 ELSE 0 END) as success_30,
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
    ) as gains
    WHERE max_gain IS NOT NULL
""")

result = cursor.fetchone()

if result:
    total, s5, s10, s15, s20, s25, s30, avg_gain, max_gain = result
    
    print("RESULTS:")
    print("="*80)
    print(f"Total candidates: {total}")
    print(f"Average gain: {avg_gain:.2f}%")
    print(f"Maximum gain: {max_gain:.2f}%\n")
    
    print("ACCURACY BY TARGET:")
    print("-"*80)
    thresholds = [
        (5, s5),
        (10, s10),
        (15, s15),
        (20, s20),
        (25, s25),
        (30, s30),
    ]
    
    for target, success in thresholds:
        accuracy = (success / total * 100) if total > 0 else 0
        status = "✓" if accuracy >= 70 else ("⚠" if accuracy >= 50 else "✗")
        print(f"  {target}%+:  {success}/{total} = {accuracy:.1f}% {status}")
    
    print("\n" + "="*80)
    print("CONCLUSIONS:")
    print("="*80)
    
    if s30 > 0:
        acc_30 = (s30 / total * 100)
        print(f"\n✓ 30% target IS achievable in good market conditions!")
        print(f"  Dec 2025: {acc_30:.1f}% accuracy")
    else:
        print(f"\n✗ Even in best month (Dec), no candidate reached 30%")
        
    if s20 / total >= 0.15:  # 15% accuracy for 20% gain
        print(f"✓ 20% target is reasonable: {s20/total*100:.1f}% accuracy")
    
    # Find sweet spot (70%+ accuracy)
    for target in range(1, 31):
        if target <= avg_gain:
            test_query = f"""
                SELECT COUNT(*)
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
                WHERE max_gain >= {target}
            """
            cursor.execute(test_query)
            count = cursor.fetchone()[0]
            acc = (count / total * 100) if total > 0 else 0
            
            if 68 <= acc <= 75:
                print(f"\n💡 RECOMMENDED TARGET for 70% accuracy: {target}% profit")
                print(f"   Dec 2025 would achieve: {acc:.1f}% accuracy")
                break

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
