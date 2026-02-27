#!/usr/bin/env python3
"""检查数据库状态 - 确认历史数据是否安全"""

import pymysql
from datetime import datetime, timedelta

try:
    conn = pymysql.connect(
        host='localhost',
        user='root',
        password='',
        database='sst',
        charset='utf8mb4'
    )
    cursor = conn.cursor()
    
    print("\n" + "="*80)
    print("🔍 数据库状态检查报告")
    print("="*80)
    
    # 1. 检查最近的日期分布
    print("\n📅 最近的交易日期分布：")
    cursor.execute("""
        SELECT StockDate, COUNT(*) as count
        FROM weekall
        WHERE StockDate >= DATE_SUB(CURDATE(), INTERVAL 30 DAY)
        GROUP BY StockDate
        ORDER BY StockDate DESC
        LIMIT 10
    """)
    
    recent_dates = cursor.fetchall()
    if recent_dates:
        for date, count in recent_dates:
            marker = "👈 刚才可能 UPDATE 了这个" if str(date) == "2026-02-23" else ""
            print(f"   {date}: {count:,} 笔 {marker}")
    
    # 2. 检查 2026-02-24 是否存在
    print("\n🎯 今天 (2026-02-24) 的数据：")
    cursor.execute("""
        SELECT COUNT(*) FROM weekall WHERE StockDate = '2026-02-24'
    """)
    today_count = cursor.fetchone()[0]
    
    if today_count > 0:
        print(f"   ⚠️  已存在 {today_count:,} 笔数据（可能是旧数据）")
    else:
        print(f"   ✅ 不存在 2026-02-24 的数据（正常，还没下载）")
    
    # 3. 检查历史数据总量
    print("\n📊 数据库总体状况：")
    cursor.execute("SELECT COUNT(DISTINCT StockDate) FROM weekall")
    total_dates = cursor.fetchone()[0]
    
    cursor.execute("SELECT COUNT(*) FROM weekall")
    total_records = cursor.fetchone()[0]
    
    cursor.execute("SELECT MIN(StockDate), MAX(StockDate) FROM weekall")
    min_date, max_date = cursor.fetchone()
    
    print(f"   总交易日数: {total_dates} 天")
    print(f"   总记录数: {total_records:,} 笔")
    print(f"   日期范围: {min_date} 至 {max_date}")
    
    # 4. 检查 investbase 表
    print("\n📋 investbase 表状态：")
    cursor.execute("SELECT RecDate, LastDate FROM investbase ORDER BY RecDate DESC LIMIT 1")
    result = cursor.fetchone()
    
    if result:
        rec_date, last_date = result
        print(f"   RecDate (记录日期): {rec_date}")
        print(f"   LastDate (最后交易日): {last_date}")
        print(f"\n   💡 说明：系统会根据这些日期决定下载目标")
        print(f"      - 如果 RecDate = 今天 且 时间 < 15:00 → 使用 LastDate")
        print(f"      - 否则 → 使用昨天日期")
    
    # 5. 安全性确认
    print("\n" + "="*80)
    print("🛡️  安全性确认：")
    print("="*80)
    
    print("\n✅ 好消息：")
    print("   1. ON DUPLICATE KEY UPDATE 只会更新「相同日期 + 相同股票」的数据")
    print("   2. 不会影响其他日期的历史数据")
    print("   3. 每个日期的数据独立存储，互不影响")
    
    if str(max_date) == "2026-02-23":
        print("\n⚠️  目前最新日期是 2026-02-23")
        print("   原因：investbase.LastDate 可能还是 2026-02-23")
        print("   解决：需要更新 investbase 才能下载 2026-02-24 的数据")
    
    print("\n" + "="*80)
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"\n❌ 错误: {e}")
