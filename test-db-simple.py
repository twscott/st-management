"""
超简单的测试 - 只查一条记录
"""

import pymysql

try:
    conn = pymysql.connect(
        host='127.0.0.1',
        user='root',
        password='',
        database='sst',
        charset='utf8mb4',
        connect_timeout=5
    )
    
    cursor = conn.cursor()
    
    print("Testing database connection...")
    
    # Test 1: Simple COUNT
    cursor.execute("SELECT COUNT(*) FROM alertlist")
    count = cursor.fetchone()[0]
    print(f"✓ Alertlist total: {count:,} records")
    
    # Test 2: One record
    cursor.execute("SELECT StockID, alertDate FROM alertlist LIMIT 1")
    row = cursor.fetchone()
    if row:
        print(f"✓ Sample: Stock {row[0]}, Date {row[1]}")
    
    # Test 3: Date range count (no JOIN)
    cursor.execute("""
        SELECT COUNT(*) FROM alertlist 
        WHERE alertDate >= '2026-01-01' AND alertDate <= '2026-02-28'
    """)
    count = cursor.fetchone()[0]
    print(f"✓ Jan-Feb 2026: {count:,} alertlist records")
    
    print("\n✅ Database is responsive!")
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"\n❌ Error: {e}")
