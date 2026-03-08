"""
优化的查询 - 用日期范围预过滤 stock60days
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
print("OPTIMIZED QUERY TEST - Pre-filter stock60days by date range")
print("="*80 + "\n")

date_start = "2026-01-07"
date_end = "2026-02-11"

print(f"Date Range: {date_start} to {date_end}\n")

# OPTIMIZED: Filter stock60days by date range BEFORE JOIN
print("Step 1: Count candidates with optimized JOIN...")
start = time.time()

cursor.execute(f"""
    SELECT COUNT(*) 
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
""")

candidates = cursor.fetchone()[0]
elapsed = time.time() - start

print(f"  Candidates: {candidates:,}")
print(f"  Query time: {elapsed:.2f}s")

if elapsed > 30:
    print(f"  ⚠️  STILL TOO SLOW!")
else:
    print(f"  ✓ Much better!")

# If reasonable, run full backtest
if candidates > 0 and candidates <= 500:
    print(f"\nStep 2: Running backtest on {candidates} candidates...")
    start = time.time()
    
    cursor.execute(f"""
        SELECT 
            COUNT(*) as total,
            SUM(CASE 
                WHEN (
                    SELECT MAX((s2.EndPrice - s60.EndPrice) / s60.EndPrice * 100)
                    FROM stock60days s2
                    WHERE s2.StockID = a.StockID
                      AND s2.StockDate > s60.StockDate
                      AND s2.StockDate <= DATE_ADD(s60.StockDate, INTERVAL 21 DAY)
                ) >= 30 THEN 1 ELSE 0 
            END) as success_count
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
    """)
    
    result = cursor.fetchone()
    elapsed = time.time() - start
    
    total, success = result[0], result[1] if result[1] is not None else 0
    accuracy = (success / total * 100) if total > 0 else 0
    
    print(f"\n  ✅ RESULTS FOR JAN-FEB 2026:")
    print(f"     Config: KD=20-70, Cooling=10-30, Vol=10-40")
    print(f"     Total: {total}")
    print(f"     Success (30%+ in 21 days): {success}")
    print(f"     Accuracy: {accuracy:.1f}%")
    print(f"     Query time: {elapsed:.2f}s")
    
elif candidates > 500:
    print(f"\n  ⚠️  Still too many candidates: {candidates}")
    print(f"     Need tighter filters or batch processing")
else:
    print(f"\n  ⚠️  No candidates found!")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
