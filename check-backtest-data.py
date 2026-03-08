"""
Check data availability for backtest
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

print("\n=== Data Availability Check ===\n")

# 1. Check stock60days date range
cursor = conn.cursor()
cursor.execute("""
    SELECT 
        MIN(StockDate) as earliest, 
        MAX(StockDate) as latest, 
        COUNT(DISTINCT StockDate) as total_days,
        COUNT(DISTINCT StockID) as total_stocks
    FROM stock60days
""")
result = cursor.fetchone()
print(f"Stock60days: {result[0]} to {result[1]}")
print(f"  Total days: {result[2]}, Total stocks: {result[3]}\n")

# 2. Check recent 3 months data
three_months_ago = (datetime.now() - timedelta(days=90)).strftime('%Y-%m-%d')
cursor.execute(f"""
    SELECT COUNT(DISTINCT StockDate) as days
    FROM stock60days
    WHERE StockDate >= '{three_months_ago}'
""")
recent_days = cursor.fetchone()[0]
print(f"Recent 3 months ({three_months_ago} onwards): {recent_days} days\n")

# 3. Check alertlist data
cursor.execute(f"""
    SELECT 
        MIN(alertDate) as earliest,
        MAX(alertDate) as latest,
        COUNT(*) as total_alerts,
        COUNT(DISTINCT StockID) as unique_stocks
    FROM alertlist
    WHERE alertDate >= '{three_months_ago}'
""")
result = cursor.fetchone()
print(f"Alertlist (Volume Spike): {result[0]} to {result[1]}")
print(f"  Total alerts: {result[2]}, Unique stocks: {result[3]}\n")

# 4. Check tradedata (big candles)
cursor.execute(f"""
    SELECT 
        MIN(TransDate) as earliest,
        MAX(TransDate) as latest,
        COUNT(*) as total_candles,
        COUNT(DISTINCT StockID) as unique_stocks
    FROM tradedata
    WHERE TransDate >= '{three_months_ago}' AND StockDiffRate >= 6.0
""")
result = cursor.fetchone()
print(f"Tradedata (Big Candle >= 6%): {result[0]} to {result[1]}")
print(f"  Total candles: {result[2]}, Unique stocks: {result[3]}\n")

# 5. Check long shadow cover
cursor.execute(f"""
    SELECT 
        MIN(transDate) as earliest,
        MAX(transDate) as latest,
        COUNT(*) as total_shadows,
        COUNT(DISTINCT stockid) as unique_stocks
    FROM t_longshadowcover
    WHERE transDate >= '{three_months_ago}'
""")
result = cursor.fetchone()
print(f"Long Shadow Cover: {result[0]} to {result[1]}")
print(f"  Total shadows: {result[2]}, Unique stocks: {result[3]}\n")

# 6. Test a sample query with relaxed conditions
test_date = '2026-02-20'
print(f"=== Sample Query Test (date: {test_date}) ===\n")

# Test alertlist query
cursor.execute(f"""
    SELECT 
        COUNT(*) as candidates,
        AVG(s60.KD_K) as avg_kd,
        AVG(DATEDIFF('{test_date}', a.alertDate)) as avg_cooling
    FROM alertlist a
    INNER JOIN stock60days s60 
        ON s60.StockID = a.StockID 
        AND s60.StockDate = '{test_date}'
    WHERE a.alertDate < '{test_date}'
      AND DATEDIFF('{test_date}', a.alertDate) BETWEEN 5 AND 60
      AND s60.MA20 > 0
""")
result = cursor.fetchone()
print(f"Alertlist candidates (relaxed): {result[0]}")
if result[0] > 0:
    print(f"  Avg KD: {result[1]:.1f}, Avg cooling days: {result[2]:.1f}\n")

# Test tradedata query
cursor.execute(f"""
    SELECT 
        COUNT(*) as candidates,
        AVG(s60.KD_K) as avg_kd,
        AVG(DATEDIFF('{test_date}', t.TransDate)) as avg_cooling
    FROM tradedata t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.StockID 
        AND s60.StockDate = '{test_date}'
    WHERE t.TransDate < '{test_date}'
      AND DATEDIFF('{test_date}', t.TransDate) BETWEEN 5 AND 60
      AND t.StockDiffRate >= 6.0
      AND s60.MA20 > 0
""")
result = cursor.fetchone()
print(f"Big Candle candidates (relaxed): {result[0]}")
if result[0] > 0:
    print(f"  Avg KD: {result[1]:.1f}, Avg cooling days: {result[2]:.1f}\n")

# Test long shadow query
cursor.execute(f"""
    SELECT 
        COUNT(*) as candidates,
        AVG(s60.KD_K) as avg_kd,
        AVG(DATEDIFF('{test_date}', t.transDate)) as avg_cooling
    FROM t_longshadowcover t
    INNER JOIN stock60days s60 
        ON s60.StockID = t.stockid 
        AND s60.StockDate = '{test_date}'
    WHERE t.transDate < '{test_date}'
      AND DATEDIFF('{test_date}', t.transDate) BETWEEN 5 AND 60
      AND s60.MA20 > 0
""")
result = cursor.fetchone()
print(f"Long Shadow candidates (relaxed): {result[0]}")
if result[0] > 0:
    print(f"  Avg KD: {result[1]:.1f}, Avg cooling days: {result[2]:.1f}\n")

conn.close()
print("=== Check Complete ===")
