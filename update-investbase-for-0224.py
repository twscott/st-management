#!/usr/bin/env python3
"""更新 investbase 以下载 2026-02-24 的数据"""

import pymysql
from datetime import datetime

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
    print("🔄 更新 investbase 表 - 准备下载 2026-02-24")
    print("="*80)
    
    # 1. 显示当前状态
    print("\n📋 当前状态：")
    cursor.execute("SELECT RecDate, LastDate FROM investbase ORDER BY RecDate DESC LIMIT 1")
    result = cursor.fetchone()
    
    if result:
        rec_date, last_date = result
        print(f"   RecDate: {rec_date}")
        print(f"   LastDate: {last_date}")
    
    # 2. 更新 LastDate 为 2026-02-24
    print("\n🔄 执行更新...")
    
    update_sql = """
        UPDATE investbase 
        SET LastDate = '2026-02-24'
        WHERE RecDate = (SELECT MAX(RecDate) FROM (SELECT RecDate FROM investbase) AS t)
    """
    
    cursor.execute(update_sql)
    conn.commit()
    
    print("   ✅ 更新成功")
    
    # 3. 验证更新结果
    print("\n📋 更新后状态：")
    cursor.execute("SELECT RecDate, LastDate FROM investbase ORDER BY RecDate DESC LIMIT 1")
    result = cursor.fetchone()
    
    if result:
        rec_date, last_date = result
        print(f"   RecDate: {rec_date}")
        print(f"   LastDate: {last_date}")
        
        if str(last_date) == "2026-02-24":
            print("\n✅ 成功！现在可以下载 2026-02-24 的数据了")
        else:
            print("\n⚠️  警告：LastDate 不是 2026-02-24")
    
    print("\n" + "="*80)
    print("💡 下一步：")
    print("="*80)
    print("   1. 刷新浏览器页面（http://localhost:5089）")
    print("   2. 再次点击「下載資料」按钮")
    print("   3. 这次会下载 2026-02-24 的数据")
    print("   4. 预计耗时：~5 分钟（全新数据，不是 UPDATE）")
    print("="*80 + "\n")
    
    cursor.close()
    conn.close()
    
except Exception as e:
    print(f"\n❌ 错误: {e}")
