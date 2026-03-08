"""
测试 JOIN 性能
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
print("JOIN PERFORMANCE TEST")
print("="*80 + "\n")

# Test 1: Simple JOIN with LIMIT
print("Test 1: Simple JOIN (LIMIT 10)...")
start_time = time.time()
cursor.execute("""
    SELECT 
        a.StockID,
        a.alertDate,
        s60.StockDate,
        s60.KD_K
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID
    WHERE a.alertDate >= '2026-02-01' 
      AND a.alertDate <= '2026-02-10'
    LIMIT 10
""")
rows = cursor.fetchall()
elapsed = time.time() - start_time
print(f"  ✓ Retrieved {len(rows)} rows in {elapsed:.2f}s")

# Test 2: JOIN with DATEDIFF (no subquery)
print("\nTest 2: JOIN with DATEDIFF condition (LIMIT 100)...")
start_time = time.time()
cursor.execute("""
    SELECT 
        a.StockID,
        a.alertDate,
        s60.StockDate,
        s60.KD_K,
        s60.EndPrice
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID
    WHERE a.alertDate >= '2026-02-01' 
      AND a.alertDate <= '2026-02-05'
      AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
      AND s60.KD_K BETWEEN 20 AND 70
    LIMIT 100
""")
rows = cursor.fetchall()
elapsed = time.time() - start_time
print(f"  ✓ Retrieved {len(rows)} rows in {elapsed:.2f}s")

# Test 3: COUNT with same conditions (critical test)
print("\nTest 3: COUNT with all filters (NO LIMIT)...")
start_time = time.time()
cursor.execute("""
    SELECT COUNT(*) 
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID
    WHERE a.alertDate >= '2026-02-01' 
      AND a.alertDate <= '2026-02-05'
      AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
      AND a.maxPLVR BETWEEN 10 AND 40
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
""")
count = cursor.fetchone()[0]
elapsed = time.time() - start_time
print(f"  ✓ Found {count:,} candidate rows in {elapsed:.2f}s")

# Test 4: Correlated subquery (THE BOTTLENECK)
print("\nTest 4: WITH correlated subquery (LIMIT 1)...")
print("  WARNING: This is where queries hang...")
start_time = time.time()

cursor.execute("""
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
    WHERE a.alertDate >= '2026-02-01'
      AND a.alertDate <= '2026-02-03'
      AND DATEDIFF(s60.StockDate, a.alertDate) BETWEEN 10 AND 30
      AND a.maxPLVR BETWEEN 10 AND 40
      AND s60.KD_K BETWEEN 20 AND 70
      AND s60.MA20 > 0
    LIMIT 1
""")
row = cursor.fetchone()
elapsed = time.time() - start_time

if row:
    print(f"  ✓ Result: Stock {row[0]}, Entry {row[1]}, Price {row[2]:.2f}, Max Gain {row[3]:.2f}%")
print(f"  ⏱️  Query time: {elapsed:.2f}s")

if elapsed > 30:
    print("  ⚠️  CRITICAL: Subquery is too slow!")
    print("  💡 SOLUTION: Pre-calculate gains or use different approach")

print("\n" + "="*80 + "\n")

cursor.close()
conn.close()
