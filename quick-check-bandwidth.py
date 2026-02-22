import mysql.connector

conn = mysql.connector.connect(host='localhost', user='root', password='', database='sst')
cursor = conn.cursor()

# 簡單檢查：抽樣檢查幾個日期的 bandwidth 資料
cursor.execute("""
    SELECT StockDate, StockID, boolUp, boolMid, boolDown, boolkaikouDiffRate
    FROM stock60days
    WHERE StockDate IN ('2025-02-01', '2025-03-01', '2025-12-01', '2026-02-01')
      AND StockID IN ('2330', '2317', '2454')
    ORDER BY StockDate, StockID
""")

print("日期\t\t股票\tBoolUp\tBoolMid\tBoolDown\tBandwidth")
print("-" * 80)
for row in cursor.fetchall():
    date, stock, up, mid, down, bw = row
    if up is not None and bw is not None:
        print(f"{date}\t{stock}\t{up:.2f}\t{mid:.2f}\t{down:.2f}\t\t{bw:.2f}%")
    else:
        print(f"{date}\t{stock}\t-\t-\t-\t\t- (未計算)")

cursor.close()
conn.close()
