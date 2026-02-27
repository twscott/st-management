#!/usr/bin/env python3
"""检查 2026-02-23 数据的详细状态"""

import pymysql
from datetime import datetime

try:
    conn = pymysql.connect(
        host='localhost',
        user='root',
        password='',
        database='sstv2',
        charset='utf8mb4'
    )
    cursor = conn.cursor()
    
    # 检查数据是否存在及更新时间
    query = """
    SELECT 
        StockDate,
        COUNT(*) as total_records,
        MIN(UpdateTime) as first_update,
        MAX(UpdateTime) as last_update,
        COUNT(DISTINCT CASE WHEN Market = 'TSE' THEN StockCode END) as tse_count,
        COUNT(DISTINCT CASE WHEN Market = 'OTC' THEN StockCode END) as otc_count,
        COUNT(DISTINCT CASE WHEN Market = 'EMERGING' THEN StockCode END) as emerging_count
    FROM weekall 
    WHERE StockDate = '2026-02-23'
    GROUP BY StockDate
    """
    
    cursor.execute(query)
    result = cursor.fetchone()
    
    if result:
        stock_date, total, first_update, last_update, tse, otc, emerging = result
        
        print(f"\n📊 数据状态报告 - {stock_date}")
        print("=" * 70)
        print(f"总记录数: {total}")
        print(f"  ├─ TSE 上市: {tse}")
        print(f"  ├─ OTC 上柜: {otc}")
        print(f"  └─ EMERGING 兴柜: {emerging}")
        print()
        print(f"⏰ 时间信息:")
        print(f"  首次创建: {first_update}")
        print(f"  最后更新: {last_update}")
        
        # 计算时间差
        if first_update and last_update:
            time_diff = (last_update - first_update).total_seconds()
            print(f"  时间跨度: {time_diff:.1f} 秒")
            
            # 判断是新下载还是更新
            now = datetime.now()
            last_update_age = (now - last_update).total_seconds()
            
            print()
            if last_update_age < 60:  # 1分钟内
                print(f"✅ 刚刚更新（{last_update_age:.0f} 秒前）")
                if time_diff < 10:
                    print("⚡ 极快完成 → 使用了 ON DUPLICATE KEY UPDATE（数据已存在）")
                else:
                    print("🆕 正常下载速度 → 全新数据下载")
            else:
                print(f"⏱️ 上次更新于 {int(last_update_age/60)} 分钟前")
    else:
        print("\n❌ 找不到 2026-02-23 的数据")
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"\n❌ 错误: {e}")
