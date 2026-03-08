"""
测试完整优化查询性能（逐步增加数据量）
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
print("PROGRESSIVE QUERY TEST - Finding the Breaking Point")
print("="*80 + "\n")

# Use aggregation approach (faster)
date_ranges = [
    ("2026-02-10", "2026-02-10", "1 day"),
    ("2026-02-09", "2026-02-10", "2 days"),
    ("2026-02-07", "2026-02-10", "4 days"),
    ("2026-02-01", "2026-02-10", "10 days"),
]

for date_start, date_end, label in date_ranges:
    print(f"Testing {label}: {date_start} to {date_end}")
    
    # Step 1: Count candidates
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
    candidate_count = cursor.fetchone()[0]
    count_time = time.time() - start
    
    print(f"  Candidates: {candidate_count:,} [{count_time:.2f}s]")
    
    if candidate_count == 0:
        print(f"  ⚠️  No data for this range\n")
        continue
    
    # Step 2: Try aggregation query
    if candidate_count <= 1000:  # Only test if reasonable number
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
        query_time = time.time() - start
        
        total, success = result[0], result[1] if result[1] is not None else 0
        accuracy = (success / total * 100) if total > 0 else 0
        
        print(f"  Results: {success}/{total} = {accuracy:.1f}% [{query_time:.2f}s]")
        
        if query_time > 30:
            print(f"  ⚠️  TOO SLOW! This range is too large.\n")
            break
        elif query_time > 10:
            print(f"  ⚠️  Getting slow...\n")
        else:
            print(f"  ✓ Acceptable speed\n")
    else:
        print(f"  ⚠️  Too many candidates - skipping full query\n")

print("="*80 + "\n")

cursor.close()
conn.close()
