import mysql.connector

conn = mysql.connector.connect(host='localhost', user='root', password='', database='sst')
cursor = conn.cursor()

# 查找最近沒有 Bollinger 數據的日期
query = """
    SELECT StockDate, COUNT(*) as total,
           SUM(CASE WHEN boolUp IS NULL THEN 1 ELSE 0 END) as missing_bollinger,
           SUM(CASE WHEN boolkaikouDiffRate IS NULL THEN 1 ELSE 0 END) as missing_bandwidth
    FROM stock60days
    WHERE StockDate >= '2025-01-01'
    GROUP BY StockDate
    HAVING missing_bollinger > 0 OR missing_bandwidth > 0
    ORDER BY StockDate DESC
    LIMIT 5
"""

cursor.execute(query)

print("日期\t\t總筆數\t缺 Bollinger\t缺 Bandwidth")
print("-" * 60)
rows = cursor.fetchall()
if rows:
    for row in rows:
        print(f"{row[0]}\t{row[1]}\t{row[2]}\t\t{row[3]}")
else:
    print("所有 2025 年資料都已計算完成！")
    
    # 查看最新的資料日期
    cursor.execute("SELECT MAX(StockDate) FROM stock60days WHERE boolkaikouDiffRate IS NOT NULL")
    latest = cursor.fetchone()[0]
    print(f"\n✅ 最新已計算到：{latest}")

cursor.close()
conn.close()
