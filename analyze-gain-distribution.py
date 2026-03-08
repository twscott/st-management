"""
分析实际涨幅分布 - 找出合理的目标
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
print("ACTUAL GAIN DISTRIBUTION ANALYSIS")
print("="*80 + "\n")

date_start = "2026-01-07"
date_end = "2026-02-11"

print(f"Date Range: {date_start} to {date_end}\n")
print("Config: KD=20-70, Cooling=10-30, Vol=10-40\n")

# Analyze actual maximum gains
print("Analyzing maximum gains in 21 days...")
start = time.time()

cursor.execute(f"""
    SELECT 
        a.StockID,
        s60.StockDate AS entry_date,
        s60.EndPrice AS entry_price,
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
        AND s60.StockDate >= DATE_ADD(a.alertDate, INTERVAL 10 DAY)
        AND s60.StockDate <= DATE_ADD(a.alertDate, INTERVAL 30 DAY)
    WHERE a.alertDate >= '{date_start}' 
      AND a.alertDate <= '{date_end}'
      AND a.maxPLVR BETWEEN 10 AND 40
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
    ORDER BY max_gain DESC
""")

results = cursor.fetchall()
elapsed = time.time() - start

print(f"Query time: {elapsed:.2f}s")
print(f"Total candidates: {len(results)}\n")

# Calculate distribution
gains = [r[3] for r in results if r[3] is not None]

if gains:
    gains_sorted = sorted(gains, reverse=True)
    
    print("="*80)
    print("GAIN DISTRIBUTION:")
    print("="*80)
    print(f"\nTop 10 performers:")
    for i, gain in enumerate(gains_sorted[:10]):
        print(f"  #{i+1}: {gain:.2f}%")
    
    print(f"\nStatistics:")
    print(f"  Maximum: {max(gains):.2f}%")
    print(f"  Top 10%: {gains_sorted[len(gains)//10] if len(gains) >= 10 else gains_sorted[0]:.2f}%")
    print(f"  Top 25%: {gains_sorted[len(gains)//4] if len(gains) >= 4 else gains_sorted[0]:.2f}%")
    print(f"  Median: {gains_sorted[len(gains)//2]:.2f}%")
    print(f"  Average: {sum(gains)/len(gains):.2f}%")
    print(f"  Minimum: {min(gains):.2f}%")
    
    # Accuracy at different thresholds
    print(f"\nAccuracy at different profit targets:")
    thresholds = [5, 10, 15, 20, 25, 30]
    for threshold in thresholds:
        success = sum(1 for g in gains if g >= threshold)
        accuracy = success / len(gains) * 100
        print(f"  {threshold}%+: {success}/{len(gains)} = {accuracy:.1f}%")
    
    # Recommendations
    print(f"\n{'='*80}")
    print("RECOMMENDATIONS:")
    print("="*80)
    
    # Find threshold with ~70% accuracy
    for target in range(1, 31):
        success = sum(1 for g in gains if g >= target)
        accuracy = success / len(gains) * 100
        if 65 <= accuracy <= 75:
            print(f"\n✓ Recommended target: {target}% profit (Accuracy: {accuracy:.1f}%)")
            break
    
    # Show realistic target
    top10_threshold = gains_sorted[len(gains)//10] if len(gains) >= 10 else gains_sorted[0]
    print(f"✓ Top 10% threshold: {top10_threshold:.1f}% profit")
    
else:
    print("⚠️  No gains data available!")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
