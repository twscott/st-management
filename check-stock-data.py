import mysql.connector

try:
    conn = mysql.connector.connect(
        host='localhost',
        user='root',
        password='',
        database='sst'
    )
    
    cursor = conn.cursor()
    
    # Check Stock60Days data
    cursor.execute('''
        SELECT 
            MIN(TradeDate) as first_date, 
            MAX(TradeDate) as last_date, 
            COUNT(DISTINCT TradeDate) as date_count, 
            COUNT(*) as total_records 
        FROM Stock60Days 
        WHERE TradeDate >= '2025-01-01'
    ''')
    
    result = cursor.fetchone()
    
    print("=== Stock60Days 数据检查 ===")
    print(f"  最早日期: {result[0]}")
    print(f"  最新日期: {result[1]}")
    print(f"  交易日数: {result[2]}")
    print(f"  总记录数: {result[3]}")
    
    # Check KD data
    cursor.execute('''
        SELECT 
            COUNT(DISTINCT TradeDate) as kd_dates
        FROM KDIndicator 
        WHERE TradeDate >= '2025-01-01'
    ''')
    
    kd_result = cursor.fetchone()
    print(f"\n=== KD Indicator 已计算日期 ===")
    print(f"  已处理: {kd_result[0]} 天")
    
    # Check Bollinger data
    cursor.execute('''
        SELECT 
            COUNT(DISTINCT TradeDate) as bb_dates
        FROM BollingerBands 
        WHERE TradeDate >= '2025-01-01'
    ''')
    
    bb_result = cursor.fetchone()
    print(f"\n=== Bollinger Bands 已计算日期 ===")
    print(f"  已处理: {bb_result[0]} 天")
    
    conn.close()
    
    print("\n✓ 数据库连接正常")
    
except Exception as e:
    print(f"✗ 数据库连接失败: {e}")
