import pymysql

conn = pymysql.connect(
    host='localhost',
    user='root',
    password='',  # 空密码
    database='sstv2'
)

try:
    cur = conn.cursor()
    
    # 先查看表结构
    print("📋 weekall 表结构（前10列）:")
    cur.execute("DESCRIBE weekall")
    for i, row in enumerate(cur.fetchall()):
        if i < 10:
            print(f"  {row[0]} ({row[1]})")
    
    print("\n" + "="*50 + "\n")
    
    # 查询最近的日期
    cur.execute("""
        SELECT StockDate, COUNT(*) as Count
        FROM weekall
        WHERE StockDate >= '2026-02-20'
        GROUP BY StockDate
        ORDER BY StockDate DESC
        LIMIT 5
    """)
    
    print("✅ 最近的交易日期:")
    for row in cur.fetchall():
        print(f"  📅 {row[0]}: {row[1]:,} 笔")
    
finally:
    conn.close()
