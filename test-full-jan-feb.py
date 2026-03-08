"""
测试完整1-2月数据（为什么之前的查询会挂起？）
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
print("FULL JAN-FEB 2026 TEST")
print("="*80 + "\n")

# Full date range
date_start = "2026-01-07"
date_end = "2026-02-11"

print(f"Date Range: {date_start} to {date_end}")
print("(60 days ago from today, allowing 21-day tracking)")

# Step 1: How many alertlist signals?
start = time.time()
cursor.execute(f"""
    SELECT COUNT(*) FROM alertlist 
    WHERE alertDate >= '{date_start}' AND alertDate <= '{date_end}'
""")
total_alerts = cursor.fetchone()[0]
print(f"\n1. Raw alertlist signals: {total_alerts:,} [{time.time() - start:.2f}s]")

# Step 2: Adding KD filter (JOIN with stock60days)
start = time.time()
cursor.execute(f"""
    SELECT COUNT(*) 
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID
    WHERE a.alertDate >= '{date_start}' 
      AND a.alertDate <= '{date_end}'
      AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
""")
with_cooldown = cursor.fetchone()[0]
elapsed = time.time() - start
print(f"2. With cooldown filter: {with_cooldown:,} [{elapsed:.2f}s]")
if elapsed > 10:
    print("   ⚠️  SLOW! This is taking too long...")

# Step 3: Full filters
start = time.time()
cursor.execute(f"""
    SELECT COUNT(*) 
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID
    WHERE a.alertDate >= '{date_start}' 
      AND a.alertDate <= '{date_end}'
      AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
      AND a.maxPLVR BETWEEN 10 AND 40
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
""")
candidates = cursor.fetchone()[0]
elapsed = time.time() - start
print(f"3. Final candidates: {candidates:,} [{elapsed:.2f}s]")

if elapsed > 10:
    print("   ⚠️  BOTTLENECK FOUND! This filter combination is too expensive.")
else:
    print(f"   ✓ Filter performance acceptable")

# Step 4: If candidates <= 200, try full analysis
if candidates > 0 and candidates <= 200:
    print(f"\n4. Running full backtest on {candidates} candidates...")
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
        WHERE a.alertDate >= '{date_start}' 
          AND a.alertDate <= '{date_end}'
          AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
          AND a.maxPLVR BETWEEN 10 AND 40
          AND s60.KD_K BETWEEN 20 AND 70
          AND s60.MA20 > 0
    """)
    
    result = cursor.fetchone()
    elapsed = time.time() - start
    
    total, success = result[0], result[1] if result[1] is not None else 0
    accuracy = (success / total * 100) if total > 0 else 0
    
    print(f"\n   Results:")
    print(f"   - Total: {total}")
    print(f"   - Success (30%+ in 21 days): {success}")
    print(f"   - Accuracy: {accuracy:.1f}%")
    print(f"   - Query time: {elapsed:.2f}s")
    
    if elapsed > 60:
        print(f"\n   ⚠️  CRITICAL: Query took over 1 minute!")
    elif elapsed > 30:
        print(f"\n   ⚠️  WARNING: Query is slow")
    else:
        print(f"\n   ✓ Query performance acceptable")
        
elif candidates > 200:
    print(f"\n4. ⚠️  Too many candidates ({candidates}) - would take too long")
    print(f"   💡 Need to either:")
    print(f"      - Reduce date range")
    print(f"      - Add stricter filters")
    print(f"      - Pre-calculate gains in a materialized table")
else:
    print(f"\n4. ⚠️  No candidates found!")
    print(f"   💡 Filters may be too restrictive or data is missing")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
