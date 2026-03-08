"""
最基础的数据检查 - 不使用JOIN
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

date_start = (datetime.now() - timedelta(days=60)).strftime('%Y-%m-%d')
date_end = (datetime.now() - timedelta(days=25)).strftime('%Y-%m-%d')

print(f"\n{'='*80}")
print("BASIC DATA CHECK (No JOINs)")
print(f"{'='*80}\n")
print(f"Date Range: {date_start} to {date_end}\n")

# 1. Total records in each table
cursor.execute("SELECT COUNT(*) FROM alertlist")
print(f"Total alertlist records: {cursor.fetchone()[0]}")

cursor.execute("SELECT COUNT(*) FROM tradedata")
print(f"Total tradedata records: {cursor.fetchone()[0]}")

cursor.execute("SELECT COUNT(*) FROM t_longshadowcover")
print(f"Total longshadowcover records: {cursor.fetchone()[0]}")

cursor.execute("SELECT COUNT(*) FROM stock60days")
print(f"Total stock60days records: {cursor.fetchone()[0]}")

# 2. Records in date range (alertlist)
print(f"\n--- Alertlist in date range ---")
cursor.execute(f"""
    SELECT COUNT(*) FROM alertlist 
    WHERE alertDate >= '{date_start}' AND alertDate <= '{date_end}'
""")
print(f"Alertlist signals: {cursor.fetchone()[0]}")

cursor.execute(f"""
    SELECT COUNT(*) FROM alertlist 
    WHERE alertDate >= '{date_start}' AND alertDate <= '{date_end}'
      AND maxPLVR BETWEEN 10 AND 40
""")
print(f"With maxPLVR filter: {cursor.fetchone()[0]}")

# 3. Stock60days in date range
print(f"\n--- Stock60days in date range ---")
cursor.execute(f"""
    SELECT COUNT(*) FROM stock60days 
    WHERE StockDate >= '{date_start}' AND StockDate <= '{date_end}'
""")
print(f"Stock60days records: {cursor.fetchone()[0]}")

cursor.execute(f"""
    SELECT COUNT(*) FROM stock60days 
    WHERE StockDate >= '{date_start}' AND StockDate <= '{date_end}'
      AND KD_K BETWEEN 20 AND 70
      AND MA20 > 0
""")
print(f"With KD/MA20 filter: {cursor.fetchone()[0]}")

# 4. Sample Stock IDs
print(f"\n--- Sample overlap check ---")
cursor.execute(f"""
    SELECT DISTINCT StockID FROM alertlist 
    WHERE alertDate >= '{date_start}' AND alertDate <= '{date_end}'
    LIMIT 5
""")
alertlist_stocks = [row[0] for row in cursor.fetchall()]
print(f"Sample alertlist stocks: {alertlist_stocks}")

for stock_id in alertlist_stocks:
    cursor.execute(f"""
        SELECT COUNT(*) FROM stock60days 
        WHERE StockID = '{stock_id}'
          AND StockDate >= '{date_start}' AND StockDate <= '{date_end}'
    """)
    count = cursor.fetchone()[0]
    print(f"  Stock {stock_id}: {count} records in stock60days")

print(f"\n{'='*80}\n")

cursor.close()
conn.close()
